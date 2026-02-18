/**
 * getEntitlements.ts — Firebase Callable: 현재 권한(entitlements) 스냅샷 조회
 *
 * 46 스킬 결정사항 준수:
 *   B. Callable 이름 = "getEntitlements", context.auth.uid 필수
 *   B. 응답 스냅샷 키: noAdsActive, ownedSeasonPasses, currencyBalances
 *   G. 리전: asia-northeast3
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
        noAdsActive: false,
        ownedSeasonPasses: [],
        currencyBalances: {},
      };
    }

    const data = snap.data()!;

    return {
      noAdsActive: data.noAdsActive ?? false,
      ownedSeasonPasses: data.ownedSeasonPasses ?? [],
      currencyBalances: data.currencyBalances ?? {},
    };
  },
);
