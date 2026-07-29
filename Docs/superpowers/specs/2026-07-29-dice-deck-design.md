# 주사위 덱(Dice Deck) 설계

**날짜:** 2026-07-29 (개정 — 보상 획득·교체 반영)
**브랜치:** feature/event-outcomes-refactor-20260729 (이 위에서 이어 작업)
**구현 단계:** Phase 1 덱 토대 → Phase 2 보상 획득·교체 (아래 §10).

## 1. 목표

플레이어가 **영구 소유하는 주사위 덱**을 도입한다.
- 각 주사위는 **커스텀 6면**(면마다 임의 정수, 예 `1 1 2 2 3 3`)과 **사용 시 효과(선택)** 를 가진다.
- 주사위는 **디자인된 에셋**(`DieDefinitionSO`)이며, 표준 주사위 + 특수 주사위 풀로 구성.
- **호버 UI**: 주사위에 마우스를 올리면 위에 6면(2줄×3, 실제 주사위 이미지)과 효과를 표시.
- **보상 획득·교체**: 전투 보상창에서 특수 주사위를 랜덤 제시 → 현재 덱의 주사위 하나와 **교체**.

현재는 매 턴 익명 주사위(값 `Random 1~6`, 개수 = 파티×2)를 새로 굴린다. 이 설계는 그것을 **소유 덱 굴림**으로 바꾼다.

## 2. 확정된 결정

- **주사위 = SO 에셋** `DieDefinitionSO`. 표준·특수 모두 에셋. 개별 개조가 아니라 **교체(스왑)** 라 런타임 변형 불필요 → SO가 적합. `saveId`(에셋 파일명)로 후속 저장과 정합.
- **기본 덱 = 캐릭터당 표준 주사위 2개.** 모집 시 +2. 소유·풀링되어 함께 굴림(매 턴 재계산 아님). 기존 `usePartyBasedDiceCount`/`dicePerCharacter`/`RefreshDiceCountFromParty`/`HandlePartyChanged` 제거.
- **덱 변형 = 교체만.** 보상으로 얻은 주사위를 기존 슬롯과 스왑(덱 크기 유지). 모집만 +2.
- **효과 발동 = `Used` 확정 시점**(`Reserved`(타겟팅 중, 취소 가능) 아님). `DiceUI`의 직접 `State=Used`를 `DiceManager.MarkUsed`로 중앙화.
- **효과 컨텍스트 최소** — 사용 캐릭터 + 굴린 값. 나중에 확장.
- **효과 다형성 = `[SerializeReference, SubclassPicker]`** — `EventOutcome`·`RuntimeArtifact`와 동일.
- **면 값이 권위** — `DiceData` `1~6` 하드 클램프 완화. 스킬 조건(`DiceRequirement`)은 값만 보므로 그대로 동작.
- **호버 면 그리드 = 실제 주사위 이미지** — 각 칸 = `Assets/Sprites/Dice.png` + 면 값(미니 주사위), 2줄×3.
- **덱 저장은 이번 범위 밖.** 이어하기 시 기본 덱(2/캐릭터)으로 복귀 — 교체분 손실 감수. 세이브 리팩터 도입 때 `saveId` 목록으로 저장 통합.

## 3. 데이터 모델

```csharp
// 주사위 한 종류 = 에셋 1개 (표준/특수 모두). 위치: Core/.../Dices/
[CreateAssetMenu(menuName = "DiceOrbit/Die Definition")]
public class DieDefinitionSO : ScriptableObject
{
    public string Name = "표준 주사위";
    public int[] Faces = {1,2,3,4,5,6};              // 길이 6
    [SerializeReference, SubclassPicker] public DieEffect Effect; // null = 효과 없음
    public Sprite Icon;                              // 보상/교체 목록 표시용
    public int RollFace() => Faces[Random.Range(0, Faces.Length)];
    // saveId = 에셋 파일명 (후속 저장 — 세이브 리팩터 SaveIdCatalog와 정합)
}

[Serializable]
public abstract class DieEffect
{
    public Sprite Icon;                              // 호버 효과 행 아이콘(선택)
    public abstract string Apply(DieUseContext ctx); // 발동 + 요약 반환
    public virtual string Preview() => "";           // 호버 라벨용 짧은 설명
}
public class DieUseContext { public Character User; public int RolledValue; }
// 증명용 구체 효과: GainGoldOnUse{amount}, HealUserOnUse{amount}
```

