# 주사위 덱(Dice Deck) 시스템

플레이어가 **영구 소유하는 주사위 덱**. 각 주사위는 커스텀 6면과 (선택) 사용 시 효과를 가지며,
매 턴 덱을 굴린다. 보상창에서 특수 주사위를 얻어 덱을 교체하고, 이벤트로 주사위에 효과를 부여할 수 있다.

> 설계/계획 기록: `Docs/superpowers/specs/2026-07-29-dice-deck-design.md`, `Docs/superpowers/plans/2026-07-29-dice-deck-phase1.md`
> 관련: [[run_structure_system]](맵/보상 흐름), 이벤트 결과 다형성은 `EventOutcomes.cs`와 동일 패턴.

## 개요

- 이전: 매 턴 익명 주사위(값 `Random 1~6`, 개수 = 파티×2)를 새로 생성.
- 현재: **`DiceDeckManager`가 소유한 덱**(각 칸 = `DieInstance`)을 매 턴 굴린다. 개수 = 덱 크기.

## 데이터 모델 (`Core/Stage/BattleStage/BattleStageSystem/Dices/`)

| 타입 | 파일 | 역할 |
|---|---|---|
| `DieDefinitionSO` | `DieDefinitionSO.cs` | 주사위 한 종류 = **에셋 1개** (`Create ▸ DiceOrbit ▸ Die Definition`). `Name`, `int[] Faces`(6), `[SerializeReference] DieEffect Effect`, `Sprite Face` + `Color NumberColor`(종류별 겉모습 — 아래 「종류별 면 그림」). `RollFace()` = 랜덤 면. saveId = 에셋 파일명(후속 저장용). |
| `DieInstance` | `DieDefinitionSO.cs` | **덱의 한 칸(런타임)** = `BaseDie`(SO) + `AttachedEffect`(선택). `Effect => AttachedEffect ?? BaseDie.Effect`. `Faces`/`RollFace()`는 BaseDie 위임. |
| `DieEffect` | `DieEffect.cs` | 사용 시 효과 **추상 다형성** (`[SerializeReference, SubclassPicker]`, 이벤트 결과/유물과 동일). `Apply(DieUseContext)` + `Preview()` + `Sprite Icon`. 구체: `GainGoldOnUse`, `HealUserOnUse`. |
| `DieUseContext` | `DieEffect.cs` | `{ Character User, int RolledValue }` (사용 확정 시 전달). |
| `DiceData` | `DiceData.cs` | 매 턴 굴린 주사위 1개. `Source`(→ `DieInstance`, 비직렬화) 추가. 값 클램프 제거(면 값이 권위). |

## 런타임 흐름

```
런 시작/모집 → DiceDeckManager.SyncDeckToParty(): standardDie ×(파티×diePerCharacter=2)
플레이어 턴 → DiceManager.RollDice(): Deck의 각 DieInstance.RollFace() → DiceData(value, Source)
호버       → DiceElement.OnPointerEnter → DiceHoverTooltipUI.Show
사용 확정   → CharacterActionUI.MarkDiceUsed → DiceManager.MarkUsed(dice, user)
             → State=Used + dice.Source.Effect?.Apply(ctx) (요약 로그)
턴 종료     → DiceManager.ResetDice (덱 자체는 유지)
```

- **`DiceDeckManager`** (`Dices/DiceDeckManager.cs`, 싱글턴): `Deck`(List\<DieInstance\>) 소유. 인스펙터 `standardDie`(시드), `specialPool`(보상 풀), `diePerCharacter`. API: `DrawRandomSpecial()`, `Replace(index, newBase)`, `AttachEffect(index, effect)`. 파티 모집(`OnPartyChanged`) 시 부족분만 표준 주사위로 채움(특수/부여분 보존).
- **`DiceManager`** (`Dices/DiceManager.cs`): `RollDice()`가 덱을 굴린다(구 파티×2 로직 제거). `MarkUsed(dice, user)`가 상태전환 + 효과 발동을 중앙에서 담당(구 `DiceUI` 직접 상태세팅 대체).

## 호버 UI (`UI/DiceHoverTooltipUI.cs`)

호버한 주사위 위에 표시. 씬에 패널 배치(`GameCanvas/DiceHoverTooltip`), 참조 배선 필요:
- `panel`(루트), `faceGrid`(GridLayoutGroup **3열**, 셀 44), `effectRow`(VerticalLayoutGroup, `reverseArrangement`=아래→위), `font`(Pretendard SDF).
- **면 그리드**: 6칸 = 그 주사위의 면 그림 + 숫자, 3×2.
- **효과 카드**: 효과별로 카드 셀 동적 생성(둥근 크림 카드 + 아이콘 + `Preview()`), 타일 정보 카드 감각.

## 종류별 면 그림 (2026-10-05)

주사위 종류마다 면이 다르게 생겼다 — 트레이에서 어떤 특수 주사위인지 한눈에 구분된다.

- **데이터**: `DieDefinitionSO.Face`(가운데가 빈 면 그림, 그 위에 숫자가 얹힌다) + `NumberColor`(어두운 면은 크림색 숫자).
- **그리는 곳은 한 군데**: `UI/DieFaceStyle.Apply(die, 몸통 Image, 숫자 TMP)`. 트레이(`DiceElement.SetDiceData`) · 호버 툴팁의 6면 · 보상 화면 주사위 카드(`RewardDieCard`)가 모두 이걸 부른다. 새 화면에서 주사위를 그릴 때도 이 한 줄.
  `Face`가 비어 있으면 에러를 한 번 남기고 그대로 둔다 (다른 주사위 그림으로 대체하지 않는다). 덱 출처가 없는 주사위(`DiceData.Source == null`)는 프리팹의 기본 면 그대로.
