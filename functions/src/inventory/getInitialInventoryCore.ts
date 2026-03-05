/**
 * getInitialInventoryCore.ts — getInitialInventory core 로직
 *
 * 사용자별 초기 지급은 서버에서 1회만 허용한다.
 * marker 문서가 이미 존재하면 빈 rewards를 반환한다.
 */

import * as admin from "firebase-admin";

const INITIAL_INVENTORY_CONFIG_PATH = "config/initialInventory";
const ALLOWED_REWARD_TYPES = new Set([
  "CARD",
  "CURRENCY",
  "EQUIP",
  "HERO",
  "RENTAL",
  "SEASON_PASS",
]);

export class InitialInventoryConfigError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "InitialInventoryConfigError";
  }
}

export interface InitialInventoryRewardData {
  type: string;
  id: string;
  amount: number;
}

export interface InitialInventoryData {
  rewards: InitialInventoryRewardData[];
}

function normalizeReward(raw: unknown): InitialInventoryRewardData | null {
  if (raw == null || typeof raw !== "object") return null;

  const obj = raw as Record<string, unknown>;
  const type = String(obj.type ?? "").trim().toUpperCase();
  const id = String(obj.id ?? "").trim();
  const amountRaw = Number(obj.amount);

  if (!type || !id || !Number.isInteger(amountRaw) || amountRaw <= 0) {
    return null;
  }

  if (!ALLOWED_REWARD_TYPES.has(type)) return null;

  return {type, id, amount: amountRaw};
}

function normalizeRewards(rawRewards: unknown): InitialInventoryRewardData[] {
  if (rawRewards == null) return [];
  if (!Array.isArray(rawRewards)) {
    throw new InitialInventoryConfigError("config/initialInventory.rewards must be an array");
  }

  const rewards: InitialInventoryRewardData[] = [];
  for (let i = 0; i < rawRewards.length; i++) {
    const normalized = normalizeReward(rawRewards[i]);
    if (normalized == null) {
      throw new InitialInventoryConfigError(
        `config/initialInventory.rewards[${i}] is invalid. ` +
        "Expected { type, id, amount } with allowed type and positive integer amount.",
      );
    }
    rewards.push(normalized);
  }
  return rewards;
}

export async function fetchAndMarkInitialInventory(uid: string): Promise<InitialInventoryData> {
  const db = admin.firestore();
  const configRef = db.doc(INITIAL_INVENTORY_CONFIG_PATH);
  const markerRef = db.collection("users").doc(uid).collection("meta").doc("initialInventory");

  const rewards = await db.runTransaction(async (tx) => {
    const markerSnap = await tx.get(markerRef);
    if (markerSnap.exists) {
      return [] as InitialInventoryRewardData[];
    }

    const configSnap = await tx.get(configRef);
    const configData = configSnap.exists ? configSnap.data() : undefined;
    const normalizedRewards = normalizeRewards(configData?.rewards);

    tx.set(markerRef, {
      grantedAtUtcMs: Date.now(),
      rewardCount: normalizedRewards.length,
      configPath: INITIAL_INVENTORY_CONFIG_PATH,
    });

    return normalizedRewards;
  });

  return {rewards};
}
