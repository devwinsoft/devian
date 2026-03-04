/**
 * getMissionClock.ts — Firebase Callable: 서버 기준 현재 시각 + 앱 버전 정보 조회
 *
 * Mission SSOT:
 *   - Callable 이름 = "getMissionClock"
 *   - 빈 payload 허용
 *   - 응답 = { serverNowUtcMs, minVersion?, currentVersion? }
 *   - mission 상태/진행도/claim 기록은 서버가 관리하지 않음
 *   - 버전 정보는 Firestore /config/appVersion 문서에서 읽음
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import {getFirestore} from "firebase-admin/firestore";
import * as logger from "firebase-functions/logger";

export const getMissionClock = onCall(
  async (request) => {
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }

    const uid = request.auth.uid;
    const serverNowUtcMs = Date.now();

    // Read app version config from Firestore
    const db = getFirestore();
    const configDoc = await db.doc("config/appVersion").get();
    const configData = configDoc.exists ? configDoc.data() : undefined;

    const minVersion = configData?.minVersion ?? "";
    const currentVersion = configData?.currentVersion ?? "";

    logger.info(`[getMissionClock] uid=${uid} serverNowUtcMs=${serverNowUtcMs}`);

    return {
      serverNowUtcMs,
      minVersion,
      currentVersion,
    };
  },
);
