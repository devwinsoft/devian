/**
 * getEntitlementsCore.ts — getEntitlements core 로직
 */

import * as admin from "firebase-admin";

export interface EntitlementsData {
  ownedSeasonPasses: string[];
  rentals: Record<string, number>;
  currencyBalances: Record<string, unknown>;
  serverNowUtcMs: number;
}

export async function fetchEntitlements(uid: string): Promise<EntitlementsData> {
  const docRef = admin.firestore()
    .collection("users").doc(uid)
    .collection("entitlements").doc("current");
  const snap = await docRef.get();

  const serverNowUtcMs = Date.now();

  if (!snap.exists) {
    return {
      ownedSeasonPasses: [],
      rentals: {},
      currencyBalances: {},
      serverNowUtcMs,
    };
  }

  const data = snap.data()!;

  return {
    ownedSeasonPasses: data.ownedSeasonPasses ?? [],
    rentals: data.rentals ?? {},
    currencyBalances: data.currencyBalances ?? {},
    serverNowUtcMs,
  };
}
