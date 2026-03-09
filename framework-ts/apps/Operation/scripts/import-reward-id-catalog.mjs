#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import XLSX from 'xlsx';
import { initializeApp, deleteApp } from 'firebase/app';
import { getFirestore, doc, setDoc, getDoc, serverTimestamp } from 'firebase/firestore';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const operationDir = path.resolve(__dirname, '..');
const repoRoot = path.resolve(operationDir, '../../..');

const envPath = path.resolve(operationDir, '.env');
const targetDocPath = 'config/rewardIdCatalog';

const DEFAULT_ITEM_TABLE_REL_PATH = 'input/Domains/Game/ItemTable.xlsx';
const DEFAULT_UNIT_TABLE_REL_PATH = 'input/Domains/Game/UnitTable.xlsx';
const DEFAULT_ENUM_TYPES_REL_PATH = 'input/Domains/Game/ENUM_META.json';
const ENV_ITEM_TABLE_PATH = 'OP_REWARD_ITEM_TABLE_XLSX_PATH';
const ENV_UNIT_TABLE_PATH = 'OP_REWARD_UNIT_TABLE_XLSX_PATH';
const ENV_ENUM_TYPES_PATH = 'OP_REWARD_ENUM_TYPES_JSON_PATH';
const LOG_PREFIX = '[reward-id-catalog-import]';

const dryRun = process.argv.includes('--dry-run');

function readEnvFile(filePath) {
  if (!fs.existsSync(filePath)) {
    throw new Error(`.env not found: ${filePath}`);
  }

  const text = fs.readFileSync(filePath, 'utf8');
  const map = {};
  const lines = text.split(/\r?\n/);
  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const eq = trimmed.indexOf('=');
    if (eq <= 0) continue;
    const key = trimmed.slice(0, eq).trim();
    let value = trimmed.slice(eq + 1).trim();
    if (
      (value.startsWith('"') && value.endsWith('"'))
      || (value.startsWith('\'') && value.endsWith('\''))
    ) {
      value = value.slice(1, -1);
    }
    map[key] = value;
  }
  return map;
}

function resolveInputPath(rawPath, fallbackRelativePath) {
  const candidate = String(rawPath ?? '').trim() || fallbackRelativePath;
  if (path.isAbsolute(candidate)) {
    return candidate;
  }
  return path.resolve(repoRoot, candidate);
}

function toSourcePath(filePath) {
  const relative = path.relative(repoRoot, filePath);
  if (!relative.startsWith('..')) {
    return relative.split(path.sep).join('/');
  }
  return filePath;
}

function extractIdsFromSheet(filePath, sheetName, idColumnName) {
  const wb = XLSX.readFile(filePath);
  const ws = wb.Sheets[sheetName];
  if (!ws) {
    throw new Error(`Sheet not found: ${sheetName} in ${filePath}`);
  }

  const rows = XLSX.utils.sheet_to_json(ws, { header: 1, raw: false });
  const headerRow = rows[0] ?? [];
  const colIndex = headerRow.findIndex(
    (cell) => String(cell ?? '').trim().toLowerCase() === idColumnName.toLowerCase(),
  );

  if (colIndex < 0) {
    throw new Error(`Column '${idColumnName}' not found in ${filePath}#${sheetName}`);
  }

  const ids = [];
  // Devian table convention: row 1 header, row 2 type, row 3 options, row 4 description, row 5+ data
  for (let r = 4; r < rows.length; r++) {
    const row = rows[r] ?? [];
    const id = String(row[colIndex] ?? '').trim();
    if (id) ids.push(id);
  }

  return Array.from(new Set(ids)).sort((a, b) => a.localeCompare(b));
}

function extractCurrencyIdsFromEnumJson(filePath) {
  const raw = fs.readFileSync(filePath, 'utf8');
  const parsed = JSON.parse(raw);
  const enums = Array.isArray(parsed?.enums) ? parsed.enums : [];
  const currencyType = enums.find((entry) => String(entry?.name ?? '').trim() === 'CURRENCY_TYPE');
  if (!currencyType) {
    throw new Error(`CURRENCY_TYPE not found in ${filePath}`);
  }

  const values = Array.isArray(currencyType.values) ? currencyType.values : [];
  const ids = values
    .map((entry) => String(entry?.name ?? '').trim())
    .filter(Boolean);

  return Array.from(new Set(ids)).sort((a, b) => a.localeCompare(b));
}

