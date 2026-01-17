#!/bin/bash
# Devian Build System v10 - Build Script
# Usage: ./build.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
node "$SCRIPT_DIR/../framework-ts/tools/builder/build.js" "$SCRIPT_DIR/../input/build.json"
