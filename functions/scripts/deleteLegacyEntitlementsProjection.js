#!/usr/bin/env node

/**
 * Delete legacy server-side entitlement projection docs:
 *   /users/{uid}/entitlements/current
 *
 * Default is dry-run. Use --execute to actually delete.
 *
 * Usage:
 *   npm -C functions run cleanup:legacy-entitlements
 *   npm -C functions run cleanup:legacy-entitlements:run
 */

const admin = require("firebase-admin");
const {FieldPath} = require("firebase-admin/firestore");

const args = new Set(process.argv.slice(2));
const execute = args.has("--execute");

function parseBatchSize() {
  for (const arg of args) {
    if (!arg.startsWith("--batch-size=")) continue;
    const value = Number(arg.slice("--batch-size=".length));
    if (Number.isFinite(value) && value > 0 && value <= 500) {
      return Math.floor(value);
    }
    throw new Error(`Invalid --batch-size value: ${arg}`);
  }
  return 300;
}

function isLegacyUserEntitlementCurrentDoc(docSnap) {
  if (!docSnap || !docSnap.exists) return false;
  if (docSnap.id !== "current") return false;

  const segments = docSnap.ref.path.split("/");
  return (
    segments.length === 4 &&
    segments[0] === "users" &&
    segments[2] === "entitlements" &&
    segments[3] === "current"
  );
}

async function main() {
  const batchSize = parseBatchSize();

  admin.initializeApp();
  const db = admin.firestore();

  let totalScanned = 0;
  let totalMatched = 0;
  let totalDeleted = 0;
  let page = 0;
  let lastDoc = null;

  console.log(
    `[cleanup:legacy-entitlements] mode=${execute ? "EXECUTE" : "DRY_RUN"} batchSize=${batchSize}`,
  );

  while (true) {
    let query = db
      .collectionGroup("entitlements")
      .orderBy(FieldPath.documentId())
      .limit(batchSize);

    if (lastDoc) {
      query = query.startAfter(lastDoc);
    }

    const snap = await query.get();
    if (snap.empty) break;

    page += 1;
    totalScanned += snap.size;

    const matched = snap.docs.filter(isLegacyUserEntitlementCurrentDoc);
    totalMatched += matched.length;

    if (execute && matched.length > 0) {
      const batch = db.batch();
      for (const doc of matched) {
        batch.delete(doc.ref);
      }
      await batch.commit();
      totalDeleted += matched.length;
    }

    const samplePaths = matched.slice(0, 3).map((d) => d.ref.path);
    console.log(
      `[page ${page}] scanned=${snap.size} matched=${matched.length} ` +
      `${execute ? `deleted=${matched.length}` : "deleted=0"} ` +
      (samplePaths.length > 0 ? `sample=${samplePaths.join(", ")}` : ""),
    );

    lastDoc = snap.docs[snap.docs.length - 1];
  }

  console.log(
    `[cleanup:legacy-entitlements] done scanned=${totalScanned} matched=${totalMatched} deleted=${totalDeleted}`,
  );

  if (!execute) {
    console.log(
      "[cleanup:legacy-entitlements] dry-run only. Re-run with --execute to delete docs.",
    );
  }
}

main().catch((err) => {
  console.error("[cleanup:legacy-entitlements] failed", err);
  process.exitCode = 1;
});
