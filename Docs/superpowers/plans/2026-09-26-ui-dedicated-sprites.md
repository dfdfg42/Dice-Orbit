# UI 전용 스프라이트 전환 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 재활용되던 스킨 부품 8종을 역할별 전용 스프라이트 48장(버튼 19시트 포함)으로 바꾸고 별 장식을 없앤다.

**Architecture:** `UiSkin`을 역할 파트 카탈로그(`SkinPart`/`SkinButton` 열거 + 항목 목록)로 재구성하고, 씬은 `UiSkinImage.part`/`UiSkinButton.button`으로만 스킨을 참조한다. 스프라이트는 힉스필드 `gpt_image_2_5`로 화면 단위 초안→승인→고해상, PIL 후처리(트림·시트 절단·9-slice 경계)로 임포트.

**Tech Stack:** Unity 6 uGUI, UiSkin ScriptableObject, Unity MCP RunCommand(씬 배선·캡처), Higgsfield MCP, Python PIL.

## Global Constraints

- 스타일: 크림 종이 `#F8ECD4` + 잉크 외곽선 `#3B3652`, 별·반짝이 금지, 글자 없음 (스펙 §3).
- 폴백 금지: 파트가 비면 `LogError`, 조용히 다른 스프라이트로 대체하지 않는다.
- 레이아웃 그룹 오브젝트에 스킨 Image를 같이 두지 않는다(`editor_owned_ui_pattern.md` 체크리스트 7).
- 신규 스프라이트 임포트: Mipmap + Trilinear.
- 브랜치 `feature/monster-zone-intent-20260925`에서 진행, 묶음마다 커밋, 병합하지 않음.

---

### Task 0: UiSkin 역할 카탈로그 리팩터

**Files:** Modify `Assets/Scripts/UI/Skin/UiSkin.cs`, `UiSkinImage.cs`; Create `UiSkinButton.cs`; Modify `Assets/Scripts/Editor/UiSkinValidator.cs`, `UiSkinSelfTests.cs`; 호출부 12파일(스펙 §4).

- [x] `SkinPart`/`SkinButton` 열거 + `SkinEntry`/`SkinButtonEntry` 목록 + `Apply(Image, SkinPart)`/`ApplyButton(Button, Image, SkinButton)` 작성. 기존 필드·헬퍼 삭제.
- [x] `UiSkinButton` 컴포넌트(에디터 소유 패턴): `OnValidate`/`Awake`에서 `SpriteState` 적용.
- [x] 검증기·자가 테스트를 열거 전수로 갱신 → `UiSkinSelfTests.RunAll` 통과.
- [x] 호출부를 역할 파트로 교체. 과도기: 새 파트에 임시로 기존 스프라이트를 채워 컴파일·플레이 유지(검증기는 "임시" 표시).
- [x] 커밋 `refactor(ui-skin): 역할 카탈로그`.

### Task 1: 전투 묶음 (14장)

- [x] 참조 크롭 업로드(`refs/ded_battle_*.png`) → 초안 low 1k ×2 (14건, 배치 4~5건씩) → 사용자 승인(번호 확답).
- [x] 최종 high 2k 투명 → `tools/finalize_ui_sprite.py`(트림·최대 변·9-slice 경계) / 버튼 `tools/cut_button_sheet.py`.
- [x] `UiSkin.asset` 배선 + 씬 배선(RunCommand: Simple/9V/9H·rect 재계산·UiSkinButton) → 투어 캡처 `review/ded_battle_*.png` → 콘솔 0 에러.
- [x] 생성 로그 행 추가, 커밋 `feat(ui): 전투 전용 스프라이트`.

### Task 2: 모집 묶음 (5장) — Task 1과 같은 4단계.
### Task 3: 노드맵·보상 묶음 (9장) — 동일.
### Task 4: 상점·이벤트·결과·튜토리얼 묶음 (14장) — 동일.
### Task 5: 메인메뉴 버튼 4시트 — 동일 (MainMenu 씬).

### Task 6: 정리

- [x] 구 스프라이트(`panel/card/chip/tooltip/slot/divider/button_*/intent_bubble`, `UI 이미지/주사위 패널·패널 1차`) 참조 0 확인 → `git rm`.
- [x] `UiSkin.IntentBubble` 파트 제거, 검증기 0 이슈.
- [x] 스펙 상태 "구현됨", README 행, 생성 로그 소계, 메모리 갱신. 커밋 `docs(ui)`.
