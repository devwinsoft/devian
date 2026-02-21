/**
 * getRecentPurchases30d.ts — Firebase Callable: 최근 30일 구매 내역 조회
 *
 * 46 스킬 F2 결정사항 준수:
 *   - 기준 시각: storePurchasedAt (영수증 날짜) — 클라이언트/디바이스 시간 사용 금지
 *   - threshold: 서버 now − 30일
 *   - kind: 클라이언트 파라미터 (필수, "Consumable" | "Subscription" | "SeasonPass")
 *   - Callable 이름: getRecentPurchases30d
 *   - 리전: asia-northeast3
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as logger from "firebase-functions/logger";

const DEFAULT_PAGE_SIZE = 20;
const THIRTY_DAYS_MS = 30 * 24 * 60 * 60 * 1000;
const VALID_KINDS = ["Consumable", "Subscription", "SeasonPass"];

export const getRecentPurchases30d = onCall(
  {region: "asia-northeast3"},
  async (request) => {
    // 인증 필수
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }
    const uid = request.auth.uid;

    // kind (필수)
    const kind = request.data?.kind;
    if (!kind || !VALID_KINDS.includes(kind)) {
      throw new HttpsError("invalid-argument", `kind must be one of: ${VALID_KINDS.join(", ")}`);
    }

    // pageSize (기본 20)
    const pageSize = Math.min(
      Math.max(Number(request.data?.pageSize) || DEFAULT_PAGE_SIZE, 1),
      100,
    );

    // 서버 기준 threshold (now − 30일) — 클라/기기 시간 사용 금지
    const nowMs = admin.firestore.Timestamp.now().toMillis();
    const thresholdMs = nowMs - THIRTY_DAYS_MS;
    const threshold = admin.firestore.Timestamp.fromMillis(thresholdMs);

    logger.info(`[getRecentPurchases30d] uid=${uid} kind=${kind} threshold=${threshold.toDate().toISOString()} pageSize=${pageSize}`);

    // Firestore 쿼리:
    //   where(kind == <kind>)
    //   where(storePurchasedAt >= threshold)
    //   orderBy(storePurchasedAt desc)
    //   limit(pageSize + 1) — 다음 페이지 존재 여부 확인용
    let query = admin.firestore()
      .collection("users").doc(uid)
      .collection("purchases")
      .where("kind", "==", kind)
      .where("storePurchasedAt", ">=", threshold)
      .orderBy("storePurchasedAt", "desc")
      .orderBy(admin.firestore.FieldPath.documentId(), "desc")
      .limit(pageSize + 1);

    // 페이지네이션: nextCursor 토큰 "storePurchasedAtMs|docId" (startAfter 2인자)
    if (request.data?.nextCursor) {
      const token = String(request.data.nextCursor);
      const parts = token.split("|");
      if (parts.length === 2) {
        const cursorMs = Number(parts[0]);
        const cursorId = parts[1];
        if (cursorMs > 0 && cursorId) {
          const cursorTs = admin.firestore.Timestamp.fromMillis(cursorMs);
          query = query.startAfter(cursorTs, cursorId);
        }
      }
    }

    const snapshot = await query.get();

    // 최소 필드만 반환 (purchaseId, internalProductId, storePurchasedAt, status)
    const docs = snapshot.docs;
    const hasMore = docs.length > pageSize;
    const items = docs.slice(0, pageSize).map((doc) => {
      const d = doc.data();
      return {
        purchaseId: d.purchaseId ?? doc.id,
        internalProductId: d.internalProductId ?? "",
        storePurchasedAt: d.storePurchasedAt?.toMillis() ?? 0,
        status: d.status ?? "",
      };
    });

    // nextCursor: 마지막 항목의 "storePurchasedAtMs|docId" 토큰
    const nextCursor = hasMore && docs.length > 0
      ? (() => {
          const lastDoc = docs[Math.min(pageSize, docs.length) - 1];
          const last = lastDoc.data();
          const ms = last.storePurchasedAt?.toMillis?.() ?? 0;
          return ms > 0 ? `${ms}|${lastDoc.id}` : null;
        })()
      : null;

    return {
      items,
      nextCursor,
    };
  },
);
