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
 */

import {onCall, HttpsError} from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as logger from "firebase-functions/logger";

export const getEntitlements = onCall(
  {region: "asia-northeast3"},
  async (request) => {
    // 46 스킬 B: context.auth.uid 필수
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Authentication required");
    }
    const uid = request.auth.uid;

    logger.info(`[getEntitlements] uid=${uid}`);

    // /users/{uid}/entitlements/current 읽기
    const docRef = admin.firestore()
      .collection("users").doc(uid)
      .collection("entitlements").doc("current");
    const snap = await docRef.get();

    // 46 스킬 B: 스냅샷 키(고정) — 문서 없으면 기본값 반환
    if (!snap.exists) {
      logger.info(`[getEntitlements] uid=${uid} — no entitlements doc, returning defaults`);
      return {
        ownedSeasonPasses: [],
        rentals: {},
        currencyBalances: {},
        serverNowUtcMs: Date.now(),
      };
    }

    const data = snap.data()!;

    return {
      ownedSeasonPasses: data.ownedSeasonPasses ?? [],
      rentals: data.rentals ?? {},
      currencyBalances: data.currencyBalances ?? {},
      serverNowUtcMs: Date.now(),
    };
  },
);
