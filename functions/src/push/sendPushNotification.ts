/**
 * sendPushNotification.ts — Firebase Callable: FCM 토픽 기반 푸시 알림 발송
 *
 * Operation 웹앱에서 호출. PUSH_REMOTE 테이블의 PushId를 FCM 토픽으로 사용하여
 * 언어별 body를 각각 발송한다.
 *
 * 입력: { entries: [{ pushId: string, body: string }] }
 * 출력: { results: [{ pushId: string, success: boolean, error?: string }] }
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as logger from "firebase-functions/logger";

interface SendEntry {
  pushId: string;
  body: string;
}

interface SendResult {
  pushId: string;
  success: boolean;
  error?: string;
}

export const sendPushNotification = onCall(async (request) => {
  // Auth 검증
  if (!request.auth) {
    throw new HttpsError(
      "unauthenticated",
      "Authentication required."
    );
  }

  const {entries} = request.data as { entries?: SendEntry[] };

  if (!entries || !Array.isArray(entries) || entries.length === 0) {
    throw new HttpsError(
      "invalid-argument",
      "entries array is required and must not be empty."
    );
  }

  // 최대 발송 수 제한 (안전 가드)
  if (entries.length > 50) {
    throw new HttpsError(
      "invalid-argument",
      "Maximum 50 entries per request."
    );
  }

  const results: SendResult[] = [];

  for (const entry of entries) {
    if (!entry.pushId || !entry.body) {
      results.push({
        pushId: entry.pushId || "(empty)",
        success: false,
        error: "pushId and body are required.",
      });
      continue;
    }

    try {
      await admin.messaging().send({
        topic: entry.pushId,
        notification: {
          body: entry.body,
        },
      });

      logger.info(`FCM sent: topic=${entry.pushId}`, {
        uid: request.auth.uid,
        pushId: entry.pushId,
      });

      results.push({pushId: entry.pushId, success: true});
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      logger.error(`FCM send failed: topic=${entry.pushId}`, {
        uid: request.auth.uid,
        pushId: entry.pushId,
        error: errorMsg,
      });

      results.push({
        pushId: entry.pushId,
        success: false,
        error: errorMsg,
      });
    }
  }

  return {results};
});
