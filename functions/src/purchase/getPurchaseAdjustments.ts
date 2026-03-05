/**
 * getPurchaseAdjustments.ts — Firebase Callable: 환불/회수 대상 구매 상태 변경 조회
 *
 * 최소 데이터 원칙:
 *   - 서버는 purchase/product 상태 중심 필드만 반환
 *   - reward payload 해석은 클라이언트(PurchaseManager)가 internalProductId로 수행
 *
 * 응답 키:
 *   - items[]: { purchaseId, internalProductId, kind, resultStatus, updatedAtUtcMs, reason? }
 *   - nextCursor: "updatedAtMs|docId" | null
 *   - hasMore: boolean
 *
 * core 로직은 getPurchaseAdjustmentsCore.ts 에 위치 (initSession과 공유).
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import {fetchPurchaseAdjustments} from "./getPurchaseAdjustmentsCore";
import * as logger from "firebase-functions/logger";

export const getPurchaseAdjustments = onCall(
  async (request) => {
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }
    const uid = request.auth.uid;

    const result = await fetchPurchaseAdjustments(
      uid,
      request.data?.pageSize,
      request.data?.cursor,
    );

    logger.info(
      `[getPurchaseAdjustments] uid=${uid} ` +
      `pageSize=${request.data?.pageSize ?? 50} ` +
      `items=${result.items.length} hasMore=${result.hasMore}`,
    );

    return result;
  },
);
