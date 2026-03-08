/**
 * getRemoteConfigCore.ts — getRemoteConfig core 로직
 *
 * getRemoteConfig callable과 initSession callable이 공유한다.
 */

import {getFirestore} from "firebase-admin/firestore";

export interface RemoteConfigData {
  serverNowUtcMs: number;
  minVersion: string;
  currentVersion: string;
}

export async function fetchRemoteConfig(): Promise<RemoteConfigData> {
  const serverNowUtcMs = Date.now();
  const db = getFirestore();
  const configDoc = await db.doc("config/appVersion").get();
  const configData = configDoc.exists ? configDoc.data() : undefined;

  return {
    serverNowUtcMs,
    minVersion: configData?.minVersion ?? "",
    currentVersion: configData?.currentVersion ?? "",
  };
}