`DiceData` 확장 — 기존(`id, value, state, assignedCharacter`) 유지 + 추가:
- `DieDefinitionSO Source`(비직렬화 런타임 참조) — 효과 발동 + 호버 표시용.
- 굴림 시 `value = Source.RollFace()`. 하드 `1~6` 클램프 완화.

## 4. 덱 소유 · 굴림 · 사용 효과 (Phase 1)

**`DiceDeckManager` (신규 싱글턴 — `ArtifactManager`/`PotionManager`와 대칭)**
- `List<DieDefinitionSO> Deck` — 런타임 소유 덱.
- 인스펙터: `DieDefinitionSO standardDie`(시드용 표준), `List<DieDefinitionSO> specialPool`(보상 풀).
- **시드**: 캐릭터 모집(`PartyManager.OnPartyChanged`/모집 이벤트) 시 `standardDie` 2개를 `Deck`에 추가. 런 시작/이어하기 시 파티 인원 기준으로 재시드. (캐릭터 사망 시 주사위 유지 — 덱은 소유물. 필요 시 조정.)
- 보상용: `DieDefinitionSO DrawRandomSpecial()`, `void Replace(int deckIndex, DieDefinitionSO newDie)`.

**`DiceManager.RollDice` 변경**
- 익명 `Random 1~6` 대신 **`DiceDeckManager.Deck`를 굴림**: 각 SO마다 `value = def.RollFace()`, `Source = def`인 `DiceData` 생성. 파티×2 로직 제거. 포캐스트 바이어스·`RerollAvailableDice`는 굴린 값에 그대로 동작.

**사용 효과 발동 (중앙화)**
- 신규 `DiceManager.MarkUsed(DiceData dice)`: `State = Used` + `dice.Source?.Effect?.Apply(new DieUseContext{User=…, RolledValue=dice.Value})`. 요약은 기존 `FloatingLabelPopup`(있으면)으로 표시하거나 로그 — 표시 자체는 선택.
- `DiceUI`의 직접 `State=Used`(현 `DiceUI.cs:161`) → 이 메서드 호출로 교체. `CharacterActionUI`(현 `:568` `MarkDiceAsUsed`)가 최종 경로.

## 5. 호버 UI (Phase 1)

**`DiceElement`에 호버 핸들러** — `IPointerEnterHandler`/`IPointerExitHandler`(이미 `IPointerClickHandler` → New Input System EventSystem 경유 동작). Enter→`DiceHoverTooltipUI.Show(this)`, Exit→`Hide()`.

**`DiceHoverTooltipUI` (신규 전용 패널)** — 호버한 주사위 **바로 위**:
- **면 그리드**: 2줄×3 = `Source.Faces` 6개. 각 칸 = `Dice.png` 배경 + 면 값(미니 주사위).
- **효과 행**: `Source.Effect`가 있으면 `Effect.Icon`(있으면) + `Effect.Preview()`. 없으면 행 숨김.
- 위치: 호버 `DiceElement` 위. 싱글턴 + 최상단 정렬은 `HoverTooltipUI` 방식 차용.

## 6. 보상 획득 · 교체 (Phase 2)

- **`DiceModReward` 노드 예고 제거**: `MapGenerator`가 `DiceModReward`를 세팅하지 않음(관련 `DiceModBattleCount` 설정·로직 제거), `NodeMapUI`의 예고 아이콘 제거, `MapNode.DiceModReward` 필드 제거.
- **RewardUI 확장**: 전투 보상창에 **주사위 보상 섹션** 추가 — `DiceDeckManager.DrawRandomSpecial()`로 특수 주사위 1개 제시(아이콘/이름/면/효과 미리보기).
  - **받기 = 선택**(안 받으면 덱 유지).
  - **받으면**: 현재 덱을 나열하는 **교체 선택 UI** → 플레이어가 슬롯 하나 선택 → `DiceDeckManager.Replace(index, newDie)` (덱 크기 유지).
  - 기본값: 주사위 보상은 매 전투 보상에 함께 제시(골드·모디파이어와 나란히).

