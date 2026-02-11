/**
 * sync-meta.js
 *
 * Unity가 생성한 .meta 파일을 Packages에서 UPM으로 역이관하는 도구.
 * - Packages/{pkg}의 .meta 파일을 upm/{pkg}로 복사
 * - non-meta 파일은 절대 건드리지 않음
 *
 * Usage:
 *   node tools/sync-meta.js <config.json>
 *   npm -w builder run sync-meta -- ../input/config.json
 *
 * SSOT: skills/devian-unity/01-policy/SKILL.md
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/**
 * Recursively find all .meta files in a directory
 * @param {string} dir - Directory to search
 * @param {string} baseDir - Base directory for relative path calculation
 * @returns {string[]} - Array of relative paths to .meta files
 */
function findMetaFiles(dir, baseDir = dir) {
    const results = [];

    if (!fs.existsSync(dir)) {
        return results;
    }

    const entries = fs.readdirSync(dir, { withFileTypes: true });

    for (const entry of entries) {
        const fullPath = path.join(dir, entry.name);
        const relativePath = path.relative(baseDir, fullPath);

        if (entry.isDirectory()) {
            results.push(...findMetaFiles(fullPath, baseDir));
        } else if (entry.name.endsWith('.meta')) {
            results.push(relativePath);
        }
    }

    return results;
}

/**
 * Sync .meta files from Packages to UPM
 * @param {string} packagesDir - Source: UnityExample/Packages
 * @param {string} upmDir - Target: upm
 */
function syncMetaFiles(packagesDir, upmDir) {
    console.log('='.repeat(60));
    console.log('sync-meta: Sync .meta files from Packages to UPM');
    console.log('='.repeat(60));
    console.log(`  Source: ${packagesDir}`);
    console.log(`  Target: ${upmDir}`);
    console.log();

    // Get all com.devian.* packages
    if (!fs.existsSync(packagesDir)) {
        console.error(`[ERROR] Packages directory not found: ${packagesDir}`);
        process.exit(1);
    }

    const packages = fs.readdirSync(packagesDir, { withFileTypes: true })
        .filter(entry => entry.isDirectory() && entry.name.startsWith('com.devian.'))
        .map(entry => entry.name);

    console.log(`Found ${packages.length} packages to process.`);
    console.log();

    let totalAdded = 0;
    let totalUpdated = 0;
    let totalSkipped = 0;

    for (const pkgName of packages) {
        const pkgPackagesPath = path.join(packagesDir, pkgName);
        const pkgUpmPath = path.join(upmDir, pkgName);

        console.log(`[Package] ${pkgName}`);

        if (!fs.existsSync(pkgUpmPath)) {
            console.log(`  [SKIP] UPM path not found: ${pkgUpmPath}`);
            continue;
        }

        // Find all .meta files in Packages
        const metaFiles = findMetaFiles(pkgPackagesPath, pkgPackagesPath);

        let pkgAdded = 0;
        let pkgUpdated = 0;
        let pkgSkipped = 0;

        for (const relativePath of metaFiles) {
            const sourceFile = path.join(pkgPackagesPath, relativePath);
            const targetFile = path.join(pkgUpmPath, relativePath);

            // Read source content
            const sourceContent = fs.readFileSync(sourceFile);

            // Check if target exists
            if (fs.existsSync(targetFile)) {
                // Compare content
                const targetContent = fs.readFileSync(targetFile);
                if (Buffer.compare(sourceContent, targetContent) === 0) {
                    pkgSkipped++;
                    continue;
                }
                // Content differs - update
                fs.writeFileSync(targetFile, sourceContent);
                console.log(`  [UPDATE] ${relativePath}`);
                pkgUpdated++;
            } else {
                // Create directory if needed
                const targetDir = path.dirname(targetFile);
                if (!fs.existsSync(targetDir)) {
                    fs.mkdirSync(targetDir, { recursive: true });
                }
                // Add new file
                fs.writeFileSync(targetFile, sourceContent);
                console.log(`  [ADD] ${relativePath}`);
                pkgAdded++;
            }
        }

        console.log(`  Summary: ${pkgAdded} added, ${pkgUpdated} updated, ${pkgSkipped} unchanged`);
        totalAdded += pkgAdded;
        totalUpdated += pkgUpdated;
        totalSkipped += pkgSkipped;
    }

    console.log();
    console.log('='.repeat(60));
    console.log(`Total: ${totalAdded} added, ${totalUpdated} updated, ${totalSkipped} unchanged`);
    console.log('='.repeat(60));
}

/**
 * Main entry point
 */
function main() {
    const args = process.argv.slice(2);

    if (args.length < 1) {
        console.log('Usage: node tools/sync-meta.js <config.json>');
        console.log('  or:  npm -w builder run sync-meta -- <config.json>');
        process.exit(1);
    }

    const configPath = path.resolve(args[0]);

    if (!fs.existsSync(configPath)) {
        console.error(`[ERROR] Config file not found: ${configPath}`);
        process.exit(1);
    }

    const config = JSON.parse(fs.readFileSync(configPath, 'utf-8'));

    // Resolve paths relative to config file location
    const configDir = path.dirname(configPath);

    const resolvePath = (p) => {
        if (path.isAbsolute(p)) return p;
        return path.resolve(configDir, p);
    };

    // Get upmConfig
    const upmConfig = config.upmConfig;
    if (!upmConfig) {
        console.error('[ERROR] upmConfig not found in config');
        process.exit(1);
    }

    const upmSourceDir = resolvePath(upmConfig.sourceDir);
    const upmPackageDir = resolvePath(upmConfig.packageDir);

    if (!upmSourceDir || !upmPackageDir) {
        console.error('[ERROR] upmConfig.sourceDir and upmConfig.packageDir are required');
        process.exit(1);
    }

    // Run sync
    syncMetaFiles(upmPackageDir, upmSourceDir);
}

main();
