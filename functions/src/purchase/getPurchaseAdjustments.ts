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
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as logger from "firebase-functions/logger";

const DEFAULT_PAGE_SIZE = 50;
const MAX_PAGE_SIZE = 200;
const ADJUSTMENT_STATUSES = ["REFUNDED", "REVOKED"];
const MAX_FILL_ROUNDS = 5;

function toMillisOrZero(value: any): number {
  if (value == null) return 0;
  if (typeof value?.toMillis === "function") {
    const ms = Number(value.toMillis());
    return Number.isFinite(ms) && ms > 0 ? Math.floor(ms) : 0;
  }
  if (typeof value === "number") {
    return Number.isFinite(value) && value > 0 ? Math.floor(value) : 0;
  }
  if (typeof value === "string") {
    const n = Number(value);
    return Number.isFinite(n) && n > 0 ? Math.floor(n) : 0;
  }
  return 0;
}

function docToItem(doc: FirebaseFirestore.QueryDocumentSnapshot) {
  const d = doc.data();
  const verifyStatus = String(d.verifyStatus ?? d.status ?? "");
  const updatedAtMs = toMillisOrZero(d.updatedAt) || toMillisOrZero(d.lastUpdatedAt);
  if (updatedAtMs <= 0) {
    throw new HttpsError(
      "failed-precondition",
      `purchase adjustment document is missing valid updatedAt: ${doc.ref.path}`,
    );
  }

  return {
    purchaseId: String(d.purchaseId ?? doc.id),
    internalProductId: String(d.internalProductId ?? ""),
    kind: String(d.kind ?? ""),
    resultStatus: verifyStatus,
    updatedAtUtcMs: updatedAtMs,
    reason: String(d.rejectReason ?? d.reason ?? ""),
  };
}

export const getPurchaseAdjustments = onCall(
  async (request) => {
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }
    const uid = request.auth.uid;

    const pageSize = Math.min(
      Math.max(Number(request.data?.pageSize) || DEFAULT_PAGE_SIZE, 1),
      MAX_PAGE_SIZE,
    );

    const baseCollection = admin.firestore()
      .collection("users").doc(uid)
      .collection("purchases");

    // Parse initial cursor if provided
    let cursorStartAfter: [FirebaseFirestore.Timestamp, string] | null = null;
    const nextCursorRaw = request.data?.cursor;
    if (nextCursorRaw != null && nextCursorRaw !== "") {
      const token = String(nextCursorRaw);
      const parts = token.split("|");
      if (parts.length !== 2) {
        throw new HttpsError("invalid-argument", "cursor must be 'updatedAtMs|docId'");
      }

      const cursorMs = Number(parts[0]);
      const cursorId = parts[1];
      if (!(cursorMs > 0) || !cursorId) {
        throw new HttpsError("invalid-argument", "cursor must be 'updatedAtMs|docId'");
      }

      cursorStartAfter = [admin.firestore.Timestamp.fromMillis(cursorMs), cursorId];
    }

    // Fill pending items across multiple query rounds to avoid empty pages
    // caused by post-query clientRefundApplied filter.
    const pendingDocs: FirebaseFirestore.QueryDocumentSnapshot[] = [];
    let lastDoc: FirebaseFirestore.QueryDocumentSnapshot | null = null;
    let hasMore = false;

    for (let round = 0; round < MAX_FILL_ROUNDS; round++) {
      let query = baseCollection
        .where("verifyStatus", "in", ADJUSTMENT_STATUSES)
        .orderBy("updatedAt", "desc")
        .orderBy(admin.firestore.FieldPath.documentId(), "desc")
        .limit(pageSize + 1);

      if (lastDoc) {
        query = query.startAfter(lastDoc);
      } else if (cursorStartAfter) {
        query = query.startAfter(cursorStartAfter[0], cursorStartAfter[1]);
      }

      const snapshot = await query.get();
      const docs = snapshot.docs;
      const roundHasMore = docs.length > pageSize;
      const pageDocs = docs.slice(0, pageSize);

      if (pageDocs.length === 0) {
        hasMore = false;
        break;
      }

      lastDoc = pageDocs[pageDocs.length - 1];

      const roundPending = pageDocs.filter((doc) => {
        const d = doc.data();
        return d.clientRefundApplied !== true;
      });
      pendingDocs.push(...roundPending);

      if (pendingDocs.length >= pageSize) {
        // Filled enough — trim to pageSize
        pendingDocs.length = pageSize;
        hasMore = true;
        break;
      }

      if (!roundHasMore) {
        // No more documents in Firestore
        hasMore = false;
        break;
      }

      // More documents exist but we haven't filled pageSize yet — continue
      hasMore = roundHasMore;
    }

    const items = pendingDocs.map(docToItem);

    // Build cursor from the last Firestore document actually read (not the last pending doc)
    // so that the next page starts after the correct position.
    const nextCursor = hasMore && lastDoc
      ? (() => {
          const d = lastDoc!.data();
          const ms = toMillisOrZero(d.updatedAt) || toMillisOrZero(d.lastUpdatedAt);
          if (!(ms > 0)) {
            throw new HttpsError(
              "failed-precondition",
              `cannot build refund cursor without updatedAt: ${lastDoc!.ref.path}`,
            );
          }
          return `${ms}|${lastDoc!.id}`;
        })()
      : null;

    logger.info(`[getPurchaseAdjustments] uid=${uid} pageSize=${pageSize} items=${items.length} hasMore=${hasMore}`);

    return {
      items,
      nextCursor,
      hasMore,
    };
  },
);
