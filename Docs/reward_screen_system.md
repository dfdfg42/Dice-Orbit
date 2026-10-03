# 보상 화면 시스템 — 전리품 시트 3박자

> 2026-10-03 전면 리워크. 설계 기록: `Docs/superpowers/specs/2026-10-03-reward-screen-rework-design.md`
> 관련: [[run_structure_system]](전투 → 보상 → 모집/맵 흐름), [[dice_deck_system]](주사위 교체), [[modifireSystem]](모디파이어 장착), [[editor_owned_ui_pattern]]

전투를 이기면 한 장의 시트가 뜨고, 세 박자로 진행한다.

```
① 전리품 (자동)   골드·유물·포션이 칩으로 붙는다. 클릭 없음 — 진입 즉시 수령.
② 강화 (결정)     공용 모디파이어 카드 3장 + 파티 초상 → [○○에게 장착]
③ 주사위 (결정)   새 주사위 카드 ↔ 내 덱 카드 → [교체] / [받지 않기]
→ 마지막 결정이 끝나면 도장 연출 후 자동으로 다음 상태 (모집 또는 맵)
```

## 규칙

| 항목 | 규칙 |
|------|------|
| 굴림 | 보상은 진입 시 `RewardRoller.Roll`이 **한 번만** 굴린다. 재굴림 경로 없음 |
| 골드 | `goldPerReward` + 유물 보너스. 즉시 지갑에 들어간다 |
| 유물 | 엘리트 노드에서만. 즉시 획득 |
| 포션 | `potionDropChance` 확률. 빈 슬롯이 있으면 즉시 획득, 가득 차면 "버릴 포션 고르기" 줄이 뜬다 (가진 포션을 누르면 그것을 버리고 받는다) |
| 모디파이어 | **공용 3장을 먼저 보고 대상을 고른다.** 제시 풀 = 생존 파티원 중 1명이라도 장착 가능한 종류 (`GetRandomChoicesForParty`) |
| 상태 줄 | 선택된 캐릭터 기준 `신규` / `중첩 n → n+1` / `최대 중첩` |
| 기본 대상 | 제시된 카드 중 하나라도 받을 수 있는 첫 캐릭터 |
| 강화 건너뛰기 | 두 번 눌러야 확정 (2초 안) |
| 주사위 | 받기 선택형. 교체할 덱 슬롯 필수 |
| 종료 | 결정 박자가 끝나면 자동 종료. 결정 박자가 없거나 못 받은 포션이 남으면 [계속] 박자를 거친다 |

클릭 수는 전투당 최대 4 (카드 · 장착 · 덱 주사위 · 교체).

## 구조

```
Core/Run/Reward/
  RewardBundle.cs    골드·유물·포션·모디파이어 제시·새 주사위 — 데이터만
  RewardRoller.cs    매니저들에서 묶음을 한 번 굴린다
  RewardFlow.cs      박자 순서와 종료 규칙 — 순수 C# (자가 테스트)
UI/Reward/
  RewardUI.cs        씬 슬롯 + 박자 렌더링. GameFlowManager는 Show()/Hide()만 부른다
  RewardLootChip · RewardStepPip · RewardModifierCard · RewardPartyPortrait · RewardDieCard
                     씬 템플릿에 붙는 뷰 — Bind + 상태 표시만, 규칙은 RewardUI
  RewardHoverInfo.cs 호버 설명 프록시 (HoverTooltipUI)
UI/UiMotion.cs       코루틴 미니 모션 (팝인·페이드·카운트업·도장, unscaled time)
Editor/RewardUiScaffold.cs      [DiceOrbit/Rebuild Reward UI Layout]
Editor/RewardFlowSelfTests.cs   [DiceOrbit/Run Reward Flow Self-Tests]
```