## 7. 데이터 흐름

```
런 시작/모집 → DiceDeckManager.Deck = standardDie ×(파티×2), 특수 주사위는 교체로 편입
플레이어 턴 → DiceManager.RollDice(): Deck의 각 SO.RollFace() → DiceData(value, Source)
호버 → DiceElement.OnPointerEnter → DiceHoverTooltipUI: Source.Faces(2×3) + Effect.Preview()
사용 확정 → CharacterActionUI → DiceManager.MarkUsed → State=Used + Source.Effect?.Apply(ctx)
턴 종료 → ResetDice (덱 유지)
전투 보상 → RewardUI: DrawRandomSpecial() → (받기) 교체 UI → DiceDeckManager.Replace(index,new)
```

## 8. 이번 범위 밖 (후속)

- **덱 저장/이어하기.** 이번엔 미구현 — 이어하기 시 기본 덱 복귀(교체 손실). 세이브 리팩터(`Docs/superpowers/specs/2026-07-27-save-system-refactor-design.md`) 도입 때: 덱 = **각 주사위의 `saveId`(에셋 파일명) 목록**으로 저장, `SaveIdCatalog`로 복원. `faces`는 에셋에 있으니 별도 저장 불필요, `[SerializeReference]` 문제 없음.
- **상점에서 주사위 구매**, 주사위 제거/추가(교체 외), 면 개조 등.

## 9. 영향 파일

**Phase 1 — 신규**
- `Core/.../Dices/DieDefinitionSO.cs` (+ `DieEffect`, `DieUseContext`, 구체 효과 1~2)
- `Core/.../Dices/DiceDeckManager.cs`
- `UI/DiceHoverTooltipUI.cs`

**Phase 1 — 수정**
- `Core/.../Dices/DiceData.cs` — `Source`, 클램프 완화
- `Core/.../Dices/DiceManager.cs` — 덱 굴림, 파티×2 제거, `MarkUsed`
- `UI/DiceElement.cs` — 호버 핸들러
- `UI/DiceUI.cs` — `State=Used` → `MarkUsed` 경유
- `UI/CharacterActionUI.cs` — 최종 사용 경로가 `MarkUsed`

**Phase 2 — 수정**
- `UI/RewardUI.cs` — 주사위 보상 + 교체 UI
- `Core/Run/MapGenerator.cs` — `DiceModReward`/`DiceModBattleCount` 제거
- `UI/NodeMapUI.cs` — 예고 아이콘 제거
- `Core/Run/MapGraph.cs` — `MapNode.DiceModReward` 필드 제거

**에디터 셋업(코드 밖)**
- `DieDefinitionSO` 에셋: 표준 주사위 1 + 특수 주사위 몇 개(면/효과).
- `DiceDeckManager`(standardDie·specialPool 배선) + `DiceHoverTooltipUI` 씬 배치.

## 10. 구현 단계

- **Phase 1 (덱 토대):** §3~5 — 데이터 모델·덱 매니저·덱 굴림·사용 효과·호버 UI. 이 단계만으로 플레이 검증 가능(고정 덱).
- **Phase 2 (보상·교체):** §6 — RewardUI 주사위 보상 + 교체 UI, 노드 예고 제거.
- 각 단계는 독립 커밋. Phase 1 완료·검증 후 Phase 2.

## 11. 검증

- **컴파일**: 콘솔 에러 0.
- **Phase 1 플레이**: 전투 진입 시 덱 수만큼 굴림 / 호버 시 면 6개(주사위 이미지)+효과 표시 / 효과 주사위를 **확정 사용** 시 효과 발동(요약 선택 표시) / **취소 시 미발동** / 턴종료·재굴림 정상 / 파티 인원이 주사위 수에 직접 관여 안 함(모집 시에만 +2).
- **Phase 2 플레이**: 전투 보상에 랜덤 특수 주사위 제시 / 받기→교체 슬롯 선택→덱 반영 / 안 받으면 덱 유지 / 노드맵에 주사위 예고 아이콘 없음.
- **회귀**: 기존 스킬 주사위 조건·배정·포캐스트/리롤 물약 정상.
