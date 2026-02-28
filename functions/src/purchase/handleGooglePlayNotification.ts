/**
 * handleGooglePlayNotification.ts — Google Play RTDN (Real-Time Developer Notifications)
 *
 * Pub/Sub 트리거: Google Play가 환불/취소 이벤트 발생 시 메시지를 전송한다.
 * 이 함수가 메시지를 수신하여:
 *   1. RTDN 메시지 파싱 (oneTimeProductNotification / subscriptionNotification)
 *   2. 환불 관련 타입만 처리 (ONE_TIME_PRODUCT_CANCELED=2, SUBSCRIPTION_REVOKED=12)
 *   3. Firestore에서 purchase 문서 조회 (collection group query by storePurchaseId)
 *   4. Google Play API 재검증 (defense in depth)
 *   5. purchase 문서 verifyStatus 업데이트 + entitlements 정리
 */

import {onMessagePublished} from "firebase-functions/v2/pubsub";
import * as admin from "firebase-admin";
import * as logger from "firebase-functions/logger";
import {verifyGooglePlay, GOOGLE_CREDENTIALS_SECRET} from "./storeVerify";

// ── Constants ──
// .env.{project} 에서 RTDN_TOPIC 읽음 (Firebase CLI가 deploy 전 process.env에 로드)
const RTDN_TOPIC = process.env.RTDN_TOPIC!;
const ONE_TIME_PRODUCT_CANCELED = 2;
const SUBSCRIPTION_REVOKED = 12;

// ── RTDN Message Types ──
interface OneTimeProductNotification {
  version: string;
  notificationType: number;
  purchaseToken: string;
  sku: string;
}

interface SubscriptionNotification {
  version: string;
  notificationType: number;
  purchaseToken: string;
  subscriptionId: string;
}

interface VoidedPurchaseNotification {
  purchaseToken: string;
  orderId?: string;
  productType: number; // 0=subscription, 1=one-time product
  refundType?: number; // 0=full_refund, 1=quantity_based_partial_refund
}

interface DeveloperNotification {
  version: string;
  packageName: string;
  eventTimeMillis: string;
  oneTimeProductNotification?: OneTimeProductNotification;
  subscriptionNotification?: SubscriptionNotification;
  voidedPurchaseNotification?: VoidedPurchaseNotification;
  testNotification?: unknown;
}

// ── Helpers ──
function entitlementsDocRef(uid: string) {
  return admin.firestore()
    .collection("users").doc(uid)
    .collection("entitlements").doc("current");
}

function resolveStoreProductIdFromDoc(purchaseData: FirebaseFirestore.DocumentData): string {
  // purchase 문서에 직접 저장된 storeProductId 우선
  if (purchaseData.storeProductId) return String(purchaseData.storeProductId);
  // storeResponse에서 productId 추출 시도
  const storeResponseStr = String(purchaseData.storeResponse ?? "");
  if (storeResponseStr) {
    try {
      const storeResponse = JSON.parse(storeResponseStr);
      if (storeResponse.productId) return String(storeResponse.productId);
    } catch { /* ignore */ }
  }
  // internalProductId는 내부 ID이므로 store SKU와 다를 수 있다 → 사용 금지
  return "";
}

function parseDeveloperNotification(data: unknown): DeveloperNotification | null {
  if (!data || typeof data !== "object") return null;
  const obj = data as Record<string, unknown>;
  if (typeof obj.packageName !== "string") return null;
  return data as DeveloperNotification;
}

// ═══════════════════════════════════════════
//  handleGooglePlayNotification (Pub/Sub)
// ═══════════════════════════════════════════

