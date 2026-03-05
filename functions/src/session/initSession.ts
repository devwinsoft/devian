/**
 * initSession.ts — Firebase Callable: 로그인 시 초기 데이터 일괄 조회
 *
 * getMissionClock + getEntitlements + getPurchaseAdjustments(첫 페이지)를
 * 한 번의 호출로 병렬 실행하여 클라이언트 네트워크 왕복을 줄인다.
 * 초기 인벤토리 지급은 포함하지 않는다 (getInitialInventory 별도 callable).
 *
 * core 로직은 각 모듈의 *Core.ts에 위치하며 개별 callable과 공유한다.
 * 코드 중복 없음.
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import {fetchMissionClock} from "../mission/getMissionClockCore";
import {fetchEntitlements} from "../purchase/getEntitlementsCore";
import {fetchPurchaseAdjustments} from "../purchase/getPurchaseAdjustmentsCore";
import * as logger from "firebase-functions/logger";

export const initSession = onCall(
  async (request) => {
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }
    const uid = request.auth.uid;

    const adjustmentPageSize = Math.min(
      Math.max(Number(request.data?.adjustmentPageSize) || 50, 1),
      200,
    );

    // 3개 Firestore 읽기를 병렬 실행
    const [missionClock, entitlements, purchaseAdjustments] = await Promise.all([
      fetchMissionClock(),
      fetchEntitlements(uid),
      fetchPurchaseAdjustments(uid, adjustmentPageSize),
    ]);

    logger.info(
      `[initSession] uid=${uid} ` +
      `adjustments=${purchaseAdjustments.items.length} ` +
      `hasMore=${purchaseAdjustments.hasMore}`,
    );

    return {missionClock, entitlements, purchaseAdjustments};
  },
);
