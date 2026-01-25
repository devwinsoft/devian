#!/usr/bin/env python3
"""
Devian Project Archive Tool
프로젝트 전체를 zip 파일로 아카이브합니다.

Usage:
    python archive_project.py [--root PATH] [--output PATH] [options]
"""

import argparse
import fnmatch
import os
import re
import zipfile
from datetime import datetime
from pathlib import Path


# 내장 제외 목록
BUILTIN_EXCLUDES = [
    '.git',
    '.git/**',
    'node_modules',
    'node_modules/**',
    '__pycache__',
    '__pycache__/**',
    '*.pyc',
    '.DS_Store',
    '*.zip',  # 기존 아카이브 제외
]


def parse_gitignore(gitignore_path: Path) -> list[str]:
    """
    .gitignore 파일을 파싱하여 패턴 목록을 반환합니다.
    """
    patterns = []
    
    if not gitignore_path.exists():
        return patterns
    
    with open(gitignore_path, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            
            # 빈 줄, 주석 무시
            if not line or line.startswith('#'):
                continue
            
            # 부정 패턴(!)은 현재 지원하지 않음 - 무시
            if line.startswith('!'):
                continue
            
            patterns.append(line)
    
    return patterns


def matches_pattern(path: str, pattern: str) -> bool:
    """
    경로가 gitignore 스타일 패턴과 일치하는지 확인합니다.
    """
    # 패턴 정규화
    pattern = pattern.rstrip('/')
    
    # 디렉토리 패턴 (끝에 /가 있었던 경우)
    is_dir_pattern = pattern.endswith('/')
    if is_dir_pattern:
        pattern = pattern[:-1]
    
    # ** 패턴 처리
    if '**' in pattern:
        # **/ -> 임의의 디렉토리
        regex_pattern = pattern.replace('**/', '(.*/)?')
        regex_pattern = regex_pattern.replace('**', '.*')
        regex_pattern = regex_pattern.replace('*', '[^/]*')
        regex_pattern = regex_pattern.replace('?', '[^/]')
        regex_pattern = f'^{regex_pattern}$'
        
        try:
            if re.match(regex_pattern, path):
                return True
        except re.error:
            pass
    
    # 경로의 각 부분과 패턴 비교
    path_parts = path.split('/')
    
    # 패턴이 /로 시작하면 루트 기준
    if pattern.startswith('/'):
        pattern = pattern[1:]
        return fnmatch.fnmatch(path, pattern)
    
    # 패턴에 /가 없으면 모든 레벨에서 매칭
    if '/' not in pattern:
        for part in path_parts:
            if fnmatch.fnmatch(part, pattern):
                return True
        # 전체 경로와도 비교
        return fnmatch.fnmatch(path, pattern) or fnmatch.fnmatch(path, f'**/{pattern}')
    
    # 패턴에 /가 있으면 상대 경로로 매칭
    return fnmatch.fnmatch(path, pattern) or fnmatch.fnmatch(path, f'**/{pattern}')


def should_exclude(rel_path: str, patterns: list[str], is_dir: bool = False) -> bool:
    """
    경로가 제외 대상인지 확인합니다.
    """
    # 디렉토리인 경우 끝에 / 추가하여 비교
    check_path = rel_path
    
    for pattern in patterns:
        if matches_pattern(check_path, pattern):
            return True
        
        # 디렉토리인 경우 하위 경로도 체크
        if is_dir:
            if matches_pattern(check_path + '/', pattern):
                return True
    
    return False


def find_project_root(start_path: Path) -> Path:
    """
    build.json 또는 .git을 찾아 프로젝트 루트를 결정합니다.
    """
    current = start_path.resolve()
    
    while current != current.parent:
        # build.json이 input/ 안에 있을 수 있음
        if (current / 'input' / 'build.json').exists():
            return current
        if (current / 'build.json').exists():
            return current
        if (current / '.git').exists():
            return current
        if (current / 'skills').exists():
            return current
        current = current.parent
    
    return start_path.resolve()


def collect_files(
    root: Path,
    exclude_patterns: list[str],
    exclude_Generated: bool = False,
    exclude_data: bool = False,
    include_temp: bool = False
) -> list[Path]:
    """
    아카이브할 파일 목록을 수집합니다.
    """
    files = []
    
    # 추가 제외 패턴
    extra_excludes = list(exclude_patterns)
    
    if exclude_Generated:
        extra_excludes.extend(['**/Generated', '**/Generated/**'])
    
    if exclude_data:
        extra_excludes.extend(['**/*.ndjson'])
    
    if not include_temp:
        extra_excludes.extend(['temp', 'temp/**', '**/temp', '**/temp/**'])
    
    for dirpath, dirnames, filenames in os.walk(root):
        rel_dir = os.path.relpath(dirpath, root)
        if rel_dir == '.':
            rel_dir = ''
        
        # 제외할 디렉토리 필터링 (in-place 수정으로 하위 탐색 방지)
        excluded_dirs = []
        for dirname in dirnames:
            if rel_dir:
                dir_rel_path = f'{rel_dir}/{dirname}'
            else:
                dir_rel_path = dirname
            
            if should_exclude(dir_rel_path, extra_excludes, is_dir=True):
                excluded_dirs.append(dirname)
        
        for dirname in excluded_dirs:
            dirnames.remove(dirname)
        
        # 파일 수집
        for filename in filenames:
            if rel_dir:
                file_rel_path = f'{rel_dir}/{filename}'
            else:
                file_rel_path = filename
            
            if not should_exclude(file_rel_path, extra_excludes):
                files.append(Path(dirpath) / filename)
    
    return files


def create_archive(
    root: Path,
    files: list[Path],
    output_dir: Path
) -> Path:
    """
    zip 아카이브를 생성합니다.
    """
    # 파일명 생성
    timestamp = datetime.now().strftime('%Y%m%d-%H%M%S')
    archive_name = f'devian-{timestamp}.zip'
    archive_path = output_dir / archive_name
    
    # zip 생성 (빠른 압축: level=1)
    with zipfile.ZipFile(archive_path, 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as zf:
        for file_path in files:
            rel_path = file_path.relative_to(root)
            zf.write(file_path, rel_path)
    
    return archive_path


def main():
    parser = argparse.ArgumentParser(
        description='Devian 프로젝트를 zip 파일로 아카이브합니다.'
    )
    parser.add_argument(
        '--root',
        type=Path,
        default=None,
        help='프로젝트 루트 경로 (기본: 자동 탐지)'
    )
    parser.add_argument(
        '--output',
        type=Path,
        default=None,
        help='출력 디렉토리 (기본: 프로젝트 루트)'
    )
    parser.add_argument(
        '--exclude-Generated',
        action='store_true',
        help='Generated 폴더 제외'
    )
    parser.add_argument(
        '--exclude-data',
        action='store_true',
        help='ndjson 데이터 파일 제외'
    )
    parser.add_argument(
        '--include-temp',
        action='store_true',
        help='temp 폴더 포함'
    )
    
    args = parser.parse_args()
    
    # 프로젝트 루트 결정
    if args.root:
        root = args.root.resolve()
    else:
        root = find_project_root(Path.cwd())
    
    print(f'프로젝트 루트: {root}')
    
    # 출력 디렉토리
    output_dir = args.output.resolve() if args.output else root
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # 제외 패턴 수집
    exclude_patterns = list(BUILTIN_EXCLUDES)
    
    # .gitignore 파싱
    gitignore_path = root / '.gitignore'
    if gitignore_path.exists():
        gitignore_patterns = parse_gitignore(gitignore_path)
        exclude_patterns.extend(gitignore_patterns)
        print(f'.gitignore에서 {len(gitignore_patterns)}개 패턴 로드')
    
    # 파일 수집
    print('파일 수집 중...')
    files = collect_files(
        root,
        exclude_patterns,
        exclude_Generated=args.exclude_Generated,
        exclude_data=args.exclude_data,
        include_temp=args.include_temp
    )
    
    print(f'수집된 파일: {len(files)}개')
    
    if not files:
        print('아카이브할 파일이 없습니다.')
        return
    
    # 아카이브 생성
    print('아카이브 생성 중...')
    archive_path = create_archive(root, files, output_dir)
    
    # 결과 출력
    size_mb = archive_path.stat().st_size / (1024 * 1024)
    print(f'완료: {archive_path}')
    print(f'크기: {size_mb:.2f} MB')


if __name__ == '__main__':
    main()
