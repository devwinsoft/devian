/**
 * ackPurchaseStoreConfirm.ts — Firebase Callable: 스토어 ConfirmPurchase 완료 ACK
 *
 * 목적:
 *   - 서버에서 verify/clientGrant 상태와 스토어 confirm 상태를 분리 추적
 *   - purchases/{purchaseId}.storeConfirmStatus 를 CONFIRMED 로 업데이트
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

export const ackPurchaseStoreConfirm = onCall(
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
      const verifyStatus = getVerifyStatus(data);
      if (verifyStatus !== "GRANTED") {
        throw new HttpsError("failed-precondition", `purchase verifyStatus must be GRANTED (actual=${verifyStatus})`);
      }

      // storeConfirmStatus is tracked independently from clientGrantStatus.
      // Confirm ACK must be allowed before local grant/client ACK to avoid consumables/rentals
      // getting stuck as "already owned" when failures happen after store purchase succeeds.
      const clientGrantStatus = String(data.clientGrantStatus ?? "PENDING");

      const currentStoreConfirmStatus = String(data.storeConfirmStatus ?? "");
      if (currentStoreConfirmStatus !== "CONFIRMED") {
        tx.set(ref, {
          verifyStatus: "GRANTED",
          status: "GRANTED", // legacy compatibility (temporary)
          storeConfirmStatus: "CONFIRMED",
          storeConfirmedAt: admin.firestore.FieldValue.serverTimestamp(),
        }, {merge: true});
      }

      return {
        purchaseId,
        verifyStatus: "GRANTED",
        clientGrantStatus,
        storeConfirmStatus: "CONFIRMED",
      };
    });

    logger.info(`[ackPurchaseStoreConfirm] uid=${uid} purchaseId=${purchaseId} confirm=${result.storeConfirmStatus}`);
    return result;
  },
);
