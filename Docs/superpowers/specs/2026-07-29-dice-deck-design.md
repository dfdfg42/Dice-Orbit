# 주사위 덱(Dice Deck) 토대 설계

**날짜:** 2026-07-29
**브랜치:** feature/event-outcomes-refactor-20260729 (이 위에서 이어 작업)
**범위:** 덱 "토대"만 — 개조 보상/상점/저장은 이번 범위 밖 (아래 §7).

## 1. 목표

플레이어가 **영구 소유하는 주사위 덱**을 도입한다. 각 주사위는:
- **커스텀 6면** — 면마다 임의 정수 값 (예: `1 1 2 2 3 3`). 표준 주사위 = `1 2 3 4 5 6`.
- **사용 시 효과(선택)** — 주사위를 행동에 확정 사용하면 발동. 유물/이벤트 결과와 같은 다형성.
- **호버 UI** — 주사위에 마우스를 올리면 위에 6면(2줄×3, 실제 주사위 이미지)과 사용 효과를 표시.

현재는 매 턴 익명 주사위(값 `Random 1~6`, 개수 = 파티×2)를 새로 굴린다. 이 토대는 그것을 **소유 덱 굴림**으로 바꾼다.

## 2. 확정된 결정

- **파티 무관 고정 덱.** 시작 덱은 `DiceDeckManager` 인스펙터에 설정(예: 표준 주사위 N개). 파티 인원과 분리 → 기존 `usePartyBasedDiceCount`/`dicePerCharacter`/`RefreshDiceCountFromParty`/`HandlePartyChanged` 제거.
- **효과 발동 = `Used` 확정 시점** (`Reserved`(타겟팅 중, 취소 가능) 아님). 현재 `DiceUI`가 직접 `State=Used` 하던 것을 `DiceManager.MarkUsed`로 중앙화.
- **효과 컨텍스트는 최소** — 사용한 캐릭터 + 굴린 값. 나중에 스킬/대상 필요 시 확장.
- **효과 다형성 = `[SerializeReference, SubclassPicker]`** — `EventOutcome`(2026-07-29 리팩터)·`RuntimeArtifact`와 동일 패턴.
- **면 값은 면이 권위** — `DiceData`의 `1~6` 하드 클램프 완화(면 값 그대로). 스킬 조건(`DiceRequirement`)은 값만 보므로 그대로 동작.
- **호버 면 그리드 = 실제 주사위 이미지** — 각 칸 = `Assets/Sprites/Dice.png` + 면 값(미니 주사위), 2줄×3. 숫자 텍스트만 아님.

## 3. 데이터 모델

```csharp
// 소유하는 주사위 한 개 (런타임). 위치: Core/.../Dices/
[Serializable]
public class DieDefinition
{
    public string Name = "표준 주사위";
    public int[] Faces = {1,2,3,4,5,6};              // 길이 6
    [SerializeReference, SubclassPicker] public DieEffect Effect;  // null = 효과 없음
    public int RollFace() => Faces[Random.Range(0, Faces.Length)];
}

// 사용 확정 시 발동하는 효과 (다형성)
[Serializable]
public abstract class DieEffect
{
    public Sprite Icon;                              // 호버 효과 행 아이콘(선택)
    public abstract string Apply(DieUseContext ctx); // 발동 + 요약 반환
    public virtual string Preview() => "";           // 호버 라벨용 짧은 설명
}
public class DieUseContext { public Character User; public int RolledValue; }

// 토대 증명용 구체 효과 1~2개 (유물/이벤트 결과 방식):
//   GainGoldOnUse { int amount }        → GoldManager.AddGold
//   HealUserOnUse { int amount }        → ctx.User 회복
```

`DiceData` 확장 — 기존(`id, value, state, assignedCharacter`) 유지 + 추가:
- `DieDefinition Source` (비직렬화 런타임 참조) — 효과 발동 + 호버 표시용.
- 굴림 시 `value = Source.RollFace()`. 하드 `1~6` 클램프 완화(면 값 허용).

## 4. 덱 소유 · 굴림 · 사용 효과

**`DiceDeckManager` (신규 싱글턴 — `ArtifactManager`/`PotionManager`와 대칭)**
- 인스펙터: `List<DieDefinition> startingDeck` (효과는 `[SerializeReference]` — 씬/프리팹 직렬화는 다형성 지원).
- `Deck` = 런타임 작업본 (런 시작 시 `startingDeck` 복사). `EnsureInstance()` 패턴. 씬에 배치.

**`DiceManager.RollDice` 변경**
- 익명 `Random 1~6` 대신 **`DiceDeckManager.Deck`를 굴림**: 각 `DieDefinition`마다 `value = def.RollFace()`, `Source = def`인 `DiceData` 생성.
- 파티×2 로직(`usePartyBasedDiceCount` 등) 제거. 포캐스트 바이어스·`RerollAvailableDice`는 굴린 값에 그대로 동작.

