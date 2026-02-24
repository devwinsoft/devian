/**
 * index.ts — Firebase Functions export entry
 *
 * Callable 이름은 46 스킬 B 섹션에서 고정:
 *   - verifyPurchase
 *   - ackPurchaseClientGrant
 *   - ackPurchaseStoreConfirm
 *   - getEntitlements
 *   - getRecentPurchases30d
 *   - getPurchaseAdjustments
 */

import * as admin from "firebase-admin";

// Firebase Admin 초기화 (프로젝트 기본 서비스 계정 사용)
admin.initializeApp();

// ── Purchase Callables ──
export {verifyPurchase} from "./purchase/verifyPurchase";
export {ackPurchaseClientGrant} from "./purchase/ackPurchaseClientGrant";
export {ackPurchaseStoreConfirm} from "./purchase/ackPurchaseStoreConfirm";
export {getEntitlements} from "./purchase/getEntitlements";
export {getRecentPurchases30d} from "./purchase/getRecentPurchases30d";
export {getPurchaseAdjustments} from "./purchase/getPurchaseAdjustments";
export {ackRefundApplied} from "./purchase/ackRefundApplied";
