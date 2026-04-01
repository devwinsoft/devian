import {google, sheets_v4} from "googleapis";
import * as logger from "firebase-functions/logger";
import {defineSecret} from "firebase-functions/params";

export const PURCHASE_AUDIT_CREDENTIALS_SECRET = defineSecret("GOOGLE_DRIVE_AUDIT_CREDENTIALS_JSON");

const DEFAULT_REGION = "asia-northeast3";
const AUDIT_HEADER = [
  "loggedAtUtcIso",
  "purchaseId",
  "storeKey",
  "internal_product_id",
  "kind",
  "storeProductId",
  "storePurchaseId",
  "verifyStatus",
  "region",
  "storePurchasedAtUtcIso",
  "eventOccurredAtUtcIso",
];
const AUDIT_HEADER_WRITE_RANGE = "A1:K1";
const AUDIT_APPEND_RANGE = "A:K";

export interface PurchaseAuditAppendInput {
  purchaseId: string;
  storeKey?: string;
  internal_product_id?: string;
  kind?: string;
  storeProductId?: string;
  storePurchaseId?: string;
  verifyStatus?: string;
  storePurchasedAtMs?: number;
  eventOccurredAtMs?: number;
}

let cachedSheetsClient: sheets_v4.Sheets | null = null;
const initializedSheetKeys = new Set<string>();
const initializingSheetPromises = new Map<string, Promise<void>>();
let warnedMissingSheetId = false;

function getSpreadsheetId(): string {
  return String(process.env.PURCHASE_AUDIT_SHEET_ID ?? "").trim();
}

function getAuditRegion(): string {
  return String(process.env.PURCHASE_AUDIT_REGION ?? process.env.FUNCTION_REGION ?? DEFAULT_REGION).trim();
}

function toUtcIsoOrEmpty(ms?: number): string {
  if (!ms || !Number.isFinite(ms) || ms <= 0) return "";
  return new Date(ms).toISOString();
}

function getLoggedAtUtcIso(): string {
  return new Date().toISOString();
}

function getLogMonthUtc(loggedAtUtcIso: string): string {
  return loggedAtUtcIso.slice(0, 7);
}

function getMonthlySheetTitle(loggedAtUtcIso: string): string {
  return getLogMonthUtc(loggedAtUtcIso);
}

function normalizeCell(value?: string): string {
  return String(value ?? "");
}

async function getSpreadsheetMetadata(sheets: sheets_v4.Sheets, spreadsheetId: string) {
  const spreadsheet = await sheets.spreadsheets.get({
    spreadsheetId,
    fields: "sheets.properties(sheetId,title,index)",
  });
  return spreadsheet.data.sheets ?? [];
}

async function runSheetInitializer(sheetKey: string, initializer: () => Promise<void>) {
  if (initializedSheetKeys.has(sheetKey)) return;

  const existingPromise = initializingSheetPromises.get(sheetKey);
  if (existingPromise) {
    await existingPromise;
    return;
  }

  const promise = (async () => {
    await initializer();
    initializedSheetKeys.add(sheetKey);
  })().finally(() => {
    initializingSheetPromises.delete(sheetKey);
  });

  initializingSheetPromises.set(sheetKey, promise);
  await promise;
}

async function getSheetsClient(): Promise<sheets_v4.Sheets> {
  if (cachedSheetsClient) return cachedSheetsClient;

  const credentials = JSON.parse(PURCHASE_AUDIT_CREDENTIALS_SECRET.value());
  const auth = new google.auth.GoogleAuth({
    credentials,
    scopes: ["https://www.googleapis.com/auth/spreadsheets"],
  });

  cachedSheetsClient = google.sheets({version: "v4", auth});
  return cachedSheetsClient;
}

async function ensureMonthlySheetReady(
  sheets: sheets_v4.Sheets,
  spreadsheetId: string,
  tabName: string,
) {
  const sheetKey = `${spreadsheetId}:${tabName}`;
  await runSheetInitializer(sheetKey, async () => {
    const sheetMetadata = await getSpreadsheetMetadata(sheets, spreadsheetId);
    const existingSheet = sheetMetadata.find((sheet) => sheet.properties?.title === tabName);

    if (!existingSheet) {
      await sheets.spreadsheets.batchUpdate({
        spreadsheetId,
        requestBody: {
          requests: [{
            addSheet: {
              properties: {
                title: tabName,
                gridProperties: {
                  frozenRowCount: 1,
                },
              },
            },
          }],
        },
      });
    }

    const headerRange = `${tabName}!1:1`;
    const header = await sheets.spreadsheets.values.get({
      spreadsheetId,
      range: headerRange,
    });

    const currentHeader = header.data.values?.[0] ?? [];
    const needsHeader = currentHeader.length === 0 ||
      currentHeader.join("\u0001") !== AUDIT_HEADER.join("\u0001");

    if (needsHeader) {
      await sheets.spreadsheets.values.clear({
        spreadsheetId,
        range: headerRange,
      });
      await sheets.spreadsheets.values.update({
        spreadsheetId,
        range: `${tabName}!${AUDIT_HEADER_WRITE_RANGE}`,
        valueInputOption: "RAW",
        requestBody: {
          values: [AUDIT_HEADER],
        },
      });
    }
  });
}

export async function ensurePurchaseAuditWorkbook(loggedAtUtcIso = getLoggedAtUtcIso()): Promise<boolean> {
  const spreadsheetId = getSpreadsheetId();
  if (!spreadsheetId) {
    if (!warnedMissingSheetId) {
      warnedMissingSheetId = true;
      logger.warn("[purchaseAuditSheet] PURCHASE_AUDIT_SHEET_ID is not configured. Audit log append will be skipped.");
    }
    return false;
  }

  const sheets = await getSheetsClient();
  await ensureMonthlySheetReady(sheets, spreadsheetId, getMonthlySheetTitle(loggedAtUtcIso));
  return true;
}

export async function appendPurchaseAuditRow(input: PurchaseAuditAppendInput): Promise<boolean> {
  const loggedAtUtcIso = getLoggedAtUtcIso();
  const spreadsheetId = getSpreadsheetId();
  if (!spreadsheetId) return ensurePurchaseAuditWorkbook(loggedAtUtcIso);

  const workbookReady = await ensurePurchaseAuditWorkbook(loggedAtUtcIso);
  if (!workbookReady) return false;

  const tabName = getMonthlySheetTitle(loggedAtUtcIso);
  const row = [[
    loggedAtUtcIso,
    input.purchaseId,
    normalizeCell(input.storeKey),
    normalizeCell(input.internal_product_id),
    normalizeCell(input.kind),
    normalizeCell(input.storeProductId),
    normalizeCell(input.storePurchaseId),
    normalizeCell(input.verifyStatus),
    getAuditRegion(),
    toUtcIsoOrEmpty(input.storePurchasedAtMs),
    toUtcIsoOrEmpty(input.eventOccurredAtMs),
  ]];

  const sheets = await getSheetsClient();
  await sheets.spreadsheets.values.append({
    spreadsheetId,
    range: `${tabName}!${AUDIT_APPEND_RANGE}`,
    valueInputOption: "RAW",
    insertDataOption: "INSERT_ROWS",
    requestBody: {
      values: row,
    },
  });
  return true;
}
