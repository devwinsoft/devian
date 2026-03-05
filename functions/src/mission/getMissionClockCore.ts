/**
 * getMissionClockCore.ts — getMissionClock core 로직
 *
 * getMissionClock callable과 initSession callable이 공유한다.
 */

import {getFirestore} from "firebase-admin/firestore";

export interface MissionClockData {
  serverNowUtcMs: number;
  minVersion: string;
  currentVersion: string;
}

export async function fetchMissionClock(): Promise<MissionClockData> {
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
