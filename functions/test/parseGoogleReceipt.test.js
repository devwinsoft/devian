const test = require("node:test");
const assert = require("node:assert/strict");

const verifyPurchaseModule = require("../lib/purchase/verifyPurchase.js");

const parseGoogleReceipt = verifyPurchaseModule.__test_parseGoogleReceipt;

test("parseGoogleReceipt parses raw purchase token with fallback package/product", () => {
  const result = parseGoogleReceipt(
    "cb3d4f2ff0abcdef",
    "com.devian.framework.chest_001",
    "com.devian.framework",
  );

  assert.deepEqual(result, {
    packageName: "com.devian.framework",
    productId: "com.devian.framework.chest_001",
    purchaseToken: "cb3d4f2ff0abcdef",
  });
});

test("parseGoogleReceipt parses Unity outer.Payload raw token string", () => {
  const payload = JSON.stringify({
    Store: "GooglePlay",
    Payload: "cb3d4f2ff0abcdef",
  });

  const result = parseGoogleReceipt(
    payload,
    "com.devian.framework.chest_001",
    "com.devian.framework",
  );

  assert.equal(result.purchaseToken, "cb3d4f2ff0abcdef");
  assert.equal(result.productId, "com.devian.framework.chest_001");
  assert.equal(result.packageName, "com.devian.framework");
});

test("parseGoogleReceipt parses payload.json raw token", () => {
  const payload = JSON.stringify({
    Store: "GooglePlay",
    Payload: {
      json: "cb3d4f2ff0abcdef",
      signature: "dummy-signature",
      packageName: "com.devian.framework",
      productId: "com.devian.framework.chest_001",
    },
  });

  const result = parseGoogleReceipt(
    payload,
    "com.devian.framework.chest_001",
    "com.devian.framework",
  );

  assert.equal(result.purchaseToken, "cb3d4f2ff0abcdef");
  assert.equal(result.productId, "com.devian.framework.chest_001");
  assert.equal(result.packageName, "com.devian.framework");
});

test("parseGoogleReceipt parses standard Unity nested json payload", () => {
  const payload = JSON.stringify({
    Store: "GooglePlay",
    Payload: JSON.stringify({
      json: JSON.stringify({
        packageName: "com.devian.framework",
        productId: "com.devian.framework.chest_001",
        purchaseToken: "cb3d4f2ff0abcdef",
      }),
      signature: "dummy-signature",
    }),
  });

  const result = parseGoogleReceipt(payload, "ignored", "ignored");

  assert.equal(result.purchaseToken, "cb3d4f2ff0abcdef");
  assert.equal(result.productId, "com.devian.framework.chest_001");
  assert.equal(result.packageName, "com.devian.framework");
});

test("parseGoogleReceipt throws when raw token fallback package/product missing", () => {
  assert.throws(
    () => parseGoogleReceipt("cb3d4f2ff0abcdef", "", ""),
    /fallback package\/product/i,
  );
});