**사용 효과 발동 (중앙화)**
- 신규 `DiceManager.MarkUsed(DiceData dice)`: `State = Used` 세팅 + `dice.Source?.Effect?.Apply(new DieUseContext{ User=…, RolledValue=dice.Value })` 발동. `Apply`가 돌려준 요약은 기존 `FloatingLabelPopup`(있으면)으로 띄우거나 로그 — 표시 자체는 선택.
- `DiceUI`의 직접 `State=Used`(현 `DiceUI.cs:161`)를 이 메서드 호출로 교체. `CharacterActionUI`(현 `:568` `MarkDiceAsUsed`)가 최종 경로.

## 5. 호버 UI

**`DiceElement`에 호버 핸들러 추가** — `IPointerEnterHandler`/`IPointerExitHandler` (이미 `IPointerClickHandler` 구현 → New Input System EventSystem 경유 동작). Enter 시 `DiceHoverTooltipUI.Show(this)`, Exit 시 `Hide()`.

**`DiceHoverTooltipUI` (신규, 전용 패널)** — 호버한 주사위 **바로 위**에 표시:
- **면 그리드**: 2줄×3 = `Source.Faces` 6개. 각 칸 = `Dice.png` 배경 + 면 값(미니 주사위).
- **효과 행**: `Source.Effect`가 있으면 `Effect.Icon`(있으면) + `Effect.Preview()`. 없으면 행 숨김.
- 위치: 호버한 `DiceElement`의 RectTransform 위. 싱글턴 + 최상단 정렬은 `HoverTooltipUI` 방식 차용.

## 6. 데이터 흐름 (전체)

```
런 시작 → DiceDeckManager.Deck = startingDeck 복사
플레이어 턴 → CombatManager → DiceManager.RollDice()
   → Deck의 각 DieDefinition.RollFace() → DiceData(value, Source=def)
   → DiceUI.DisplayDice
호버 → DiceElement.OnPointerEnter → DiceHoverTooltipUI: Source.Faces(2×3) + Effect.Preview()
사용 확정 → CharacterActionUI → DiceManager.MarkUsed(dice)
   → State=Used + Source.Effect?.Apply(ctx) → 요약 표시
턴 종료 → DiceManager.ResetDice (덱 자체는 유지)
```

## 7. 이번 범위 밖 (후속)

- **덱 저장/이어하기.** 고정 덱이라 이번엔 불필요(이어하기 = config 재초기화). 개조로 덱이 mutable해지면 필요. 그때 세이브 리팩터(`Docs/superpowers/specs/2026-07-27-save-system-refactor-design.md`)의 **`saveId`(에셋 파일명) + `SaveIdCatalog`** 방식에 맞춰 통합: `faces`는 `int[]`로 직접 저장(JsonUtility OK), **효과는 `saveId` 참조**(JsonUtility가 `[SerializeReference]` 다형성을 저장 못 하므로). — 이번엔 코드 없이 방향만 기록.
- **개조 보상 연결** — 맵의 `DiceModReward` 노드가 실제로 덱을 강화하는 보상 화면.
- **상점/획득** — 주사위 구매·획득.
- 새 주사위 프리셋을 SO로 관리하는 카탈로그(개조/상점 도입 시).

## 8. 영향 파일

**신규**
- `Core/.../Dices/DieDefinition.cs` (+ `DieEffect`, `DieUseContext`, 구체 효과 1~2)
- `Core/.../Dices/DiceDeckManager.cs`
- `UI/DiceHoverTooltipUI.cs`

**수정**
- `Core/.../Dices/DiceData.cs` — `Source` 참조, 클램프 완화
- `Core/.../Dices/DiceManager.cs` — 덱 굴림, 파티×2 제거, `MarkUsed`
- `UI/DiceElement.cs` — 호버 핸들러
- `UI/DiceUI.cs` — `State=Used` 직접 세팅 → `DiceManager.MarkUsed` 경유
- `UI/CharacterActionUI.cs` — 최종 사용 경로가 `MarkUsed` 호출

**에디터 셋업(코드 밖)**
- `DiceDeckManager`를 BattleScene에 배치 + `startingDeck` 구성.
- `DiceHoverTooltipUI` 패널을 씬/프리팹에 배치.

## 9. 검증

- **컴파일**: 헤드리스 빌드 또는 에디터 리프레시 후 콘솔 에러 0.
- **플레이**: 전투 진입 시 덱 수만큼 주사위가 굴려짐 / 호버 시 면 6개(주사위 이미지)+효과 표시 / 효과 주사위를 행동에 **확정 사용** 시 효과 발동(요약은 선택 표시) / **취소(Reserved) 시 미발동** / 턴 종료·재굴림 정상.
- **회귀**: 파티 인원 변경이 더는 주사위 수에 영향 없음(고정 덱). 기존 스킬 주사위 조건·배정·포캐스트/리롤 물약 정상.
