/**
 * index.ts — Firebase Functions export entry
 *
 * Callable 이름은 46 스킬 B 섹션에서 고정:
 *   - verifyPurchase
 *   - ackPurchaseClientGrant
 *   - ackPurchaseStoreConfirm
 *   - getEntitlements
 *   - getMissionClock
 *   - getRecentPurchases30d
 *   - getPurchaseAdjustments
 *   - initSession
 */

import * as admin from "firebase-admin";
import {setGlobalOptions} from "firebase-functions/v2";

// 전역 옵션 (모든 함수에 적용)
setGlobalOptions({region: "asia-northeast3"});

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

// ── Mission Callables ──
export {getMissionClock} from "./mission/getMissionClock";

// ── Session ──
export {initSession} from "./session/initSession";

// ── Google Play RTDN (Pub/Sub) ──
export {handleGooglePlayNotification} from "./purchase/handleGooglePlayNotification";

// ── Ads SSV ──
export {verifyAdReward} from "./ads/verifyAdReward";
