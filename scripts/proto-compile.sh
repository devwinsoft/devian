#!/bin/bash
# Devian Proto Compile Script
# 
# [DEPRECATED] 이 스크립트는 현재 사용되지 않습니다.
# proto-manual 폴더는 폐기되었습니다.
# IDL(.proto)은 input/{Domain}/protocols/{Domain}.proto 단일 파일만 사용합니다.
#
# Domain Root (정본):
# - Devian에서 Domain은 디렉터리 이름이 아니라 논리 단위이다
# - Data domain: contracts + tables (Common)
# - Protocol-only domain: protocols 단일 파일 (C2Game, Game2C)
#
# 사전 요구사항:
# - protoc (Protocol Buffers Compiler) 설치
# - Google.Protobuf NuGet 패키지 (C# 프로젝트에서 참조)

echo "[DEPRECATED] This script is no longer used."
echo "proto-manual folders have been removed."
echo "Use build.json v8 schema with protocolsDir/protocolFile instead."
exit 0