function toIsoStringFromFirestoreTimestamp(value) {
  if (!value || typeof value !== 'object') return null;
  if (typeof value.toDate !== 'function') return null;
  const date = value.toDate();
  if (!(date instanceof Date) || Number.isNaN(date.getTime())) return null;
  return date.toISOString();
}

async function main() {
  const env = readEnvFile(envPath);

  const itemTablePath = resolveInputPath(env[ENV_ITEM_TABLE_PATH], DEFAULT_ITEM_TABLE_REL_PATH);
  const unitTablePath = resolveInputPath(env[ENV_UNIT_TABLE_PATH], DEFAULT_UNIT_TABLE_REL_PATH);
  const enumTypesPath = resolveInputPath(env[ENV_ENUM_TYPES_PATH], DEFAULT_ENUM_TYPES_REL_PATH);

  if (!fs.existsSync(itemTablePath)) {
    throw new Error(`Input xlsx not found: ${itemTablePath} (${ENV_ITEM_TABLE_PATH})`);
  }
  if (!fs.existsSync(unitTablePath)) {
    throw new Error(`Input xlsx not found: ${unitTablePath} (${ENV_UNIT_TABLE_PATH})`);
  }
  if (!fs.existsSync(enumTypesPath)) {
    throw new Error(`Input enum json not found: ${enumTypesPath} (${ENV_ENUM_TYPES_PATH})`);
  }

  const currencyIds = extractCurrencyIdsFromEnumJson(enumTypesPath);
  const equipIds = extractIdsFromSheet(itemTablePath, 'ITEM_EQUIP', 'equipId');
  const cardIds = extractIdsFromSheet(itemTablePath, 'ITEM_CARD', 'cardId');
  const heroIds = extractIdsFromSheet(unitTablePath, 'UNIT_HERO', 'unitId');

  const basePayload = {
    currencyIds,
    equipIds,
    cardIds,
    heroIds,
    source: {
      enumTypesPath: toSourcePath(enumTypesPath),
      itemTablePath: toSourcePath(itemTablePath),
      unitTablePath: toSourcePath(unitTablePath),
    },
  };

  console.log(`${LOG_PREFIX} Input files`);
  console.log(`  EnumTypes: ${toSourcePath(enumTypesPath)}`);
  console.log(`  ItemTable: ${toSourcePath(itemTablePath)}`);
  console.log(`  UnitTable: ${toSourcePath(unitTablePath)}`);
  console.log(`${LOG_PREFIX} Parsed IDs`);
  console.log(`  CURRENCY: ${currencyIds.length}`);
  console.log(`  EQUIP: ${equipIds.length}`);
  console.log(`  CARD: ${cardIds.length}`);
  console.log(`  HERO: ${heroIds.length}`);

  if (dryRun) {
    console.log(`${LOG_PREFIX} Dry-run mode. Firestore write skipped.`);
    console.log(JSON.stringify({
      ...basePayload,
      importedAt: 'SERVER_TIME_ON_WRITE',
    }, null, 2));
    return;
  }

  const app = initializeApp({
    apiKey: env.VITE_FIREBASE_API_KEY ?? '',
    authDomain: env.VITE_FIREBASE_AUTH_DOMAIN ?? '',
    projectId: env.VITE_FIREBASE_PROJECT_ID ?? '',
    storageBucket: env.VITE_FIREBASE_STORAGE_BUCKET ?? '',
    messagingSenderId: env.VITE_FIREBASE_MESSAGING_SENDER_ID ?? '',
    appId: env.VITE_FIREBASE_APP_ID ?? '',
  });

  try {
    const db = getFirestore(app);
    const docRef = doc(db, targetDocPath);

    // Step 1: Ask Firestore server to stamp write time.
    await setDoc(docRef, {
      ...basePayload,
      importedAt: serverTimestamp(),
    });

    // Step 2: Read resolved server timestamp and persist as ISO string.
    const snap = await getDoc(docRef);
    const importedAt = toIsoStringFromFirestoreTimestamp(snap.data()?.importedAt);
    if (!importedAt) {
      throw new Error('Failed to resolve Firestore server timestamp for importedAt.');
    }

    await setDoc(docRef, {
      ...basePayload,
      importedAt,
    });

    console.log(`${LOG_PREFIX} importedAt: ${importedAt}`);
    console.log(`${LOG_PREFIX} Exported to Firestore '${targetDocPath}'.`);
  } finally {
    await deleteApp(app);
  }
}

main().catch((error) => {
  console.error(`${LOG_PREFIX} Failed:`, error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
});
