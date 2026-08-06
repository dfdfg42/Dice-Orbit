# Project Overview

- **Game Title:** Dice-Orbit
- **High-Level Concept:** 주사위를 굴려 액션 예산을 만들고, 궤도형 전장에서 파티 캐릭터로 몬스터와 턴제 전투를 벌이는 로그라이트.
- **Players:** Single player (VS AI 몬스터)
- **Inspiration / Reference Games:** Slay the Spire류 덱/자원 관리 + 주사위 전투
- **Tone / Art Direction:** 크림색 종이(스코어시트) 테마의 아기자기한 보드게임풍 UI
- **Target Platform:** PC (StandaloneWindows64)
- **Screen Orientation / Resolution:** Landscape 1920×1080
- **Render Pipeline:** URP (PC_RPAsset)

# 목표 (This Task)

Figma 배틀 UI 목업(Frame 1, 1920×1080)을 `BattleScene`에 적용한다. 프로젝트에는 이미 대응되는 uGUI 시스템이 모두 존재하므로 **새로 만들지 않고 기존 UI를 Figma 레이아웃/테마에 맞춰 재배치·재스타일링**한다.

**확정된 결정 사항:**
- 폰트: 기존 **Pretendard-Regular SDF** 유지 (KOTRA HOPE 임포트 안 함)
- 범위: **전체 레이아웃 재배치** (Figma 위치/크기/색 최대한 재현)
- 하단 중앙 **로그/메시지 박스: 제외**
- Figma의 배경/초상화/액션 이미지 PNG는 목업 스크린샷이므로 **UI로 사용하지 않음** (게임 보드는 실시간 렌더링, 초상화는 런타임 `CharacterPreset.Portrait`/라이브 스프라이트로 채워짐)

# Figma → 기존 시스템 매핑

| Figma 요소 | 기존 owner (씬 오브젝트 / 스크립트) |
|---|---|
| 우측 캐릭터 정보 패널 (전사/HP/상태이상/대검/패시브 전우애) | `battle Info UI/InfoPanelCanvas/Panel` → `BattleInfoPanelUI` |
| 좌하단 파티 초상화 2개 + HP바 | `GameCanvas/PartyRosterUI` → `PartyRosterUI` + `PartyRosterEntryUI` 프리팹 |
| 우하단 액션 버튼(대검/이동/취소) + 액션 캐릭터 이미지 | `GameCanvas/CharacterActionPanel` → `CharacterActionUI` |
| 하단 중앙 "턴 종료" 버튼 | `GameCanvas/DicePanel/End Turn Button` → `CombatManager.endTurnButton` |
| 상단 Turn 표시 | `===Canvas/Canvas/Turn Count` → `CombatManager.turnCountText` |
| 크림색 둥근 패널 테마 | `UiRoundedSprite.Get(radius)` + `InfoPanelRows` 팔레트 + `BattleInfoPanelUI.paperColor` |
| 하단 중앙 로그 박스 (Rectangle 2) | **제외** |

# Game Mechanics

## Core Gameplay Loop
플레이어 턴 시작 → 주사위 굴림(`DiceUI`) → 캐릭터 선택 시 액션 패널(`CharacterActionUI`) 등장 → 주사위를 스킬(대검)/이동에 배정 → 대상 선택(`TargetSelectionOverlay`) → 실행 → 턴 종료(`CombatManager`) → 몬스터 턴 → 반복. 이번 작업은 이 루프의 **화면 표시(HUD)** 만 Figma 톤으로 재정렬하는 것이며 전투 로직/흐름은 변경하지 않는다.

## Controls and Input Methods
New Input System 기반, 마우스 클릭/드래그로 캐릭터·주사위·버튼 조작. (이번 작업에서 입력 로직 변경 없음.)

# UI

## Figma 좌표 → 캔버스(1920×1080, top-left 원점) 변환
Figma 프레임 원점: x=-111, y=102. 로컬X = FigmaX + 111, 로컬Y(top) = FigmaY − 102.