- `RewardFlow.Current` = `Upgrade` → `Dice` → (`Wrap`) → `Finished`. `HasBlockedLoot`은 RewardUI가 포션 수령 상태에 맞춰 갱신한다.
- 선택 상태가 유지되는 카드·초상은 `UiSkin.ApplyButtonSelected(button, role, selected)` — 선택이면 Pressed 스프라이트를 바탕으로 쓴다.
- 모디파이어 카드의 아이콘은 계열 아이콘 4종 (`ModifierFamily` → `UiSkin.GetFamilyIcon`). 계열이 `None`인 모디파이어는 보상 카드로 제시할 수 없다 (예외).

## 씬 계층 (`RewardUI/RewardCanvas`)

```
Backdrop                스크림
Sheet (1320×920)        Frame · Title · StepRow · LootRow · DiscardRow
  UpgradeBody           Prompt · CardRow · PartyRow
  DiceBody              Prompt · DiceRow( NewDieCard · Arrow · DeckRow )
  WrapBody              Message
  Hint · SecondaryButton · PrimaryButton · Stamp
Templates (비활성)      LootChip · StepPip · ModifierCard · PartyPortrait · DieCard
```

- 스타일은 씬이 소유한다. 계층을 처음부터 다시 만들려면 `RewardCanvas`의 자식을 지우고 `[DiceOrbit/Rebuild Reward UI Layout]`.
- 스캐폴드는 `Sheet`가 이미 있으면 덮어쓰지 않는다 (씬에서 다듬은 스타일 보호).
- 슬롯이 비면 `RewardUI.Show()`가 에러를 내고 멈춘다 — 런타임 생성 폴백은 없다.

## 레이아웃 메모

- 선택 카드 스프라이트(`btn_reward_choice_card_*`)는 선택 테두리·리본만큼 캔버스 여백이 있다 (위 약 17, 옆·아래 약 12 + 안쪽 테두리선 약 26). 카드 내용은 그 안쪽에 둔다. 리본은 우상단 — 이름은 리본 아래에서 시작한다.
- 작은 덱 카드는 `RewardDieCard.borderScaleOverride`(3.2)로 9-slice 경계 배수를 키워 테두리·선택 리본을 카드 크기에 맞게 가늘게 한다 (0 = 스킨 카탈로그 값 2.37).
- `DiceRow`는 부모가 자식의 선호 폭으로 배치한다 (`childControlWidth = true`). 자식에 `ContentSizeFitter`를 두면 부모가 갱신 전 폭으로 배치해 덱 줄이 새 주사위 카드와 겹친다.
- 박자 몸통은 **먼저 켠 뒤 채운다** (`OpenBody` → `Build…`). 켜지는 순간 `UiSkinButton.Awake`가 스프라이트를 보통 상태로 되돌리기 때문.
- "버릴 포션 고르기" 줄은 박자 안내문과 같은 자리를 쓴다 — 줄이 나와 있으면 안내문을 숨긴다.

## 확장

| 하고 싶은 것 | 손댈 곳 |
|--------------|---------|
| 전리품 종류 추가 | `RewardBundle` 필드 + `RewardRoller.Roll` + `RewardUI.ClaimLoot/BuildLootChips` |
| 결정 박자 추가 | `RewardBeat` + `RewardFlow` 생성자 + `RewardUI.EnterBeat`의 분기 + 스캐폴드에 몸통 |
| 모디파이어 계열 추가 | `ModifierFamily` + `Label()` + `UiSkin` 아이콘 필드·`GetFamilyIcon` + 점검기 |
| 수치 조정 | `RewardUI` 인스펙터 (`goldPerReward`, `modifierChoiceCount`, `potionDropChance`) |

## 검증

- `[DiceOrbit/Run Reward Flow Self-Tests]` — 박자 4조합, 자동 종료 vs [계속], 제시 풀(중복 없음·부적격 제외·빈 파티), 계열 지정 전수.
- `[도구/Dice Orbit/UI 스킨 점검]` 이슈 0.
- Play 확인 포인트: 전리품 칩 등장 → 카드 선택/대상 전환(최대 중첩 캐릭터는 흐림) → 장착 도장 → 주사위 교체/받지 않기 → 다음 상태.
