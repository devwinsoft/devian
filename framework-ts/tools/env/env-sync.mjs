#!/usr/bin/env node
/**
 * env-sync.mjs
 *
 * Generates .env.example from env.spec.json
 *
 * Usage: node env-sync.mjs <appRoot>
 * Example: node ../../tools/env/env-sync.mjs .
 */

import fs from 'fs';
import path from 'path';

const appRoot = process.argv[2];

if (!appRoot) {
    console.error('[env-sync] Error: appRoot argument is required');
    console.error('Usage: node env-sync.mjs <appRoot>');
    process.exit(1);
}

const specPath = path.resolve(appRoot, 'env.spec.json');
const examplePath = path.resolve(appRoot, '.env.example');

// Read env.spec.json
if (!fs.existsSync(specPath)) {
    console.error(`[env-sync] Error: ${specPath} not found`);
    process.exit(1);
}

let spec;
try {
    const content = fs.readFileSync(specPath, 'utf-8');
    spec = JSON.parse(content);
} catch (err) {
    console.error(`[env-sync] Error: Failed to parse ${specPath}`);
    console.error(err.message);
    process.exit(1);
}

if (!Array.isArray(spec)) {
    console.error(`[env-sync] Error: env.spec.json must be an array`);
    process.exit(1);
}

// Generate .env.example content
const lines = [
    '# AUTO-GENERATED. DO NOT EDIT.',
    '# Source of truth: env.spec.json',
    '# Copy this file to .env and adjust values if needed.',
    '',
];

for (const entry of spec) {
    if (!entry.key || entry.default === undefined || !entry.description) {
        console.error(`[env-sync] Error: Invalid entry in env.spec.json`);
        console.error(`  Each entry must have: key, default, description`);
        console.error(`  Got:`, JSON.stringify(entry));
        process.exit(1);
    }

    lines.push(`# ${entry.description}`);
    lines.push(`${entry.key}=${entry.default}`);
    lines.push('');
}

const output = lines.join('\n');

// Write .env.example
fs.writeFileSync(examplePath, output, 'utf-8');
console.log(`[env-sync] Generated ${examplePath}`);
