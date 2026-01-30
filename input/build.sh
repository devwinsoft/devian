#!/bin/bash
# Devian Build System v10 - Build Script
# Usage: ./build.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FRAMEWORK_TS_DIR="$SCRIPT_DIR/../framework-ts"
INPUT_JSON="$SCRIPT_DIR/input_common.json"

# Bootstrap: install dependencies if node_modules missing (root only)
if [ ! -d "$FRAMEWORK_TS_DIR/node_modules" ]; then
  echo "[Bootstrap] Installing dependencies (framework-ts)..."
  if [ -f "$FRAMEWORK_TS_DIR/package-lock.json" ]; then
    (cd "$FRAMEWORK_TS_DIR" && npm ci)
  else
    (cd "$FRAMEWORK_TS_DIR" && npm install)
  fi
fi

# Run builder from root workspace
(cd "$FRAMEWORK_TS_DIR" && npm -w builder run build -- "$INPUT_JSON")
