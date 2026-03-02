/**
 * getMissionClock.ts — Firebase Callable: 서버 기준 현재 시각 조회
 *
 * Mission SSOT:
 *   - Callable 이름 = "getMissionClock"
 *   - 빈 payload 허용
 *   - 응답 = { serverNowUtcMs }
 *   - mission 상태/진행도/claim 기록은 서버가 관리하지 않음
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as logger from "firebase-functions/logger";

export const getMissionClock = onCall(
  async (request) => {
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }

    const uid = request.auth.uid;
    const serverNowUtcMs = Date.now();

    logger.info(`[getMissionClock] uid=${uid} serverNowUtcMs=${serverNowUtcMs}`);

    return {
      serverNowUtcMs,
    };
  },
);
