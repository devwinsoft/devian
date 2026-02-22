/**
 * ackPurchaseClientGrant.ts — Firebase Callable: 클라이언트 로컬 지급 결과 보고(성공/실패)
 *
 * 목적:
 *   - verifyPurchase(서버 검증 성공)와 클라이언트 로컬 지급 결과를 분리 추적
 *   - purchases/{purchaseId}.clientGrantStatus 를 APPLIED_ACKED / FAILED_REPORTED 로 업데이트
 *
 * 참고:
 *   - 이름은 기존 호출자 호환을 위해 유지한다.
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as logger from "firebase-functions/logger";

function purchaseDocRef(uid: string, purchaseId: string) {
  return admin.firestore()
    .collection("users").doc(uid)
    .collection("purchases").doc(purchaseId);
}

function getVerifyStatus(data: FirebaseFirestore.DocumentData | undefined): string {
  return String(data?.verifyStatus ?? data?.status ?? "");
}

export const ackPurchaseClientGrant = onCall(
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

    const requestedClientGrantStatusRaw = String(request.data?.clientGrantStatus ?? "APPLIED_ACKED");
    const requestedClientGrantStatus =
      requestedClientGrantStatusRaw === "FAILED_REPORTED" ? "FAILED_REPORTED" :
      requestedClientGrantStatusRaw === "APPLIED_ACKED" ? "APPLIED_ACKED" :
      null;
    if (!requestedClientGrantStatus) {
      throw new HttpsError("invalid-argument", "clientGrantStatus must be APPLIED_ACKED or FAILED_REPORTED");
    }

    const ref = purchaseDocRef(uid, purchaseId);

    const result = await admin.firestore().runTransaction(async (tx) => {
      const snap = await tx.get(ref);
      if (!snap.exists) {
        throw new HttpsError("not-found", "purchase not found");
      }

      const data = snap.data()!;
      const verifyStatus = getVerifyStatus(data);
      if (verifyStatus !== "GRANTED") {
        throw new HttpsError("failed-precondition", `purchase verifyStatus must be GRANTED (actual=${verifyStatus})`);
      }

      const currentClientGrantStatus = String(data.clientGrantStatus ?? "");
      if (currentClientGrantStatus !== requestedClientGrantStatus) {
        tx.set(ref, {
          verifyStatus: "GRANTED",
          status: "GRANTED", // legacy compatibility (temporary)
          clientGrantStatus: requestedClientGrantStatus,
          clientGrantReportedAt: admin.firestore.FieldValue.serverTimestamp(),
        }, {merge: true});
      }

      return {
        purchaseId,
        verifyStatus: "GRANTED",
        clientGrantStatus: requestedClientGrantStatus,
      };
    });

    logger.info(`[ackPurchaseClientGrant] uid=${uid} purchaseId=${purchaseId} clientGrant=${result.clientGrantStatus}`);
    return result;
  },
);
