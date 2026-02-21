/**
 * verifyPurchase.ts — Firebase Callable: 결제 검증 + 멱등 지급
 *
 * 46 스킬 결정사항 전수 준수:
 *   B. Callable 이름 = "verifyPurchase", context.auth.uid 필수
 *   B. 요청 키: storeKey, internalProductId, kind, payload (+ optional storeProductId)
 *   B. 응답 키: resultStatus, grants, entitlementsSnapshot
 *   C. 멱등키: purchaseId = "{storeKey}_{storePurchaseId}"
 *   F. grants 빈 배열 허용, GRANTED/ALREADY_GRANTED 시 entitlementsSnapshot 반환
 *   G. 리전: asia-northeast3
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as logger from "firebase-functions/logger";
import {verifyGooglePlay, verifyApple} from "./storeVerify";

// ── Firestore 참조 헬퍼 ──
function purchaseDocRef(uid: string, purchaseId: string) {
  return admin.firestore()
    .collection("users").doc(uid)
    .collection("purchases").doc(purchaseId);
}

function entitlementsDocRef(uid: string) {
  return admin.firestore()
    .collection("users").doc(uid)
    .collection("entitlements").doc("current");
}

// ── Entitlements 스냅샷 읽기 헬퍼 ──
async function readEntitlementsSnapshot(uid: string) {
  const snap = await entitlementsDocRef(uid).get();
  if (!snap.exists) {
    return {noAdsActive: false, ownedSeasonPasses: [], currencyBalances: {}};
  }
  const d = snap.data()!;
  return {
    noAdsActive: d.noAdsActive ?? false,
    ownedSeasonPasses: d.ownedSeasonPasses ?? [],
    currencyBalances: d.currencyBalances ?? {},
  };
}

// ── 입력 검증 ──
type StoreKey = "apple" | "google";
type Kind = "Consumable" | "Rental" | "Subscription" | "SeasonPass";

interface VerifyRequest {
  storeKey: StoreKey;
  internalProductId: string;
  storeProductId?: string;
  kind: Kind;
  payload: string;
}

function validateRequest(data: any): VerifyRequest {
  const {storeKey, internalProductId, storeProductId, kind, payload} = data;

  if (!storeKey || !["apple", "google"].includes(storeKey)) {
    throw new HttpsError("invalid-argument", "storeKey must be 'apple' or 'google'");
  }
  if (!internalProductId || typeof internalProductId !== "string") {
    throw new HttpsError("invalid-argument", "internalProductId is required");
  }
  if (storeProductId !== undefined && typeof storeProductId !== "string") {
    throw new HttpsError("invalid-argument", "storeProductId must be string when provided");
  }
  if (!kind || !["Consumable", "Rental", "Subscription", "SeasonPass"].includes(kind)) {
    throw new HttpsError("invalid-argument", "kind must be Consumable/Rental/Subscription/SeasonPass");
  }
  if (!payload || typeof payload !== "string") {
    throw new HttpsError("invalid-argument", "payload is required");
  }

  return {storeKey, internalProductId, storeProductId, kind, payload};
}

interface GoogleReceiptInfo {
  packageName: string;
  productId: string;
  purchaseToken: string;
}

function parseGoogleReceipt(payload: string, fallbackProductId: string): GoogleReceiptInfo {
  const outer = JSON.parse(payload);
  if (!outer || typeof outer !== "object") {
    throw new Error("Google receipt payload is not an object.");
  }

  // Unity receipt shape:
  //   { Store, TransactionID, Payload }
  // where Payload is:
  //   - stringified { json, signature } OR
  //   - direct { purchaseToken, packageName, productId } in some SDK variants.
  let payloadNode: any = outer.Payload ?? outer.payload ?? outer;
  if (typeof payloadNode === "string") {
    payloadNode = JSON.parse(payloadNode);
  }

  let purchaseData: any = payloadNode;
  if (payloadNode && typeof payloadNode === "object" && typeof payloadNode.json === "string") {
    purchaseData = JSON.parse(payloadNode.json);
  }

  const packageName =
    purchaseData?.packageName ??
    payloadNode?.packageName ??
    outer.packageName ??
    "";
  const productId =
    purchaseData?.productId ??
    payloadNode?.productId ??
    outer.productId ??
    fallbackProductId;
  const purchaseToken =
    purchaseData?.purchaseToken ??
    payloadNode?.purchaseToken ??
    outer.purchaseToken ??
    "";

  if (!packageName || !productId || !purchaseToken) {
    throw new Error(`Google receipt is missing required fields. package=${!!packageName} product=${!!productId} token=${!!purchaseToken}`);
  }

  return {
    packageName: String(packageName),
    productId: String(productId),
    purchaseToken: String(purchaseToken),
  };
}

// ═══════════════════════════════════════════
//  verifyPurchase Callable (46 스킬 B 섹션)
// ═══════════════════════════════════════════

export const verifyPurchase = onCall(
  {region: "asia-northeast3"},
  async (request) => {
    // 46 스킬 B: context.auth.uid 필수(unauthenticated 거부)
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }
    const uid = request.auth.uid;

    // 입력 검증 (46 스킬 B: 요청 스키마 고정 키)
    const req = validateRequest(request.data);

    logger.info(`[verifyPurchase] uid=${uid} store=${req.storeKey} product=${req.internalProductId} kind=${req.kind}`);

    // ── 1) 스토어 서버 검증 ──
    let storeResult;
    try {
      if (req.storeKey === "google") {
        const googleReceipt = parseGoogleReceipt(req.payload, req.storeProductId ?? req.internalProductId);
        storeResult = await verifyGooglePlay(
          googleReceipt.packageName,
          googleReceipt.productId,
          req.kind,
          googleReceipt.purchaseToken,
        );
      } else {
        // Apple: payload = receipt raw (base64)
        storeResult = await verifyApple(req.payload);
      }
    } catch (err: any) {
      logger.error(`[verifyPurchase] Store verification error: ${err.message}`);
      return {
        resultStatus: "REJECTED",
        grants: [],
      };
    }

    // 검증 실패 → REJECTED
    if (!storeResult.valid) {
      logger.warn(`[verifyPurchase] Store verification failed for uid=${uid}`);
      return {
        resultStatus: "REJECTED",
        grants: [],
      };
    }

    // storePurchasedAt은 "영수증/스토어 검증 응답의 구매 시각"이어야 한다.
    // 영수증 시각을 확보하지 못하면 요구사항(영수증 날짜 기준)을 만족할 수 없으므로 REJECTED 처리한다.
    if (!storeResult.purchasedAtMs || storeResult.purchasedAtMs <= 0) {
      logger.error(
        `[verifyPurchase] Missing purchasedAtMs (receipt timestamp). uid=${uid} store=${req.storeKey} product=${req.internalProductId} kind=${req.kind}`,
      );
      return {
        resultStatus: "REJECTED",
        grants: [],
      };
    }

    // ── 2) kind별 구매 제한 검사 ──
    // Consumable: 제한 없음
    // Rental: 동일 internalProductId로 30일 이내 구매 기록 있으면 거부
    // SeasonPass: 동일 internalProductId로 구매 기록 1건이라도 있으면 거부
    if (req.kind === "Rental") {
      const THIRTY_DAYS_MS = 30 * 24 * 60 * 60 * 1000;
      const thresholdMs = admin.firestore.Timestamp.now().toMillis() - THIRTY_DAYS_MS;
      const threshold = admin.firestore.Timestamp.fromMillis(thresholdMs);

      const recentSnap = await admin.firestore()
        .collection("users").doc(uid)
        .collection("purchases")
        .where("internalProductId", "==", req.internalProductId)
        .where("kind", "==", "Rental")
        .where("status", "==", "GRANTED")
        .where("storePurchasedAt", ">=", threshold)
        .limit(1)
        .get();

      if (!recentSnap.empty) {
        logger.warn(`[verifyPurchase] Rental purchase blocked: uid=${uid} product=${req.internalProductId} (already purchased within 30 days)`);
        return {
          resultStatus: "REJECTED",
          rejectReason: "RENTAL_ALREADY_ACTIVE",
          grants: [],
        };
      }
    } else if (req.kind === "SeasonPass") {
      const existingSnap = await admin.firestore()
        .collection("users").doc(uid)
        .collection("purchases")
        .where("internalProductId", "==", req.internalProductId)
        .where("kind", "==", "SeasonPass")
        .where("status", "==", "GRANTED")
        .limit(1)
        .get();

      if (!existingSnap.empty) {
        logger.warn(`[verifyPurchase] SeasonPass purchase blocked: uid=${uid} product=${req.internalProductId} (already purchased)`);
        return {
          resultStatus: "REJECTED",
          rejectReason: "SEASON_PASS_ALREADY_OWNED",
          grants: [],
        };
      }
    }
    // Consumable, Subscription: 제한 없음

    // ── 3) 멱등키 생성 (46 스킬 C 섹션) ──
    // purchaseId = "{storeKey}_{storePurchaseId}"
    const purchaseId = `${req.storeKey}_${storeResult.storePurchaseId}`;

    // ── 4) Firestore 트랜잭션: 원장 upsert + 중복 지급 방지 ──
    const db = admin.firestore();
    const purchaseRef = purchaseDocRef(uid, purchaseId);
    const entitlementRef = entitlementsDocRef(uid);

    const resultStatus = await db.runTransaction(async (tx) => {
      const existingDoc = await tx.get(purchaseRef);

      // 46 스킬 C: 이미 지급 완료면 ALREADY_GRANTED 반환(중복 지급 금지)
      if (existingDoc.exists) {
        const existing = existingDoc.data()!;
        if (existing.status === "GRANTED") {
          return "ALREADY_GRANTED";
        }
      }

      // 원장 upsert
      // storePurchasedAt: 스토어 검증 응답에서 추출한 구매 시각 (클라 시간 사용 금지, fallback 없음)
      const storePurchasedAt = admin.firestore.Timestamp.fromMillis(storeResult.purchasedAtMs);

      tx.set(purchaseRef, {
        purchaseId,
        uid,
        storeKey: req.storeKey,
        internalProductId: req.internalProductId,
        kind: req.kind,
        storePurchaseId: storeResult.storePurchaseId,
        storePurchasedAt,
        status: "GRANTED",
        grantedAt: admin.firestore.FieldValue.serverTimestamp(),
        storeResponse: JSON.stringify(storeResult.rawResponse),
      });

      // entitlements 스냅샷 갱신 (최소: updatedAt 타임스탬프)
      tx.set(entitlementRef, {
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      }, {merge: true});

      return "GRANTED";
    });

    // ── 5) 응답 구성 (46 스킬 B + F 섹션) ──
    // 46 스킬 F: grants 빈 배열 허용
    // 46 스킬 F: GRANTED/ALREADY_GRANTED 시 entitlementsSnapshot 반환
    const entitlementsSnapshot = await readEntitlementsSnapshot(uid);

    logger.info(`[verifyPurchase] uid=${uid} purchaseId=${purchaseId} result=${resultStatus}`);

    return {
      resultStatus,
      grants: [],
      entitlementsSnapshot,
    };
  },
);
