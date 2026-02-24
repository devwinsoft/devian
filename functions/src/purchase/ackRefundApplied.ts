/**
 * ackRefundApplied.ts — Firebase Callable: 환불/회수 클라이언트 적용 완료 ACK
 *
 * 목적:
 *   - 클라이언트가 RevokeRewards를 성공적으로 실행한 후 서버에 ACK
 *   - purchases/{purchaseId}.clientRefundApplied = true 로 마킹
 *   - getPurchaseAdjustments가 ACK 완료 아이템을 제외하여 중복 처리 방지
 *
 * 참고:
 *   - ackPurchaseClientGrant와 동일 패턴 (단건 ACK, 트랜잭션 기반)
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as logger from "firebase-functions/logger";

const REFUND_STATUSES = ["REFUNDED", "REVOKED"];

function purchaseDocRef(uid: string, purchaseId: string) {
  return admin.firestore()
    .collection("users").doc(uid)
    .collection("purchases").doc(purchaseId);
}

export const ackRefundApplied = onCall(
  {region: "asia-northeast3"},
  async (request) => {
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }

    const uid = request.auth.uid;
    const purchaseId = String(request.data?.purchaseId ?? "");
    if (!purchaseId) {
      throw new HttpsError("invalid-argument", "purchaseId is required");
    }

    const ref = purchaseDocRef(uid, purchaseId);

    const result = await admin.firestore().runTransaction(async (tx) => {
      const snap = await tx.get(ref);
      if (!snap.exists) {
        throw new HttpsError("not-found", "purchase not found");
      }

      const data = snap.data()!;
      const verifyStatus = String(data.verifyStatus ?? data.status ?? "");
      if (!REFUND_STATUSES.includes(verifyStatus)) {
        throw new HttpsError(
          "failed-precondition",
          `purchase verifyStatus must be REFUNDED or REVOKED (actual=${verifyStatus})`,
        );
      }

      const alreadyApplied = data.clientRefundApplied === true;
      if (!alreadyApplied) {
        tx.set(ref, {
          clientRefundApplied: true,
          clientRefundAppliedAt: admin.firestore.FieldValue.serverTimestamp(),
        }, {merge: true});
      }

      return {
        purchaseId,
        verifyStatus,
        clientRefundApplied: true,
      };
    });

    logger.info(`[ackRefundApplied] uid=${uid} purchaseId=${purchaseId} verifyStatus=${result.verifyStatus}`);
    return result;
  },
);