### 우측 캐릭터 정보 패널 (Panel, `BattleInfoPanelUI`)
- 패널 배경(Rectangle 1): 로컬 left≈1487, top≈15, **w=410, h=1040**, radius 25, cream `#FAF3E0` → 우측 도킹(오른쪽/상하 여백 ~20px)
- Header 슬롯 (좌측 패딩 ~x1541 기준, 패널 내부 상단):
  - `nameText` "전사" (ink `#4B425C`, ~40pt) + 이름 뒤 크림-딥 칩(Rectangle 5, `#E8DBC3`, radius 10)
  - `hpText` "HP 80/80" (red `#C53A3A`, ~35pt)
  - `flavorText`/상태이상 라인 "상태이상 없음" (muted `#B0B0B0`, ~25pt)
- `activesTitle`+`activesContainer` "대검" 섹션: 제목 칩(`#E8DBC3`, radius 10) + "주사위 4 이상 · 적 1명"(흰/밝은) + "주사위 4 이상 X 1피해"(muted)
- `passivesContainer` "패시브" 섹션: 제목 칩 + "전우애"(green `#839F8E`, ~35pt) + "좌우 1칸 아군 1명당 피해 +50%"(muted ~25pt)
- `statusesContainer`/`modifiersContainer`: 하단 슬롯 유지, Figma엔 값 없음 → 비어있을 때 숨김

### 좌하단 파티 초상화 (`PartyRosterUI` / `PartyRosterEntryUI`)
- 각 엔트리 = **140×140** 크림 프레임(radius 25) + 내부 132×132 초상화(`portraitImage`) + `hpSlider`/`hpText`
- 배치: `listRoot`를 좌하단 앵커, 가로 배열(HorizontalLayoutGroup), 첫 프레임 로컬 left≈54, top≈714 (Figma Rectangle 8/9)

### 우하단 액션 패널 (`CharacterActionPanel`, `CharacterActionUI`)
- `portraitImage`(액션 캐릭터, image_2 자리): 로컬 ~(1169, 764), 크기 ~318×352 (preserveAspect, 런타임 라이브 스프라이트 미러링 유지)
- 버튼 세로 스택(우측): `skillButton`("대검") ~y817, `moveButton`("이동") ~y927, `cancelButton`("취소") ~y1028, x≈1290
- ⚠ 슬라이드 애니메이션(`hiddenPosition`/`shownPosition`, anchoredPosition 기반) 값을 새 위치에 맞게 재계산 (안 하면 화면 밖으로 사라짐)

### 하단 중앙 "턴 종료" 버튼 (`CombatManager.endTurnButton`)
- 로컬 ~(845, 940), **w=141, h=57**, radius 21, 3중 레이어 퍼플 룩: 외곽 `#7678A1` / 중간 `#BABEDB` / 내부 `#9EA4C8`, 텍스트 흰색 ~30pt "턴 종료"

### 상단 Turn 표시 (`CombatManager.turnCountText`)
- 현재 `===Canvas/Canvas`가 ConstantPixelSize/800×600(레거시)라 1920×1080 스케일과 불일치 → 1920×1080 ScaleWithScreenSize 캔버스로 이동 권장. Figma엔 명시 위치 없음 → 상단 좌측 유지, 크림 톤 정리.

## 색상 팔레트 (Figma 기준, `InfoPanelRows` + `paperColor`에 반영)
- Cream panel: `#FAF3E0` → `(0.980, 0.953, 0.878)`
- Chip/section-title bg: `#E8DBC3` → `(0.910, 0.859, 0.765)`
- Ink dark: `#4B425C` → `(0.294, 0.259, 0.361)`
- HP red: `#C53A3A`
- Muted: `#B0B0B0`
- Passive green: `#839F8E`
- Turn button: outer `#7678A1`, mid `#BABEDB`, inner `#9EA4C8`

# Key Asset & Context

