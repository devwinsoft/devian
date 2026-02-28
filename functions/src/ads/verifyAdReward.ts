/**
 * verifyAdReward.ts — Firebase HTTP Function: AdMob Rewarded SSV 콜백 검증
 *
 * 41 스킬 결정사항 준수:
 *   B. 엔드포인트 이름 = "verifyAdReward", HTTP GET (onRequest)
 *   C. custom_data 포맷: "{uid}:{advertiseId}:{rewardGroupId}"
 *   D. Firestore path: /users/{uid}/adRewards/{transactionId}
 *   E. 공개키: https://gstatic.com/admob/reward/verifier-keys.json (캐시 24h)
 *   F. Node.js crypto 직접 ECDSA 검증 (외부 npm 없음)
 *   G. 클라이언트 즉시 지급 + 서버 감사 로그 용도
 *
 * 40 스킬 구현 정본:
 *   A. HTTP function (onRequest), AdMob이 GET 요청
 *   B. ECDSA with SHA-256 서명 검증
 *   C. Firestore 감사 로그 기록
 *   D. custom_data 파싱
 *   E. transaction_id 기반 멱등 처리
 *   F. 에러 시에도 HTTP 200 (서명/파싱 실패), Firestore 실패만 500
 */

import {onRequest} from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as logger from "firebase-functions/logger";
import * as crypto from "crypto";
import * as https from "https";

// ────────────────────────────────────────────
// 공개키 캐시 (24시간)
// ────────────────────────────────────────────

const KEYS_URL = "https://gstatic.com/admob/reward/verifier-keys.json";
const CACHE_TTL_MS = 24 * 60 * 60 * 1000;

interface AdMobKey {
  keyId: number;
  pem: string;
  base64: string;
}

let cachedKeys: AdMobKey[] = [];
let cacheExpiresAt = 0;

async function fetchPublicKeys(): Promise<AdMobKey[]> {
  const now = Date.now();
  if (cachedKeys.length > 0 && now < cacheExpiresAt) {
    return cachedKeys;
  }

  const json = await new Promise<string>((resolve, reject) => {
    https.get(KEYS_URL, (res) => {
      let data = "";
      res.on("data", (chunk) => { data += chunk; });
      res.on("end", () => resolve(data));
      res.on("error", reject);
    }).on("error", reject);
  });

  const parsed = JSON.parse(json) as { keys: AdMobKey[] };
  cachedKeys = parsed.keys;
  cacheExpiresAt = now + CACHE_TTL_MS;
  logger.info("[verifyAdReward] Public keys refreshed", {count: cachedKeys.length});
  return cachedKeys;
}

// ────────────────────────────────────────────
// ECDSA 서명 검증
// ────────────────────────────────────────────

function verifySignature(
  queryString: string,
  signature: string,
  pem: string,
): boolean {
  const verifier = crypto.createVerify("SHA256");
  verifier.update(queryString);
  return verifier.verify(pem, signature, "base64");
}

/**
 * 쿼리 파라미터에서 signature/key_id를 제외하고
 * 알파벳 순으로 정렬하여 "&"로 연결한 문자열을 생성한다.
 */
function buildMessageToVerify(query: Record<string, string>): string {
  const filtered = Object.entries(query)
    .filter(([key]) => key !== "signature" && key !== "key_id")
    .sort(([a], [b]) => a.localeCompare(b));

  return filtered.map(([k, v]) => `${k}=${v}`).join("&");
}

// ────────────────────────────────────────────
// custom_data 파싱
// ────────────────────────────────────────────

interface CustomData {
  uid: string;
  advertiseId: string;
  rewardGroupId: string;
}

function parseCustomData(raw: string | undefined): CustomData | null {
  if (!raw) return null;
  const parts = raw.split(":");
  if (parts.length < 3) return null;
  const [uid, advertiseId, ...rest] = parts;
  if (!uid || !advertiseId) return null;
  return {uid, advertiseId, rewardGroupId: rest.join(":")};
}

// ────────────────────────────────────────────
// Firestore 참조
// ────────────────────────────────────────────

function adRewardDocRef(uid: string, transactionId: string) {
  return admin.firestore()
    .collection("users").doc(uid)
    .collection("adRewards").doc(transactionId);
}

// ────────────────────────────────────────────
// HTTP Endpoint
// ────────────────────────────────────────────

export const verifyAdReward = onRequest(
  {region: "asia-northeast3"},
  async (req, res) => {
    // GET만 허용
    if (req.method !== "GET") {
      res.status(200).send("OK");
      return;
    }

    const query = req.query as Record<string, string>;
    const signature = query.signature;
    const keyId = query.key_id;
    const transactionId = query.transaction_id;
    const customDataRaw = query.custom_data;

    // ── 필수 파라미터 확인 ──
    if (!signature || !keyId || !transactionId) {
      logger.warn("[verifyAdReward] Missing required params", {
        hasSignature: !!signature,
        hasKeyId: !!keyId,
        hasTransactionId: !!transactionId,
      });
      res.status(200).send("OK");
      return;
    }

    // ── custom_data 파싱 ──
    const customData = parseCustomData(customDataRaw);
    if (!customData) {
      logger.warn("[verifyAdReward] custom_data parse failed", {customDataRaw});
      res.status(200).send("OK");
      return;
    }

    // ── 서명 검증 ──
    let verified = false;
    try {
      const keys = await fetchPublicKeys();
      const key = keys.find((k) => String(k.keyId) === keyId);
      if (!key) {
        logger.warn("[verifyAdReward] Unknown key_id", {keyId});
        res.status(200).send("OK");
        return;
      }

      const message = buildMessageToVerify(query);
      verified = verifySignature(message, signature, key.pem);

      if (!verified) {
        logger.warn("[verifyAdReward] Signature verification failed", {
          transactionId,
          keyId,
        });
        res.status(200).send("OK");
        return;
      }
    } catch (err) {
      logger.error("[verifyAdReward] Verification error", {err});
      // 공개키 조회 실패 → 500 (재시도 허용)
      res.status(500).send("Internal Error");
      return;
    }

    // ── 멱등 처리 + Firestore 감사 로그 기록 ──
    try {
      const docRef = adRewardDocRef(customData.uid, transactionId);
      const existing = await docRef.get();

      if (existing.exists) {
        // 이미 기록됨 → 중복 기록 방지
        logger.info("[verifyAdReward] Duplicate callback, skipping", {transactionId});
        res.status(200).send("OK");
        return;
      }

      await docRef.set({
        transactionId,
        adUnit: query.ad_unit || "",
        adNetwork: query.ad_network || "",
        rewardAmount: Number(query.reward_amount) || 0,
        rewardItem: query.reward_item || "",
        customData: customDataRaw || "",
        advertiseId: customData.advertiseId,
        rewardGroupId: customData.rewardGroupId,
        verified,
        timestamp: Number(query.timestamp) || 0,
        verifiedAt: admin.firestore.FieldValue.serverTimestamp(),
      });

      logger.info("[verifyAdReward] Reward logged", {
        transactionId,
        uid: customData.uid,
        advertiseId: customData.advertiseId,
        rewardGroupId: customData.rewardGroupId,
      });

      res.status(200).send("OK");
    } catch (err) {
      logger.error("[verifyAdReward] Firestore write error", {err, transactionId});
      // Firestore 실패 → 500 (재시도 허용)
      res.status(500).send("Internal Error");
    }
  },
);
