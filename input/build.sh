#!/bin/bash
# Devian Build System v10 - Build Script
# Usage: ./build.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS_DIR="$SCRIPT_DIR/../framework-ts/tools"

# Bootstrap: install dependencies if node_modules missing
if [ ! -d "$TOOLS_DIR/node_modules" ]; then
  echo "[Bootstrap] Installing dependencies..."
  if [ -f "$TOOLS_DIR/package-lock.json" ]; then
    (cd "$TOOLS_DIR" && npm ci)
  else
    (cd "$TOOLS_DIR" && npm install)
  fi
fi

node "$SCRIPT_DIR/../framework-ts/tools/builder/build.js" "$SCRIPT_DIR/../input/input_common.json"
