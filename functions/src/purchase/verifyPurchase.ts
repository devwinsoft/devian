/**
 * verifyPurchase.ts — Firebase Callable: 결제 검증 + 멱등 지급 (v3)
 *
 * 46 스킬 결정사항 전수 준수:
 *   B. Callable 이름 = "verifyPurchase", context.auth.uid 필수
 *   B. 요청 키: storeKey, internalProductId, kind, payload (+ optional storeProductId)
 *   B. 응답 키: resultStatus, grants, entitlementsSnapshot (+ purchaseId, verifyStatus, clientGrantStatus, storeConfirmStatus)
 *   C. 멱등키: purchaseId = "{storeKey}_{storePurchaseId}"
 *   F. grants 빈 배열 허용, GRANTED/ALREADY_GRANTED 시 entitlementsSnapshot 반환
 *   G. 리전: asia-northeast3
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as logger from "firebase-functions/logger";
import {verifyGooglePlay, verifyApple, GOOGLE_CREDENTIALS_SECRET} from "./storeVerify";

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

type VerifyStatus = "GRANTED" | "REJECTED" | "PENDING" | "REVOKED" | "REFUNDED";
type ClientGrantStatus = "PENDING" | "APPLIED_ACKED" | "FAILED_REPORTED";
type StoreConfirmStatus = "PENDING" | "CONFIRMED";
const RENTAL_DURATION_MS = 30 * 24 * 60 * 60 * 1000;

// Restore projection policy (implemented in verifyPurchase transaction for new GRANTED purchases):
// - SeasonPass: keep internalProductId ownership projection on server (e.g. ownedSeasonPasses[]).
// - Rental: keep simple map { [rentalId || internalProductId]: expiresAtServerUtcMs } on server.
// - Rental expiry policy on new GRANTED purchase:
//     newExpiry = max(existingExpiry, serverNowUtcMs) + 30 days
// - DO NOT extend rental expiry on ALREADY_GRANTED (idempotent retry).

function getVerifyStatus(data: FirebaseFirestore.DocumentData | undefined): string {
  return String(data?.verifyStatus ?? data?.status ?? "");
}

function getClientGrantStatus(data: FirebaseFirestore.DocumentData | undefined): ClientGrantStatus {
  const raw = String(data?.clientGrantStatus ?? "");
  if (raw === "APPLIED_ACKED") return "APPLIED_ACKED";
  if (raw === "FAILED_REPORTED") return "FAILED_REPORTED";
  if (raw === "PENDING") return "PENDING";

  // Legacy documents (pre-clientGrantStatus) are treated as already delivered
  // to avoid duplicate local reward on first app after migration.
  if (getVerifyStatus(data) === "GRANTED") return "APPLIED_ACKED";

  return "PENDING";
}

function getStoreConfirmStatus(data: FirebaseFirestore.DocumentData | undefined): StoreConfirmStatus {
  const raw = String(data?.storeConfirmStatus ?? "");
  if (raw === "CONFIRMED") return "CONFIRMED";
  if (raw === "PENDING") return "PENDING";

  // Legacy documents (pre-storeConfirmStatus) are treated as confirmed so we do not
  // regress into duplicate/unsafe confirm attempts after migration.
  if (getVerifyStatus(data) === "GRANTED") return "CONFIRMED";

  return "PENDING";
}

// ── Entitlements 스냅샷 읽기 헬퍼 ──
async function readEntitlementsSnapshot(uid: string) {
  const snap = await entitlementsDocRef(uid).get();
  const serverNowUtcMs = Date.now();
  if (!snap.exists) {
    return {ownedSeasonPasses: [], rentals: {}, currencyBalances: {}, serverNowUtcMs};
  }
  const d = snap.data()!;
  return {
    ownedSeasonPasses: d.ownedSeasonPasses ?? [],
    rentals: d.rentals ?? {},
    currencyBalances: d.currencyBalances ?? {},
    serverNowUtcMs,
  };
}

// ── 입력 검증 ──
type StoreKey = "apple" | "google";
type Kind = "Consumable" | "Rental" | "Subscription" | "SeasonPass";

interface VerifyRequest {
  storeKey: StoreKey;
  internalProductId: string;
  storeProductId?: string;
  packageName?: string;
  kind: Kind;
  payload: string;
  rentalId?: string; // Rental: REWARD table Id (entitlements key). Falls back to internalProductId.
}

function validateRequest(data: any): VerifyRequest {
  const {storeKey, internalProductId, storeProductId, packageName, kind, payload, rentalId} = data;

  if (!storeKey || !["apple", "google"].includes(storeKey)) {
    throw new HttpsError("invalid-argument", "storeKey must be 'apple' or 'google'");
  }
  if (!internalProductId || typeof internalProductId !== "string") {
    throw new HttpsError("invalid-argument", "internalProductId is required");
  }
  if (storeProductId !== undefined && typeof storeProductId !== "string") {
    throw new HttpsError("invalid-argument", "storeProductId must be string when provided");
  }
  if (packageName !== undefined && typeof packageName !== "string") {
    throw new HttpsError("invalid-argument", "packageName must be string when provided");
  }
  if (!kind || !["Consumable", "Rental", "Subscription", "SeasonPass"].includes(kind)) {
    throw new HttpsError("invalid-argument", "kind must be Consumable/Rental/Subscription/SeasonPass");
  }
  if (!payload || typeof payload !== "string") {
    throw new HttpsError("invalid-argument", "payload is required");
  }
  if (rentalId !== undefined && typeof rentalId !== "string") {
    throw new HttpsError("invalid-argument", "rentalId must be string when provided");
  }

  return {storeKey, internalProductId, storeProductId, packageName, kind, payload, rentalId};
}

interface GoogleReceiptInfo {
  packageName: string;
  productId: string;
  purchaseToken: string;
}

function tryParseJsonObject(input: string): Record<string, unknown> | null {
  try {
    const parsed = JSON.parse(input);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
      return null;
    }
    return parsed as Record<string, unknown>;
  } catch {
    return null;
  }
}

function parseGoogleReceipt(payload: string, fallbackProductId: string, fallbackPackageName = ""): GoogleReceiptInfo {
  const trimmed = String(payload ?? "").trim();
  // Some Unity IAP / store flows may hand us a raw purchase token instead of JSON receipt.
  // In this case, req.storeProductId and req.packageName must be supplied by the client.
  if (trimmed && !trimmed.startsWith("{")) {
    const purchaseToken = trimmed;
    const packageName = String(fallbackPackageName ?? "");
    const productId = String(fallbackProductId ?? "");
    if (!packageName || !productId || !purchaseToken) {
      throw new Error(
        `Google receipt token payload requires fallback package/product. package=${!!packageName} product=${!!productId} token=${!!purchaseToken}`,
      );
    }
    return {
      packageName,
      productId,
      purchaseToken,
    };
  }

  const outerParsed = tryParseJsonObject(trimmed);
  if (!outerParsed) {
    throw new Error("Google receipt payload is not a valid JSON object.");
  }
  const outer = outerParsed;
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
    const payloadNodeTrimmed = payloadNode.trim();
    const parsedPayloadNode = payloadNodeTrimmed.startsWith("{") ? tryParseJsonObject(payloadNodeTrimmed) : null;
    if (parsedPayloadNode) {
      payloadNode = parsedPayloadNode;
    } else {
      // Some SDK variants place the raw Google purchaseToken in outer.Payload.
      payloadNode = {purchaseToken: payloadNodeTrimmed};
    }
  }

  let purchaseData: any = payloadNode;
  if (payloadNode && typeof payloadNode === "object" && typeof payloadNode.json === "string") {
    const purchaseJson = payloadNode.json.trim();
    const parsedPurchaseJson = purchaseJson.startsWith("{") ? tryParseJsonObject(purchaseJson) : null;
    if (parsedPurchaseJson) {
      purchaseData = parsedPurchaseJson;
    } else {
      // Some SDK variants put the raw Google purchaseToken in payload.json.
      purchaseData = {purchaseToken: purchaseJson};
    }
  }

  const packageName =
    purchaseData?.packageName ??
    payloadNode?.packageName ??
    outer.packageName ??
    fallbackPackageName ??
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

// Exposed only for unit tests. Do not use from production call paths other than verifyPurchase.
export const __test_parseGoogleReceipt = parseGoogleReceipt;

// ═══════════════════════════════════════════
//  verifyPurchase Callable (46 스킬 B 섹션)
// ═══════════════════════════════════════════

export const verifyPurchase = onCall(
  {secrets: [GOOGLE_CREDENTIALS_SECRET]},
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
        let googleReceipt: GoogleReceiptInfo;
        try {
          googleReceipt = parseGoogleReceipt(
            req.payload,
            req.storeProductId ?? req.internalProductId,
            req.packageName ?? "",
          );
        } catch (err: any) {
          logger.error(`[verifyPurchase] Google receipt parse error: ${err?.message ?? String(err)}`);
          return {
            resultStatus: "REJECTED",
            verifyStatus: "REJECTED",
            rejectReason: "STORE_VERIFY_PARSE_ERROR_GOOGLE_RECEIPT",
            grants: [],
          };
        }

        const googleVerifyProductId = req.storeProductId ?? googleReceipt.productId;
        if (googleReceipt.productId && req.storeProductId && googleReceipt.productId !== req.storeProductId) {
          logger.warn(
            `[verifyPurchase] Google receipt productId mismatch. uid=${uid} internal=${req.internalProductId} ` +
            `receiptProductId=${googleReceipt.productId} requestStoreProductId=${req.storeProductId} (using requestStoreProductId)`,
          );
        }
        storeResult = await verifyGooglePlay(
          googleReceipt.packageName,
          googleVerifyProductId,
          req.kind,
          googleReceipt.purchaseToken,
        );
      } else {
        // Apple: payload = receipt raw (base64)
        storeResult = await verifyApple(req.payload);
      }
    } catch (err: any) {
      const errCode = err?.code ?? err?.response?.status ?? "unknown";
      logger.error(`[verifyPurchase] Store verification error: ${err.message} | code=${errCode}`);
      return {
        resultStatus: "REJECTED",
        verifyStatus: "REJECTED",
        rejectReason: "STORE_VERIFY_ERROR",
        grants: [],
      };
    }

    // 검증 실패 → REJECTED
    if (!storeResult.valid) {
      // purchaseState를 rejectReason에 포함: 클라이언트가 취소된 구매(state=1)를 ConfirmPurchase로 제거 가능
      const purchaseState = storeResult.rawResponse?.purchaseState;
      const rejectDetail = purchaseState !== undefined ? `STORE_VERIFY_INVALID:purchaseState=${purchaseState}` : "STORE_VERIFY_INVALID";
      logger.warn(`[verifyPurchase] Store verification failed for uid=${uid} product=${req.internalProductId} store=${req.storeKey} kind=${req.kind} ${rejectDetail}`);
      return {
        resultStatus: "REJECTED",
        verifyStatus: "REJECTED",
        rejectReason: rejectDetail,
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
        verifyStatus: "REJECTED",
        rejectReason: "STORE_VERIFY_MISSING_PURCHASE_TIME",
        grants: [],
      };
    }

    // ── 2) kind별 구매 제한 검사 ──
    // Consumable / Rental / Subscription: 제한 없음
    // SeasonPass: 동일 internalProductId로 구매 기록 1건이라도 있으면 거부
    if (req.kind === "SeasonPass") {
      const existingSnap = await admin.firestore()
        .collection("users").doc(uid)
        .collection("purchases")
        .where("internalProductId", "==", req.internalProductId)
        .where("kind", "==", "SeasonPass")
        .where("verifyStatus", "==", "GRANTED")
        .limit(1)
        .get();

      const existingLegacySnap = existingSnap.empty
        ? await admin.firestore()
          .collection("users").doc(uid)
          .collection("purchases")
          .where("internalProductId", "==", req.internalProductId)
          .where("kind", "==", "SeasonPass")
          .where("status", "==", "GRANTED")
          .limit(1)
          .get()
        : existingSnap;

      if (!existingLegacySnap.empty) {
        logger.warn(`[verifyPurchase] SeasonPass purchase blocked: uid=${uid} product=${req.internalProductId} (already purchased)`);
        return {
          resultStatus: "REJECTED",
          verifyStatus: "REJECTED",
          rejectReason: "SEASON_PASS_ALREADY_OWNED",
          grants: [],
        };
      }
    }
    // Consumable, Rental, Subscription: 제한 없음

    // ── 3) 멱등키 생성 (46 스킬 C 섹션) ──
    // purchaseId = "{storeKey}_{storePurchaseId}"
    const purchaseId = `${req.storeKey}_${storeResult.storePurchaseId}`;

    // ── 4) Firestore 트랜잭션: 원장 upsert + 중복 지급 방지 ──
    const db = admin.firestore();
    const purchaseRef = purchaseDocRef(uid, purchaseId);
    const entitlementRef = entitlementsDocRef(uid);

    const txResult = await db.runTransaction(async (tx) => {
      // Firestore 트랜잭션: 모든 read를 write보다 먼저 수행해야 함
      const existingDoc = await tx.get(purchaseRef);
      const entitlementSnap = await tx.get(entitlementRef);

      // 46 스킬 C: 이미 지급 완료면 ALREADY_GRANTED 반환(중복 지급 금지)
      if (existingDoc.exists) {
        const existing = existingDoc.data()!;
        if (getVerifyStatus(existing) === "GRANTED") {
          const clientGrantStatus = getClientGrantStatus(existing);
          const storeConfirmStatus = getStoreConfirmStatus(existing);

          // Backfill new fields for legacy docs without changing idempotent semantics.
          if (!existing.verifyStatus || !existing.clientGrantStatus || !existing.storeConfirmStatus) {
            tx.set(purchaseRef, {
              verifyStatus: "GRANTED",
              status: "GRANTED", // legacy compatibility (temporary)
              clientGrantStatus,
              storeConfirmStatus,
            }, {merge: true});
          }

          return {
            resultStatus: "ALREADY_GRANTED",
            verifyStatus: "GRANTED" as VerifyStatus,
            clientGrantStatus,
            storeConfirmStatus,
          };
        }
      }

      // 원장 upsert
      // storePurchasedAt: 스토어 검증 응답에서 추출한 구매 시각 (클라 시간 사용 금지, fallback 없음)
      const storePurchasedAt = admin.firestore.Timestamp.fromMillis(storeResult.purchasedAtMs);

      tx.set(purchaseRef, {
        purchaseId,
        uid,
        storeKey: req.storeKey,
        storeProductId: req.storeProductId, // Google Play SKU (RTDN 재검증용)
        internalProductId: req.internalProductId,
        kind: req.kind,
        storePurchaseId: storeResult.storePurchaseId,
        storePurchasedAt,
        verifyStatus: "GRANTED",
        status: "GRANTED", // legacy compatibility (temporary)
        clientGrantStatus: "PENDING",
        storeConfirmStatus: "PENDING",
        grantedAt: admin.firestore.FieldValue.serverTimestamp(),
        storeResponse: JSON.stringify(storeResult.rawResponse),
        // Rental: rentalId 저장 (RTDN 환불 시 entitlements 정리용)
        ...(req.kind === "Rental" ? {rentalId: req.rentalId || req.internalProductId} : {}),
      });

      // entitlements projection 갱신 (SeasonPass ownership / Rental expiry)
      const entitlementData = (entitlementSnap.exists ? entitlementSnap.data() : undefined) ?? {};
      const entitlementPatch: Record<string, unknown> = {
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      };

      if (req.kind === "SeasonPass") {
        const ownedSeasonPasses = Array.isArray(entitlementData.ownedSeasonPasses)
          ? [...entitlementData.ownedSeasonPasses]
          : [];
        if (!ownedSeasonPasses.includes(req.internalProductId)) {
          ownedSeasonPasses.push(req.internalProductId);
        }
        entitlementPatch.ownedSeasonPasses = ownedSeasonPasses;
      } else if (req.kind === "Rental") {
        const serverNowUtcMs = Date.now();
        const rentalsRaw = (entitlementData.rentals && typeof entitlementData.rentals === "object") ? entitlementData.rentals : {};
        const rentals: Record<string, number> = {...rentalsRaw as Record<string, number>};
        // rentalId: REWARD table Id (canonical key). Falls back to internalProductId for backward compatibility.
        const rentalKey = req.rentalId || req.internalProductId;
        const existingExpiryRaw = rentals[rentalKey];
        const existingExpiry = Number.isFinite(existingExpiryRaw) ? Number(existingExpiryRaw) : 0;
        rentals[rentalKey] = Math.max(existingExpiry, serverNowUtcMs) + RENTAL_DURATION_MS;
        entitlementPatch.rentals = rentals;
      }

      tx.set(entitlementRef, entitlementPatch, {merge: true});

      return {
        resultStatus: "GRANTED",
        verifyStatus: "GRANTED" as VerifyStatus,
        clientGrantStatus: "PENDING" as ClientGrantStatus,
        storeConfirmStatus: "PENDING" as StoreConfirmStatus,
      };
    });

    // ── 5) 응답 구성 (46 스킬 B + F 섹션) ──
    // 46 스킬 F: grants 빈 배열 허용
    // 46 스킬 F: GRANTED/ALREADY_GRANTED 시 entitlementsSnapshot 반환
    const entitlementsSnapshot = await readEntitlementsSnapshot(uid);

    logger.info(`[verifyPurchase] uid=${uid} purchaseId=${purchaseId} result=${txResult.resultStatus} clientGrant=${txResult.clientGrantStatus} confirm=${txResult.storeConfirmStatus}`);

    return {
      purchaseId,
      resultStatus: txResult.resultStatus,
      verifyStatus: txResult.verifyStatus,
      clientGrantStatus: txResult.clientGrantStatus,
      storeConfirmStatus: txResult.storeConfirmStatus,
      grants: [],
      entitlementsSnapshot,
    };
  },
);
