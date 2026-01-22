#!/usr/bin/env node
/**
 * Devian Project Archive Tool (Node.js)
 * 프로젝트 전체를 zip 파일로 아카이브합니다.
 * 
 * 외부 의존성 없이 시스템 zip 명령어 또는 PowerShell을 사용합니다.
 *
 * Usage:
 *   node archive.js [--root PATH] [--output PATH] [options]
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { execSync, spawnSync } from 'child_process';
import os from 'os';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 내장 제외 목록
const BUILTIN_EXCLUDES = [
    '.git',
    '.git/**',
    'node_modules',
    'node_modules/**',
    '__pycache__',
    '__pycache__/**',
    '*.pyc',
    '.DS_Store',
    '*.zip',
];

/**
 * .gitignore 파일을 파싱하여 패턴 목록을 반환합니다.
 */
function parseGitignore(gitignorePath) {
    const patterns = [];

    if (!fs.existsSync(gitignorePath)) {
        return patterns;
    }

    const content = fs.readFileSync(gitignorePath, 'utf-8');
    const lines = content.split('\n');

    for (let line of lines) {
        line = line.trim();

        // 빈 줄, 주석 무시
        if (!line || line.startsWith('#')) {
            continue;
        }

        // 부정 패턴(!)은 현재 지원하지 않음
        if (line.startsWith('!')) {
            continue;
        }

        patterns.push(line);
    }

    return patterns;
}

/**
 * 간단한 glob 매칭 함수
 */
function minimatch(str, pattern) {
    let regexPattern = pattern
        .replace(/\./g, '\\.')
        .replace(/\*\*/g, '{{GLOBSTAR}}')
        .replace(/\*/g, '[^/]*')
        .replace(/\?/g, '[^/]')
        .replace(/{{GLOBSTAR}}/g, '.*');

    regexPattern = `^${regexPattern}$`;

    try {
        return new RegExp(regexPattern).test(str);
    } catch (e) {
        return false;
    }
}

/**
 * 경로가 gitignore 스타일 패턴과 일치하는지 확인합니다.
 */