export const handleGooglePlayNotification = onMessagePublished(
  {
    topic: RTDN_TOPIC,
    secrets: [GOOGLE_CREDENTIALS_SECRET],
  },
  async (event) => {
    // ── 1) RTDN 메시지 디코딩 ──
    let notification: DeveloperNotification | null = null;

    // firebase-functions v2: event.data.message.json (auto-decoded) 또는 base64 data
    if (event.data.message.json) {
      notification = parseDeveloperNotification(event.data.message.json);
    }

    if (!notification && event.data.message.data) {
      try {
        const decoded = Buffer.from(event.data.message.data, "base64").toString("utf8");
        notification = parseDeveloperNotification(JSON.parse(decoded));
      } catch (err: any) {
        logger.error(`[handleGooglePlayNotification] Failed to decode message: ${err.message}`);
        return; // 파싱 불가 → 재시도 의미 없음
      }
    }

    if (!notification) {
      logger.warn("[handleGooglePlayNotification] Empty or invalid RTDN message.");
      return;
    }

    // ── 2) 환불 관련 타입 필터 ──
    let purchaseToken: string;
    let storeProductId: string;
    let kind: string;
    let targetVerifyStatus: "REFUNDED" | "REVOKED";

    if (notification.voidedPurchaseNotification) {
      // 환불/취소된 구매 (Play Console 환불, 결제 취소 등) — 가장 일반적인 환불 경로
      purchaseToken = notification.voidedPurchaseNotification.purchaseToken;
      storeProductId = ""; // purchase 문서에서 확인
      kind = ""; // purchase 문서에서 확인
      targetVerifyStatus = "REFUNDED";
    } else if (notification.oneTimeProductNotification &&
        notification.oneTimeProductNotification.notificationType === ONE_TIME_PRODUCT_CANCELED) {
      purchaseToken = notification.oneTimeProductNotification.purchaseToken;
      storeProductId = notification.oneTimeProductNotification.sku;
      kind = ""; // purchase 문서에서 확인
      targetVerifyStatus = "REFUNDED";
    } else if (notification.subscriptionNotification &&
               notification.subscriptionNotification.notificationType === SUBSCRIPTION_REVOKED) {
      purchaseToken = notification.subscriptionNotification.purchaseToken;
      storeProductId = notification.subscriptionNotification.subscriptionId;
      kind = "Subscription";
      targetVerifyStatus = "REVOKED";
    } else if (notification.testNotification) {
      logger.info(`[handleGooglePlayNotification] Test notification received. pkg=${notification.packageName}`);
      return;
    } else {
      // 환불 이외 알림 → 무시
      const nType = notification.oneTimeProductNotification?.notificationType
        ?? notification.subscriptionNotification?.notificationType
        ?? "unknown";
      logger.info(`[handleGooglePlayNotification] Ignoring notificationType=${nType} pkg=${notification.packageName}`);
      return;
    }

    logger.info(
      `[handleGooglePlayNotification] Refund detected. pkg=${notification.packageName} ` +
      `token=${purchaseToken.substring(0, 16)}... target=${targetVerifyStatus}`,
    );

    // ── 3) Firestore에서 purchase 문서 조회 ──
    const purchasesQuery = admin.firestore()
      .collectionGroup("purchases")
      .where("storePurchaseId", "==", purchaseToken)
      .where("storeKey", "==", "google")
      .limit(1);

    const snapshot = await purchasesQuery.get();

    if (snapshot.empty) {
      logger.warn(
        `[handleGooglePlayNotification] No purchase found for storePurchaseId. ` +
        `pkg=${notification.packageName} token=${purchaseToken.substring(0, 16)}...`,
      );
      return; // 문서 없음 → 재시도 의미 없음
    }

    const purchaseDoc = snapshot.docs[0];
    const purchaseData = purchaseDoc.data();
    const currentVerifyStatus = String(purchaseData.verifyStatus ?? purchaseData.status ?? "");

    // ── 4) 멱등성: 이미 처리된 문서 ──
    if (currentVerifyStatus === "REFUNDED" || currentVerifyStatus === "REVOKED") {
      logger.info(
        `[handleGooglePlayNotification] Already ${currentVerifyStatus}. ` +
        `purchaseId=${purchaseData.purchaseId} — skipping.`,
      );
      return;
    }

    if (currentVerifyStatus !== "GRANTED") {
      logger.warn(
        `[handleGooglePlayNotification] Unexpected verifyStatus=${currentVerifyStatus}. ` +
        `Only GRANTED can be refunded. purchaseId=${purchaseData.purchaseId}`,
      );
      return;
    }

    const uid = String(purchaseData.uid ?? "");
    if (!uid) {
      logger.error(`[handleGooglePlayNotification] Purchase doc missing uid. docPath=${purchaseDoc.ref.path}`);
      return;
    }

    // ── 5) Google Play API 재검증 (defense in depth) ──
    const storedKind = String(purchaseData.kind ?? kind);
    const packageName = notification.packageName;
    // storeProductId: RTDN에 포함된 경우 사용, 없으면 purchase 문서의 storeResponse에서 복원
    const resolvedStoreProductId = storeProductId || resolveStoreProductIdFromDoc(purchaseData);

    if (!resolvedStoreProductId) {
      logger.warn(
        `[handleGooglePlayNotification] Cannot resolve storeProductId for re-verify. ` +
        `Skipping re-verify. purchaseId=${purchaseData.purchaseId}`,
      );
      // 재검증 건너뛰고 바로 환불 처리 (RTDN 자체가 Google 발행이므로 신뢰)
    } else {
      let reVerifyResult;
      try {
        reVerifyResult = await verifyGooglePlay(
          packageName,
          resolvedStoreProductId,
          storedKind,
          purchaseToken,
        );
      } catch (err: any) {
        logger.error(
          `[handleGooglePlayNotification] Re-verify failed. ` +
          `purchaseId=${purchaseData.purchaseId}: ${err.message}`,
        );
        throw err; // transient → Pub/Sub 재시도
      }

      // products: purchaseState === 1 (canceled) 확인
      if (storedKind !== "Subscription") {
        const purchaseState = reVerifyResult.rawResponse?.purchaseState;
        if (purchaseState === 0) {
          logger.warn(
            `[handleGooglePlayNotification] Re-verify shows purchaseState=0 (active). ` +
            `Aborting refund. purchaseId=${purchaseData.purchaseId}`,
          );
          return;
        }
      }
    }

    // ── 6) Firestore 트랜잭션: purchase 상태 + entitlements 정리 ──
    const purchaseRef = purchaseDoc.ref;
    const entitlementRef = entitlementsDocRef(uid);

    await admin.firestore().runTransaction(async (tx) => {
      const [txPurchaseSnap, txEntitlementSnap] = await Promise.all([
        tx.get(purchaseRef),
        tx.get(entitlementRef),
      ]);

      if (!txPurchaseSnap.exists) {
        logger.error("[handleGooglePlayNotification] Purchase doc disappeared during transaction.");
        return;
      }

      const txPurchaseData = txPurchaseSnap.data()!;
      const txVerifyStatus = String(txPurchaseData.verifyStatus ?? txPurchaseData.status ?? "");

      // 멱등성: 트랜잭션 내 재확인
      if (txVerifyStatus === "REFUNDED" || txVerifyStatus === "REVOKED") {
        return;
      }

      // 6-a) purchase 문서 업데이트
      tx.set(purchaseRef, {
        verifyStatus: targetVerifyStatus,
        status: targetVerifyStatus, // legacy compatibility
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
        refundedAt: admin.firestore.FieldValue.serverTimestamp(),
        refundSource: "RTDN",
      }, {merge: true});

      // 6-b) entitlements 정리
      if (!txEntitlementSnap.exists) {
        return; // entitlements 없으면 정리 불필요
      }

      const entitlementData = txEntitlementSnap.data()!;
      const purchaseKind = String(txPurchaseData.kind ?? "");
      const entitlementPatch: Record<string, unknown> = {
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      };

      if (purchaseKind === "Rental") {
        // rentalId가 저장되어 있으면 사용, 없으면 internalProductId fallback
        const rentalKey = String(txPurchaseData.rentalId ?? txPurchaseData.internalProductId ?? "");
        const rentals = {...(entitlementData.rentals ?? {})} as Record<string, number>;
        if (rentalKey && rentals[rentalKey] !== undefined) {
          delete rentals[rentalKey];
          entitlementPatch.rentals = rentals;
        }
      } else if (purchaseKind === "SeasonPass") {
        const ownedSeasonPasses = Array.isArray(entitlementData.ownedSeasonPasses)
          ? [...entitlementData.ownedSeasonPasses]
          : [];
        const internalProductId = String(txPurchaseData.internalProductId ?? "");
        const idx = ownedSeasonPasses.indexOf(internalProductId);
        if (idx >= 0) {
          ownedSeasonPasses.splice(idx, 1);
          entitlementPatch.ownedSeasonPasses = ownedSeasonPasses;
        }
      }
      // Consumable: entitlements projection 없음 → 정리 불필요
      // Subscription: 현재 서버 entitlements projection 없음

      tx.set(entitlementRef, entitlementPatch, {merge: true});
    });

    logger.info(
      `[handleGooglePlayNotification] Refund processed. ` +
      `uid=${uid} purchaseId=${purchaseData.purchaseId} status=${targetVerifyStatus}`,
    );
  },
);