## 수정 대상 파일 (스크립트)
- `Assets/Scripts/UI/InfoPanel/InfoPanelRows.cs` — 팔레트 상수(`SectionTitleColor`, `PassiveColor`, `HpColor`, `MutedColor`, `InkDark` 등)를 Figma 값으로 조정. 섹션 제목을 **채워진 칩** 스타일로 렌더하도록 `AddSectionTitle`/`FormatSectionTitle` 보강.
- `Assets/Scripts/UI/InfoPanel/BattleInfoPanelUI.cs` — `paperColor`를 `#FAF3E0`로 조정. (레이아웃 슬롯 로직은 유지)
- `Assets/Scripts/UI/CharacterActionUI.cs` — `hiddenPosition`/`shownPosition`/버튼 스택 배치 상수 갱신 (필요 시). 로직 변경 없이 좌표만.
- `Assets/Scripts/Core/.../Combat/CombatManager.cs` — 코드 변경 최소화. `endTurnButton`/`turnCountText` 참조는 그대로, 스타일/위치는 씬에서 처리.

## 수정 대상 (씬 오브젝트, `BattleScene.unity`)
- `battle Info UI/InfoPanelCanvas/Panel` 및 하위 슬롯 RectTransform (우측 도킹 410×1040)
- `GameCanvas/PartyRosterUI/ListRoot` 앵커/위치, `PartyRosterEntryUI` 프리팹 프레임(140×140, radius 25)
- `GameCanvas/CharacterActionPanel` 하위 버튼/포트레이트 RectTransform
- `GameCanvas/DicePanel/End Turn Button` 위치/색상 레이어
- `===Canvas/Canvas/Turn Count` (캔버스 스케일 정합화)

## 재사용 유틸/컨벤션 (반드시 준수)
- 둥근 패널: `UiRoundedSprite.Get(radius)` + `Image.type = Sliced`, 색은 `Image.color` 틴트 (아트 에셋 불필요)
- 텍스트: 전부 `TextMeshProUGUI` + `Pretendard-Regular SDF`
- 스타일 상수는 `InfoPanelRows`에 중앙집중 — 하드코딩 지양
- 캔버스: `ScaleWithScreenSize`, RefRes `1920×1080`, match=0(width) 표준
- 싱글턴 + `EnsureInstance()`/`Instance` 패턴, `GameState.Combat` 게이팅 유지
- 반복 엔트리(파티/주사위/칩)만 프리팹 인스턴스화, 매니저 패널은 씬 배치 + `[ContextMenu("기본 레이아웃 생성")]` 스캐폴드

## 사용하지 않는 Figma 임포트 에셋
`Assets/UI/FigmaImport/BattleUI/`의 `asdfasdfasdf_1.png`(배경 스크린샷), `dfgsdfgsdfg_1/2.png`(초상화 목업), `image_2.png`(액션 이미지 목업)는 참고용. 최종 UI에는 사용하지 않음. `Frame_1_reference.png`는 시각적 레퍼런스로만 활용.

# Implementation Steps

### Step 1 — 팔레트/테마 상수 반영
- **Description:** `InfoPanelRows.cs` 팔레트 상수를 Figma 색으로 조정(cream/chip/ink/hp/muted/passive), `BattleInfoPanelUI.paperColor`를 `#FAF3E0`로 설정. 섹션 제목을 채워진 칩(`#E8DBC3`, radius 10) 스타일로 렌더하도록 helper 보강.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes (Step 4와 병행 가능)

### Step 2 — 우측 정보 패널 재배치/재스타일
- **Description:** `battle Info UI/InfoPanelCanvas/Panel`을 우측 도킹 410×1040(radius 25)으로 조정. Header(이름 칩+HP+상태), Actives(대검), Passives(전우애) 슬롯을 Figma 좌표/폰트 크기에 맞춰 정렬. 값 없는 modifiers/statuses는 비어있을 때 숨김 확인. 런타임에서 `Character.GetBattleInfo()`가 정상 채워지는지 검증.
- **Assigned role:** developer
- **Dependencies:** Step 1
- **Parallelizable:** No

