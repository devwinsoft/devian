/**
 * getEntitlements.ts — Firebase Callable: 현재 권한(entitlements) 스냅샷 조회
 *
 * 46 스킬 결정사항 준수:
 *   B. Callable 이름 = "getEntitlements", context.auth.uid 필수
 *   B. 응답 스냅샷 키: ownedSeasonPasses, currencyBalances (+ rentals, serverNowUtcMs)
 *   G. 리전: asia-northeast3
 *
 * NOTE (restore projection):
 *   - rentals(map) / serverNowUtcMs를 함께 반환하여 Rental 만료 복구/남은시간 계산에 사용한다.
 *   - ownedSeasonPasses / rentals 는 클라이언트 local/cloud cache(PurchaseStorage)에 저장될 수 있다.
 *   - noAds 는 게임 로직 전용이므로 서버 entitlements 스냅샷에 포함하지 않는다.
 *
 * core 로직은 getEntitlementsCore.ts 에 위치 (initSession과 공유).
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import {fetchEntitlements} from "./getEntitlementsCore";
import * as logger from "firebase-functions/logger";

export const getEntitlements = onCall(
  async (request) => {
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }
    const uid = request.auth.uid;

    logger.info(`[getEntitlements] uid=${uid}`);

    return await fetchEntitlements(uid);
  },
);
