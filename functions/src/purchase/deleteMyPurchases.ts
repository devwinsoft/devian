/**
 * deleteMyPurchases.ts — Firebase Callable: 개발/테스트용 purchase 기록 전체 삭제
 *
 * 46 스킬 F3 결정사항 준수:
 *   - Callable 이름: deleteMyPurchases
 *   - 인증: context.auth.uid 필수
 *   - 환경 변수 가드: ALLOW_PURCHASE_DELETE=true 일 때만 동작
 *   - 삭제 대상: /users/{uid}/purchases/* + /users/{uid}/entitlements/current 초기화
 *   - 리전: asia-northeast3
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as logger from "firebase-functions/logger";

const BATCH_SIZE = 500;

export const deleteMyPurchases = onCall(
  {region: "asia-northeast3"},
  async (request) => {
    // 프로덕션 가드: 환경 변수 ALLOW_PURCHASE_DELETE=true 일 때만 동작
    if (process.env.ALLOW_PURCHASE_DELETE !== "true") {
      throw new HttpsError(
        "failed-precondition",
        "deleteMyPurchases is disabled. Set ALLOW_PURCHASE_DELETE=true to enable.",
      );
    }

    // 인증 필수 (46 스킬 B)
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }
    const uid = request.auth.uid;

    logger.info(`[deleteMyPurchases] uid=${uid} — starting`);

    const db = admin.firestore();
    const purchasesCol = db.collection("users").doc(uid).collection("purchases");

    // purchases 컬렉션 전체 삭제 (batch 단위)
    let deletedCount = 0;
    let snapshot = await purchasesCol.limit(BATCH_SIZE).get();

    while (!snapshot.empty) {
      const batch = db.batch();
      for (const doc of snapshot.docs) {
        batch.delete(doc.ref);
      }
      await batch.commit();
      deletedCount += snapshot.docs.length;

      if (snapshot.docs.length < BATCH_SIZE) {
        break;
      }
      snapshot = await purchasesCol.limit(BATCH_SIZE).get();
    }

    // entitlements/current 초기값으로 리셋
    const entitlementRef = db
      .collection("users").doc(uid)
      .collection("entitlements").doc("current");

    await entitlementRef.set({
      noAdsActive: false,
      ownedSeasonPasses: [],
      currencyBalances: {},
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    });

    logger.info(`[deleteMyPurchases] uid=${uid} — done. deletedCount=${deletedCount}`);

    return {deletedCount};
  },
);
