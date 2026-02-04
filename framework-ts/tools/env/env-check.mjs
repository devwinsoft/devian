#!/usr/bin/env node
/**
 * env-check.mjs
 *
 * Validates that .env.example matches env.spec.json
 *
 * Usage: node env-check.mjs <appRoot>
 * Example: node ../../tools/env/env-check.mjs .
 */

import fs from 'fs';
import path from 'path';

const appRoot = process.argv[2];

if (!appRoot) {
    console.error('[env-check] Error: appRoot argument is required');
    console.error('Usage: node env-check.mjs <appRoot>');
    process.exit(1);
}

const specPath = path.resolve(appRoot, 'env.spec.json');
const examplePath = path.resolve(appRoot, '.env.example');

// Read env.spec.json
if (!fs.existsSync(specPath)) {
    console.error(`[env-check] Error: ${specPath} not found`);
    process.exit(1);
}

let spec;
try {
    const content = fs.readFileSync(specPath, 'utf-8');
    spec = JSON.parse(content);
} catch (err) {
    console.error(`[env-check] Error: Failed to parse ${specPath}`);
    console.error(err.message);
    process.exit(1);
}

if (!Array.isArray(spec)) {
    console.error(`[env-check] Error: env.spec.json must be an array`);
    process.exit(1);
}

// Generate expected .env.example content (same logic as env-sync)
const lines = [
    '# AUTO-GENERATED. DO NOT EDIT.',
    '# Source of truth: env.spec.json',
    '# Copy this file to .env and adjust values if needed.',
    '',
];

for (const entry of spec) {
    if (!entry.key || entry.default === undefined || !entry.description) {
        console.error(`[env-check] Error: Invalid entry in env.spec.json`);
        console.error(`  Each entry must have: key, default, description`);
        console.error(`  Got:`, JSON.stringify(entry));
        process.exit(1);
    }

    lines.push(`# ${entry.description}`);
    lines.push(`${entry.key}=${entry.default}`);
    lines.push('');
}

const expected = lines.join('\n');

// Read current .env.example
if (!fs.existsSync(examplePath)) {
    console.error(`[env-check] FAIL: ${examplePath} not found`);
    console.error(`[env-check] Run: npm run env:sync`);
    process.exit(1);
}

const actual = fs.readFileSync(examplePath, 'utf-8');

// Compare
if (actual !== expected) {
    const appName = path.basename(path.resolve(appRoot));
    console.error(`[env-check] FAIL: .env.example is out of sync in ${appName}`);
    console.error(`[env-check] Run: npm run env:sync`);
    process.exit(1);
}

console.log(`[env-check] OK: .env.example is up to date`);