- **그림**: `Assets/Sprites/Dice/die_<에셋 이름>.png` 16장 (표준 + 특수 15종).

| 주사위 | 면 | | 주사위 | 면 |
|---|---|---|---|---|
| 표준 | 크림색 민무늬 | | 양극 | 흑백 대각 분할 + 가운데 회색 판 |
| 전진 | 주황 + 화살표 | | 정밀 | 강철색 + 눈금·십자선 |
| 연금 | 초록 유리 + 플라스크 | | 성벽 | 성돌 + 성가퀴 |
| 비전 | 짙은 보라 + 룬 | | 정제 | 얼음빛 수정 + 물방울 |
| 혼돈 | 분홍·청록 소용돌이 | | 그림자 | 검은 남색 + 보라 연기 |
| 집중 | 진홍 + 과녁 고리 | | 잔걸음 | 민트 + 발자국 |
| 거인 | 갈색 바위 | | 별빛 | 밤하늘 + 별·초승달 |
| 생명 | 연두 + 하트·새싹 | | 연성 | 살구색과 하늘색이 섞임 |

- **만드는 법**: 4×4 시트 한 장을 `python Tools/cut_die_faces.py <시트.png>`로 자른다 (칸 순서 = 도구의 `ORDER`). 면 그림 + 배선표 `die_faces.json`(숫자 색은 면 가운데 밝기로 자동 결정)이 나오고, Unity 메뉴 **DiceOrbit → Assign Die Faces**(`DieFaceSetup`)가 표대로 에셋에 꽂는다. 주사위 폴더에 표에 없는 주사위가 있으면 예외.
- **새 주사위를 더할 때**: 면 그림을 `Assets/Sprites/Dice/`에 넣고 `Face`·`NumberColor`를 직접 지정하거나, 시트를 다시 뽑아 `ORDER`에 이름을 더한다. 그림 없이 다른 면을 같이 쓰려면 도구의 `ALIASES`.

## 획득 · 교체 (`UI/Reward/RewardUI.cs`)

전투 보상의 **주사위 박자** — `RewardRoller`가 `DrawRandomSpecial()`로 특수 주사위 1개를 뽑는다(진입 시 1회). 왼쪽 "새 주사위" 카드에 이름·등급·6면·사용 효과·설명이 호버 없이 전부 보이고, 오른쪽 "내 덱" 카드에서 교체할 주사위를 고른 뒤 [교체] → `Replace(index, newDie)`. [받지 않기]로 넘길 수 있다. specialPool이 비면 박자 자체가 없다. 상세: [[reward_screen_system]].

## 이벤트 효과 부여 (`Core/Run/EventOutcomes.cs`)

`AttachDieEffectOutcome : EventOutcome` — 이벤트 선택지 결과. `[SerializeReference] DieEffect effect`를 담고, `Apply()` 시 **덱의 랜덤 주사위 하나**에 `AttachEffect`. (플레이어 선택 피커가 아닌 랜덤 — 이벤트의 즉시-동기 흐름에 맞춤.)

## 콘텐츠 저작 가이드

- **새 주사위**: `Create ▸ DiceOrbit ▸ Die Definition` → Faces/Effect/Face(면 그림)·NumberColor 지정. `DiceDeckManager.specialPool`(보상용) 또는 `standardDie`(시드용)에 배선.
- **새 사용 효과**: `DieEffect` 상속 클래스 하나 추가(Apply+Preview). enum/switch 없음.
- **이벤트로 효과 부여**: `EventDefinition` 에셋의 선택지 결과에 `AttachDieEffectOutcome` 추가 후 effect 지정.

## 이번 범위 밖 / as-built 메모

- **덱 저장 미구현**: 이어하기 시 파티 인원 기준 **기본 덱으로 복귀**(교체/부여분 손실). 세이브 리팩터(`saveId`/`SaveIdCatalog`) 도입 시 덱 = 주사위 saveId 목록으로 저장 예정.
- **Phase 3 랜덤 부여**: 이벤트 효과 부여는 랜덤 주사위 대상(플레이어 선택 피커는 후속 가능).
- **DiceModReward 제거**: 노드맵의 주사위 개조 예고 배지/필드 삭제(보상은 RewardUI로 이동).
- **일기예보 바이어스**: 굴린 값에 그대로 적용(커스텀 면 위에서도 동작).

## 파일 맵

- 신규: `Dices/{DieEffect,DieDefinitionSO,DiceDeckManager}.cs`, `UI/DiceHoverTooltipUI.cs`, `Core/Run/EventOutcomes.cs`의 `AttachDieEffectOutcome`
- 수정: `Dices/{DiceData,DiceManager}.cs`, `UI/{DiceElement,DiceUI,CharacterActionUI,RewardUI}.cs`, `Core/Run/{MapGraph,MapGenerator,ActDefinition}.cs`(DiceModReward 제거)
- 에셋: `Assets/Scripts/Data/Dices/*.asset`(DieDefinitionSO), 씬: `BattleScene`(DiceDeckManager, DiceHoverTooltip)