### Step 3 — 좌하단 파티 로스터 재배치
- **Description:** `PartyRosterEntryUI` 프리팹을 140×140 크림 프레임(radius 25) + 132 초상화 + HP 슬라이더/텍스트로 스타일. `PartyRosterUI.listRoot`를 좌하단 앵커·가로 배열로 배치(첫 프레임 ~left54/top714). 런타임 파티 동기화/HP 갱신/hover→InfoPanel 동작 유지 확인.
- **Assigned role:** developer
- **Dependencies:** Step 1
- **Parallelizable:** Yes (Step 2와 병행 가능)

### Step 4 — 우하단 액션 패널 재배치
- **Description:** `CharacterActionPanel`의 `portraitImage`(~318×352)와 버튼 세로 스택(대검/이동/취소, x≈1290)을 Figma 위치로 배치. `hiddenPosition`/`shownPosition` 슬라이드 상수를 새 위치 기준으로 재계산(화면 밖 이탈 방지). 캐릭터 선택 시 등장/숨김·주사위 배정·취소 동작 검증.
- **Assigned role:** developer
- **Dependencies:** Step 1
- **Parallelizable:** No (슬라이드 좌표 검증 필요)

### Step 5 — 턴 종료 버튼 + 상단 Turn 표시 정리
- **Description:** `End Turn Button`을 하단 중앙(~845,940, 141×57, radius 21)으로 이동하고 3중 퍼플 레이어 스타일 적용, `CombatManager.endTurnButton` 참조 유지. `Turn Count`를 1920×1080 ScaleWithScreenSize 캔버스로 이동(또는 정합화)하여 스케일 일치. `turnCountText` 참조 유지.
- **Assigned role:** developer
- **Dependencies:** Step 1
- **Parallelizable:** No

### Step 6 — 통합 검증 및 정리
- **Description:** 전 캔버스 sortOrder 레이어링(정보패널 −5, 게임플레이 0, 턴안내 1000, HUD 1600) 충돌 없는지 확인. 중복 `PartyRosterUI`(root vs GameCanvas) 정리. 다양한 해상도/에디터 Game 뷰에서 앵커/여백 확인. Console 에러 0 확인.
- **Assigned role:** developer
- **Dependencies:** Steps 2–5
- **Parallelizable:** No

# Verification & Testing

- **Play Mode 전투 진입:** MainMenu → 캐릭터 선택 → BattleScene 진입 시 우측 정보 패널이 우측 도킹 크림 패널로 표시되고, 캐릭터 hover/선택 시 이름/HP/대검/전우애가 채워지는지.
- **파티 로스터:** 좌하단에 140×140 크림 프레임 파티 초상화 2개 + HP 바가 표시, 피해 시 HP 갱신, 사망 시 dim 처리, hover→정보패널 연동.
- **액션 패널:** 캐릭터 선택 시 우하단에서 슬라이드 인, 대검/이동/취소 버튼 동작, 취소 시 슬라이드 아웃(화면 밖 정확히 이탈), 라이브 초상화 미러링.
- **턴 종료/Turn 표시:** 턴 종료 버튼 클릭 → 몬스터 턴 진행, Turn 텍스트 "Turn: N" 증가 및 해상도 변경에도 위치 안정.
- **테마 일관성:** 모든 패널 `#FAF3E0` 크림 + radius, 텍스트 Pretendard, Figma 레퍼런스(`Frame_1_reference.png`)와 좌표/색 대조.
- **회귀:** 전투 흐름(주사위→배정→대상선택→실행→턴종료→몬스터턴) 정상, Console 에러/경고 없음.
- **엣지 케이스:** 파티원 1명/최대일 때 로스터 배치, 스킬/패시브가 없는 유닛일 때 섹션 숨김, 몬스터 선택 시 정보패널 intent 표시.
