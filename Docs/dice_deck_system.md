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
| `DieDefinitionSO` | `DieDefinitionSO.cs` | 주사위 한 종류 = **에셋 1개** (`Create ▸ DiceOrbit ▸ Die Definition`). `Name`, `int[] Faces`(6), `[SerializeReference] DieEffect Effect`, `Sprite Icon`. `RollFace()` = 랜덤 면. saveId = 에셋 파일명(후속 저장용). |
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
- `panel`(루트), `faceGrid`(GridLayoutGroup **3열**, 셀 44), `effectRow`(VerticalLayoutGroup, `reverseArrangement`=아래→위), `dieFaceSprite`(=`Assets/Sprites/Dice.png`), `font`(Pretendard SDF).
- **면 그리드**: 6칸 = 주사위 이미지 + 숫자(진한 잉크), 3×2.
- **효과 카드**: 효과별로 카드 셀 동적 생성(둥근 크림 카드 + 아이콘 + `Preview()`), 타일 정보 카드 감각.

## 획득 · 교체 (`UI/RewardUI.cs`)

전투 보상창에 **주사위 보상 행** 추가 — `DrawRandomSpecial()`로 특수 주사위 제시. 행 클릭 시 캐릭터 강화와 같은 패널을 재사용해 **현재 덱 슬롯을 나열 → 선택 슬롯을 `Replace`**. 받기 선택형(안 받으면 행 유지), 교체 슬롯 필수. specialPool이 비면 행이 안 뜬다.

## 이벤트 효과 부여 (`Core/Run/EventOutcomes.cs`)

`AttachDieEffectOutcome : EventOutcome` — 이벤트 선택지 결과. `[SerializeReference] DieEffect effect`를 담고, `Apply()` 시 **덱의 랜덤 주사위 하나**에 `AttachEffect`. (플레이어 선택 피커가 아닌 랜덤 — 이벤트의 즉시-동기 흐름에 맞춤.)

## 콘텐츠 저작 가이드

- **새 주사위**: `Create ▸ DiceOrbit ▸ Die Definition` → Faces/Effect/Icon 지정. `DiceDeckManager.specialPool`(보상용) 또는 `standardDie`(시드용)에 배선.
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
