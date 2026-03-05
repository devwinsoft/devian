/**
 * getInitialInventory.ts — Firebase Callable: 사용자 초기 인벤토리 1회 지급 데이터 조회
 *
 * 로그인 initSession 이후 SyncState.Initial 경로에서 별도 호출된다.
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as logger from "firebase-functions/logger";
import {
  fetchAndMarkInitialInventory,
  InitialInventoryConfigError,
} from "./getInitialInventoryCore";

export const getInitialInventory = onCall(
  async (request) => {
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }

    const uid = request.auth.uid;
    let result;
    try {
      result = await fetchAndMarkInitialInventory(uid);
    } catch (error) {
      if (error instanceof InitialInventoryConfigError) {
        throw new HttpsError("failed-precondition", error.message);
      }
      throw error;
    }

    logger.info(
      `[getInitialInventory] uid=${uid} rewards=${result.rewards.length}`,
    );

    return result;
  },
);