function matchesPattern(filePath, pattern) {
    pattern = pattern.replace(/\/$/, '');

    if (pattern.includes('**')) {
        let regexPattern = pattern
            .replace(/\*\*\//g, '(.*/)?')
            .replace(/\*\*/g, '.*')
            .replace(/\*/g, '[^/]*')
            .replace(/\?/g, '[^/]')
            .replace(/\./g, '\\.');
        regexPattern = `^${regexPattern}$`;

        try {
            if (new RegExp(regexPattern).test(filePath)) {
                return true;
            }
        } catch (e) {
            // regex error, skip
        }
    }

    const pathParts = filePath.split('/');

    if (pattern.startsWith('/')) {
        pattern = pattern.slice(1);
        return minimatch(filePath, pattern);
    }

    if (!pattern.includes('/')) {
        for (const part of pathParts) {
            if (minimatch(part, pattern)) {
                return true;
            }
        }
        return minimatch(filePath, pattern) || minimatch(filePath, `**/${pattern}`);
    }

    return minimatch(filePath, pattern) || minimatch(filePath, `**/${pattern}`);
}

/**
 * 경로가 제외 대상인지 확인합니다.
 */
function shouldExclude(relPath, patterns, isDir = false) {
    for (const pattern of patterns) {
        if (matchesPattern(relPath, pattern)) {
            return true;
        }

        if (isDir && matchesPattern(relPath + '/', pattern)) {
            return true;
        }
    }

    return false;
}

/**
 * input_common.json 또는 .git을 찾아 프로젝트 루트를 결정합니다.
 */
function findProjectRoot(startPath) {
    let current = path.resolve(startPath);

    while (current !== path.dirname(current)) {
        if (fs.existsSync(path.join(current, 'input', 'input_common.json'))) {
            return current;
        }
        if (fs.existsSync(path.join(current, 'input_common.json'))) {
            return current;
        }
        if (fs.existsSync(path.join(current, '.git'))) {
            return current;
        }
        if (fs.existsSync(path.join(current, 'skills'))) {
            return current;
        }
        current = path.dirname(current);
    }

    return path.resolve(startPath);
}

/**
 * 아카이브할 파일 목록을 수집합니다.
 */
function collectFiles(root, excludePatterns, options = {}) {
    const files = [];
    const extraExcludes = [...excludePatterns];

    if (options.excludeGenerated) {
        extraExcludes.push('**/generated', '**/generated/**');
    }

    if (options.excludeData) {
        extraExcludes.push('**/*.ndjson');
    }

    if (!options.includeTemp) {
        extraExcludes.push('temp', 'temp/**', '**/temp', '**/temp/**');
    }

    function walkDir(dir) {
        const entries = fs.readdirSync(dir, { withFileTypes: true });

        for (const entry of entries) {
            const fullPath = path.join(dir, entry.name);
            const relPath = path.relative(root, fullPath).replace(/\\/g, '/');

            if (entry.isDirectory()) {
                if (!shouldExclude(relPath, extraExcludes, true)) {
                    walkDir(fullPath);
                }
            } else {
                if (!shouldExclude(relPath, extraExcludes, false)) {
                    files.push(relPath);
                }
            }
        }
    }

    walkDir(root);
    return files;
}

/**
 * zip 명령어 사용 가능 여부 확인
 */
function hasZipCommand() {
    try {
        execSync('zip --version', { stdio: 'ignore' });
        return true;
    } catch {
        return false;
    }
}

/**
 * 시스템 zip 명령어로 아카이브 생성 (Linux/Mac/WSL)
 */
function createArchiveWithZip(root, files, archivePath) {
    const result = spawnSync('zip', ['-1', archivePath, ...files], {
        cwd: root,
        encoding: 'utf-8',
        maxBuffer: 50 * 1024 * 1024,
    });

    if (result.status !== 0) {
        throw new Error(`zip failed: ${result.stderr}`);
    }
}

/**
 * PowerShell로 아카이브 생성 (Windows)
 */
function createArchiveWithPowerShell(root, files, archivePath) {
    const tempDir = path.join(os.tmpdir(), `devian-archive-${Date.now()}`);
    fs.mkdirSync(tempDir, { recursive: true });

    try {
        for (const relPath of files) {
            const srcPath = path.join(root, relPath);
            const destPath = path.join(tempDir, relPath);
            const destDir = path.dirname(destPath);
            
            fs.mkdirSync(destDir, { recursive: true });
            fs.copyFileSync(srcPath, destPath);
        }

        const psCommand = `Compress-Archive -Path "${tempDir}\\*" -DestinationPath "${archivePath}" -CompressionLevel Fastest -Force`;
        execSync(`powershell -Command "${psCommand}"`, { stdio: 'ignore' });
    } finally {
        fs.rmSync(tempDir, { recursive: true, force: true });
    }
}

/**
 * zip 아카이브를 생성합니다.
 */
function createArchive(root, files, outputDir) {
    const now = new Date();
    const pad = (n) => String(n).padStart(2, '0');
    const timestamp = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`;
    const archiveName = `devian-${timestamp}.zip`;
    const archivePath = path.join(outputDir, archiveName);

    if (fs.existsSync(archivePath)) {
        fs.unlinkSync(archivePath);
    }

    if (hasZipCommand()) {
        createArchiveWithZip(root, files, archivePath);
    } else if (os.platform() === 'win32') {
        createArchiveWithPowerShell(root, files, archivePath);
    } else {
        throw new Error('zip 명령어를 찾을 수 없습니다. zip을 설치해주세요: sudo apt install zip');
    }

    return archivePath;
}

/**
 * CLI 인자 파싱
 */
function parseArgs(args) {
    const options = {
        root: null,
        output: null,
        excludeGenerated: false,
        excludeData: false,
        includeTemp: false,
    };

    for (let i = 0; i < args.length; i++) {
        const arg = args[i];

        switch (arg) {
            case '--root':
                options.root = args[++i];
                break;
            case '--output':
                options.output = args[++i];
                break;
            case '--exclude-generated':
                options.excludeGenerated = true;
                break;
            case '--exclude-data':
                options.excludeData = true;
                break;
            case '--include-temp':
                options.includeTemp = true;
                break;
            case '--help':
            case '-h':
                console.log(`
Devian Project Archive Tool

Usage:
  node archive.js [options]

Options:
  --root <path>          프로젝트 루트 경로 (기본: 자동 탐지)
  --output <path>        출력 디렉토리 (기본: 프로젝트 루트)
  --exclude-generated    generated 폴더 제외
  --exclude-data         ndjson 데이터 파일 제외
  --include-temp         temp 폴더 포함
  -h, --help             도움말 표시
`);
                process.exit(0);
        }
    }

    return options;
}

/**
 * 메인 함수
 */
function main() {
    const args = process.argv.slice(2);
    const options = parseArgs(args);

    const root = options.root
        ? path.resolve(options.root)
        : findProjectRoot(process.cwd());

    console.log(`프로젝트 루트: ${root}`);

    const outputDir = options.output
        ? path.resolve(options.output)
        : root;

    if (!fs.existsSync(outputDir)) {
        fs.mkdirSync(outputDir, { recursive: true });
    }

    const excludePatterns = [...BUILTIN_EXCLUDES];

    const gitignorePath = path.join(root, '.gitignore');
    if (fs.existsSync(gitignorePath)) {
        const gitignorePatterns = parseGitignore(gitignorePath);
        excludePatterns.push(...gitignorePatterns);
        console.log(`.gitignore에서 ${gitignorePatterns.length}개 패턴 로드`);
    }

    console.log('파일 수집 중...');
    const files = collectFiles(root, excludePatterns, options);

    console.log(`수집된 파일: ${files.length}개`);

    if (files.length === 0) {
        console.log('아카이브할 파일이 없습니다.');
        return;
    }

    console.log('아카이브 생성 중...');
    const archivePath = createArchive(root, files, outputDir);

    const stats = fs.statSync(archivePath);
    const sizeMb = (stats.size / (1024 * 1024)).toFixed(2);
    console.log(`완료: ${archivePath}`);
    console.log(`크기: ${sizeMb} MB`);
}

main();
