#!/usr/bin/env node
/*
  Devian Skill Runner: devian-core (TypeScript) Generator

  What it does:
  - (Optional) runs: dotnet run --project framework/cs/Devian.Tools -- build
  - copies generated TS artifacts from:
      modules/ts/common/generated/*.g.ts
      modules/ts/ws/generated/*.g.ts
    into:
      framework/ts/devian-core/src/generated/{common,ws}
  - writes generated export barrel:
      framework/ts/devian-core/src/generated/index.g.ts
    and ensures:
      framework/ts/devian-core/src/index.ts

  Notes:
  - This runner never edits modules/**.
  - Deterministic: file list is sorted; no timestamps.
*/

import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

function parseArgs(argv) {
  const out = { repo: null, build: false };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--repo" && i + 1 < argv.length) {
      out.repo = argv[i + 1];
      i++;
      continue;
    }
    if (a === "--build") {
      out.build = true;
      continue;
    }
  }
  return out;
}

function findRepoRoot(startDir) {
  let dir = startDir;
  for (let i = 0; i < 12; i++) {
    const sln = path.join(dir, "Devian.sln");
    const tools = path.join(dir, "framework", "cs", "Devian.Tools");
    if (fs.existsSync(sln) && fs.existsSync(tools)) return dir;
    const parent = path.dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  return null;
}

function ensureDir(p) {
  fs.mkdirSync(p, { recursive: true });
}

function readDirFiles(dir, predicate) {
  if (!fs.existsSync(dir)) return [];
  return fs
    .readdirSync(dir)
    .filter((f) => predicate(f))
    .map((f) => path.join(dir, f));
}

function copyFileDeterministic(src, dst) {
  // Preserve content exactly; normalize line endings to LF to avoid OS drift.
  const raw = fs.readFileSync(src, "utf8");
  const normalized = raw.replace(/\r\n/g, "\n");
  fs.writeFileSync(dst, normalized, "utf8");
}

function writeIndexBarrel(outFile, exports) {
  const lines = [];
  lines.push("// AUTO-GENERATED. DO NOT EDIT.");
  for (const e of exports) {
    lines.push(`export * from ${JSON.stringify(e)};`);
  }
  lines.push("");
  fs.writeFileSync(outFile, lines.join("\n"), "utf8");
}

function ensureTsPackageSkeleton(repoRoot) {
  const pkgRoot = path.join(repoRoot, "framework", "ts", "devian-core");
  const srcRoot = path.join(pkgRoot, "src");
  const genRoot = path.join(srcRoot, "generated");

  ensureDir(genRoot);

  const pkgJson = path.join(pkgRoot, "package.json");
  if (!fs.existsSync(pkgJson)) {
    fs.writeFileSync(
      pkgJson,
      JSON.stringify(
        {
          name: "devian-core",
          version: "0.0.0",
          private: true,
          type: "module",
          main: "dist/index.js",
          types: "dist/index.d.ts",
          files: ["dist"],
          scripts: {
            build: "tsc -p tsconfig.json",
            clean: "rm -rf dist"
          },
          devDependencies: {
            typescript: "^5.0.0"
          }
        },
        null,
        2
      ) + "\n",
      "utf8"
    );
  }

  const tsconfig = path.join(pkgRoot, "tsconfig.json");
  if (!fs.existsSync(tsconfig)) {
    fs.writeFileSync(
      tsconfig,
      JSON.stringify(
        {
          compilerOptions: {
            target: "ES2022",
            module: "ES2022",
            moduleResolution: "Bundler",
            declaration: true,
            outDir: "dist",
            rootDir: "src",
            strict: true,
            skipLibCheck: true
          },
          include: ["src/**/*"]
        },
        null,
        2
      ) + "\n",
      "utf8"
    );
  }

  const indexTs = path.join(srcRoot, "index.ts");
  if (!fs.existsSync(indexTs)) {
    fs.writeFileSync(
      indexTs,
      [
        "// AUTO-GENERATED DEFAULT ENTRY (safe to edit if you know what you are doing)",
        "export * from \"./generated/index.g\";",
        ""
      ].join("\n"),
      "utf8"
    );
  }

  return { pkgRoot, srcRoot, genRoot };
}

function runBuild(repoRoot) {
  const r = spawnSync(
    "dotnet",
    ["run", "--project", "framework/cs/Devian.Tools", "--", "build"],
    {
      cwd: repoRoot,
      stdio: "inherit"
    }
  );
  if (r.status !== 0) {
    throw new Error(`Devian.Tools build failed (exit=${r.status})`);
  }
}

function main() {
  const args = parseArgs(process.argv);
  const repoRoot = args.repo
    ? path.resolve(args.repo)
    : findRepoRoot(process.cwd());

  if (!repoRoot) {
    console.error("ERROR: repo root not found. Use --repo <path>.");
    process.exit(1);
  }

  console.log("=== devian-core-ts-generator ===");
  console.log(`  repo: ${repoRoot}`);

  // 보완 2: build 실행 옵션 명확화
  if (args.build) {
    console.log("  mode: --build (running Devian.Tools build first)");
    runBuild(repoRoot);
  } else {
    console.log("  mode: copy-only (no build, use --build to run Devian.Tools)");
  }

  // 보완 3: 패키지 골격 보장
  console.log("\n[1/4] Ensuring package skeleton...");
  const { genRoot, pkgRoot } = ensureTsPackageSkeleton(repoRoot);
  console.log(`  - ${path.relative(repoRoot, pkgRoot)}/package.json`);
  console.log(`  - ${path.relative(repoRoot, pkgRoot)}/tsconfig.json`);
  console.log(`  - ${path.relative(repoRoot, pkgRoot)}/src/index.ts`);

  // 보완 1: 입력 산출물 존재 검증
  console.log("\n[2/4] Scanning input artifacts...");
  const srcCommon = path.join(repoRoot, "modules", "ts", "common", "generated");
  const srcWs = path.join(repoRoot, "modules", "ts", "ws", "generated");

  const commonFiles = readDirFiles(srcCommon, (f) => f.endsWith(".g.ts"));
  const wsFiles = readDirFiles(srcWs, (f) => f.endsWith(".g.ts"));
  const totalFiles = commonFiles.length + wsFiles.length;

  console.log(`  - common: ${commonFiles.length} file(s) from ${srcCommon}`);
  console.log(`  - ws:     ${wsFiles.length} file(s) from ${srcWs}`);

  if (totalFiles === 0) {
    console.error("\nERROR: No .g.ts files found in modules/ts/**/generated/");
    console.error("  - Ensure 'dotnet run --project framework/cs/Devian.Tools -- build' has been executed");
    console.error("  - Or run this script with --build option");
    process.exit(1);
  }

  // Copy files
  console.log("\n[3/4] Copying artifacts...");
  const dstCommon = path.join(genRoot, "common");
  const dstWs = path.join(genRoot, "ws");

  ensureDir(dstCommon);
  ensureDir(dstWs);

  // Copy (deterministic order)
  for (const src of commonFiles.sort()) {
    const dst = path.join(dstCommon, path.basename(src));
    copyFileDeterministic(src, dst);
  }
  for (const src of wsFiles.sort()) {
    const dst = path.join(dstWs, path.basename(src));
    copyFileDeterministic(src, dst);
  }
  console.log(`  - copied ${totalFiles} file(s)`);

  // Build export barrel
  console.log("\n[4/4] Generating export barrel...");
  const exports = [];
  for (const src of commonFiles.sort()) {
    exports.push(`./common/${path.basename(src, ".ts")}`);
  }
  for (const src of wsFiles.sort()) {
    exports.push(`./ws/${path.basename(src, ".ts")}`);
  }
  const indexG = path.join(genRoot, "index.g.ts");
  writeIndexBarrel(indexG, exports);
  console.log(`  - ${path.relative(repoRoot, indexG)}`);

  // Summary
  console.log("\n=== SUCCESS ===");
  console.log(`  output: ${path.relative(repoRoot, genRoot)}`);
  console.log(`  files:  ${totalFiles} artifact(s) + index.g.ts`);
  console.log("  next:   cd framework/ts/devian-core && npm install && npm run build");
}

main();
