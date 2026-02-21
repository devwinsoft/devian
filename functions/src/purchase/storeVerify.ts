/**
 * storeVerify.ts — Apple / Google 스토어 서버 검증
 *
 * 46 스킬 결정사항 준수:
 *   D. Google: androidpublisher v3, storePurchaseId = purchaseToken
 *   E. Apple : verifyReceipt, storePurchaseId = transaction_id, 21007 sandbox 재시도
 */

import {google} from "googleapis";
import * as functions from "firebase-functions";
import {defineString} from "firebase-functions/params";

// ── 시크릿 (46 스킬 G 섹션) ──
const GOOGLE_CREDENTIALS_JSON = defineString("GOOGLE_APPLICATION_CREDENTIALS_JSON");
const APPLE_SHARED_SECRET = defineString("APPLE_SHARED_SECRET");

// ── 타입 ──
export interface StoreVerifyResult {
  valid: boolean;
  storePurchaseId: string; // Google: purchaseToken, Apple: transaction_id
  purchasedAtMs: number; // 스토어 검증 응답에서 추출한 구매 시각(밀리초) — 클라 시간 사용 금지
  rawResponse: any;
}

// ═══════════════════════════════════════════
//  Google Play 검증  (46 스킬 D 섹션)
// ═══════════════════════════════════════════

export async function verifyGooglePlay(
  packageName: string,
  storeProductId: string,
  kind: string,
  purchaseToken: string,
): Promise<StoreVerifyResult> {
  const credentials = JSON.parse(GOOGLE_CREDENTIALS_JSON.value());
  const auth = new google.auth.GoogleAuth({
    credentials,
    scopes: ["https://www.googleapis.com/auth/androidpublisher"],
  });

  const androidPublisher = google.androidpublisher({version: "v3", auth});

  let response: any;

  if (kind === "Subscription") {
    // 46 스킬 D: kind == Subscription → purchases.subscriptions.get
    response = await androidPublisher.purchases.subscriptions.get({
      packageName,
      subscriptionId: storeProductId,
      token: purchaseToken,
    });
  } else {
    // 46 스킬 D: 그 외 → purchases.products.get
    response = await androidPublisher.purchases.products.get({
      packageName,
      productId: storeProductId,
      token: purchaseToken,
    });
  }

  const data = response.data;
  const valid = kind === "Subscription"
    ? (data.paymentState !== undefined) // subscriptions: paymentState exists
    : (data.purchaseState === 0); // products: 0 = purchased

  // Google 구매 시각 추출:
  //   products → purchaseTimeMillis (46 스킬 F2)
  //   subscriptions → startTimeMillis (구독 시작 = 구매 시각)
  const purchasedAtMs = kind === "Subscription"
    ? Number(data.startTimeMillis ?? 0)
    : Number(data.purchaseTimeMillis ?? 0);

  return {
    valid,
    // 46 스킬 D: storePurchaseId 규칙(고정) = purchaseToken
    storePurchaseId: purchaseToken,
    purchasedAtMs,
    rawResponse: data,
  };
}

// ═══════════════════════════════════════════
//  Apple 검증  (46 스킬 E 섹션)
// ═══════════════════════════════════════════

const APPLE_PRODUCTION_URL = "https://buy.itunes.apple.com/verifyReceipt";
const APPLE_SANDBOX_URL = "https://sandbox.itunes.apple.com/verifyReceipt";

async function callAppleVerifyReceipt(
  url: string,
  receiptData: string,
  password: string,
): Promise<any> {
  const res = await fetch(url, {
    method: "POST",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({
      "receipt-data": receiptData,
      "password": password,
      "exclude-old-transactions": true,
    }),
  });
  return res.json();
}

export async function verifyApple(
  receiptData: string,
): Promise<StoreVerifyResult> {
  const password = APPLE_SHARED_SECRET.value();

  // 프로덕션 먼저 시도
  let result = await callAppleVerifyReceipt(
    APPLE_PRODUCTION_URL,
    receiptData,
    password,
  );

  // 46 스킬 E: status 21007이면 sandbox endpoint로 재시도
  if (result.status === 21007) {
    functions.logger.info("Apple verifyReceipt: 21007 → sandbox 재시도");
    result = await callAppleVerifyReceipt(
      APPLE_SANDBOX_URL,
      receiptData,
      password,
    );
  }

  const valid = result.status === 0;

  // 46 스킬 E: storePurchaseId = transaction_id
  let transactionId = "";
  let purchasedAtMs = 0;
  if (valid && result.receipt?.in_app?.length > 0) {
    const inApp = result.receipt.in_app as any[];

    // purchase_date_ms가 가장 큰 항목을 "최신"으로 선택 (배열 정렬 가정 금지)
    let bestTx: any = null;
    let bestMs = 0;

    for (const tx of inApp) {
      const ms = Number(tx?.purchase_date_ms ?? 0);
      if (ms > bestMs) {
        bestMs = ms;
        bestTx = tx;
      }
    }

    transactionId = bestTx?.transaction_id ?? "";
    purchasedAtMs = bestMs;
  }

  return {
    valid,
    storePurchaseId: transactionId,
    purchasedAtMs,
    rawResponse: result,
  };
}
