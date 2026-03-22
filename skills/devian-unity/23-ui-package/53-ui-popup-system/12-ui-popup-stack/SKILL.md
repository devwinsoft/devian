# 12-ui-popup-stack — Stack Rules

Status: ACTIVE
AppliesTo: v1

## Purpose

popup stack의 push / pop / duplicate / top-state 규칙을 정리한다.

## Core Rules

- stack top만 입력 가능하다
- non-top popup은 stack에 남아 있지만 input inactive다
- top popup이 `Opened` 상태일 때만 input을 받는다
- `Opening` / `Closing` top popup 동안은 다른 popup input을 열지 않는다

## Duplicate Policy

### Allow
- 항상 새 popup을 push

### IgnoreIfOpened
- 같은 `PopupId`가 열려 있으면 새 요청 무시

### FocusIfOpened
- 기존 entry를 stack에서 제거한 뒤 다시 top으로 push
- open transition은 다시 재생하지 않음
- sibling order / top-only input / dim 상태를 전부 다시 계산

### ReplaceIfOpened
- 기존 popup을 `Replaced` reason으로 close 시작
- 새 popup은 별도로 open

## Close Rules

### CloseTop
- top entry만 close

### CloseAll
- top부터 역순 정리
- `ForceClosed` reason 사용
- transition skip

### CloseById
- v1 비지원
