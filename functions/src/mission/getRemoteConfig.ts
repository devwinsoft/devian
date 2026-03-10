/**
 * getRemoteConfig.ts — Firebase Callable: 서버 기준 현재 시각 + 앱 버전 정보 조회
 *
 * RemoteConfig SSOT:
 *   - Callable 이름 = "getRemoteConfig"
 *   - 빈 payload 허용
 *   - 응답 = { serverNowUtcMs, minVersion?, currentVersion? }
 *   - 버전 정보는 Firestore /config/appVersion 문서에서 읽음
 *
 * core 로직은 getRemoteConfigCore.ts 에 위치 (initSession과 공유).
 */

import {onCall} from "firebase-functions/v2/https";
import {fetchRemoteConfig} from "./getRemoteConfigCore";
import * as logger from "firebase-functions/logger";

export const getRemoteConfig = onCall(
  async (request) => {
    const uid = request.auth?.uid ?? "unauthenticated";
    const result = await fetchRemoteConfig();

    logger.info(`[getRemoteConfig] uid=${uid} serverNowUtcMs=${result.serverNowUtcMs}`);

    return result;
  },
);
