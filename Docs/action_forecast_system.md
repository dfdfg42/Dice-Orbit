# 행동 예고 시스템 — "누르기 전에 다 보인다"

- 최종 갱신: 2026-10-04 (행동 예고 신설)
- 설계 스펙: [superpowers/specs/2026-10-04-action-forecast-design.md](superpowers/specs/2026-10-04-action-forecast-design.md)
- 코드: `Combat/Forecast/`, `Visuals/ActionForecastView.cs`, `Tile/TileForecast.cs`

캐릭터와 주사위를 고르는 순간(또는 다른 주사위에 마우스를 올리는 순간), 그 주사위로 움직였을 때 벌어질 일을 전부 보여 준다:
이동 경로 · 공격 종류 · 대상별 예상 피해 · 콤보 변화 · 경로 위 타일 효과 · 도착 타일에서 턴을 마칠 때의 효과 · 도착지를 노리는 몬스터 행동.

---

## 1. 원칙 — 예고는 실행과 같은 코드에서 나온다

예고용 계산식을 따로 두지 않는다. 실행 경로를 "도착했다고 치고" 부른다. 그래서 새 패시브·모디파이어·상태·타일을 추가해도 예고는 따로 손댈 것 없이 따라온다.

| 무엇 | 단일 출처 | 예고가 쓰는 방법 |
|---|---|---|
| 이동 경로 | `OrbitManager.BuildMovePath` | 그대로 호출 (이동 루틴·지나간 구역 수집도 같은 함수) |
| 강화/기본/표적 없음 판정, 표적 수집 | `AutoAttackSystem.PlanAttack` → `AttackPlan` | `Character.PretendAt(도착 타일)` 안에서 호출 |
| 피해 보정 (패시브·모디파이어·상태·타일·유물) | `CombatPipeline.SimulateCalculation` | `AttackActionScope.Simulate(...)` 안에서 호출 |
| 방어도 흡수·무적 | `UnitStats.ResolveDamage` | `TakeDamage`와 같은 순수 함수 |
| 타일 통과/턴 종료 효과 | `TileAttribute.ForecastTraverse / ForecastEndTurn` | 속성이 스스로 적는다 |

**예고는 상태를 바꾸지 않는다.** 콤보 단계·1회성 상태(촉매·고양·감전)·알림·RNG 전부 그대로다. 파이프라인 리액터는 기존 규칙대로 `context.IsSimulation`일 때 부수효과를 건너뛴다.

### 장치 세 개

- **`Character.PretendAt(tile)`** — `using` 블록 동안 `CurrentTile`이 그 타일을 가리킨다. 구역 판정(`CombatZoneManager.GetZoneOf`)·방진·협공·마력 회로·예리함 타일·위치 모디파이어가 전부 `CurrentTile`을 읽으므로 한 번에 "도착했다고 치고" 계산된다. 트랜스폼과 실제 위치는 건드리지 않는다. **동기 구간에서만** 쓴다 — 코루틴 양보를 걸치면 다른 로직이 가짜 위치를 본다.
- **`AttackActionScope.Simulate(source, info)`** — 주사위 눈·콤보 단계·구역 넘음 정보를 읽는 모디파이어와 1회성 상태가 예고에서도 같은 조건으로 판정된다. 끝나면 이전 스코프(진행 중인 실제 행동 포함)를 그대로 되돌린다.
- **가상 리액터** — 경로의 타일이 `forecast.GrantStatus(EffectType.Catalyst, 25)`처럼 "가는 길에 얻을 상태"를 알리면, 그 상태를 유닛에 붙이지 않은 채 시뮬레이션에만 끼워 넣는다 (`SimulateCalculation(context, extraReactors)`). 시약 타일을 밟고 들어가는 공격의 촉매 보너스가 예고 수치에 들어가는 이유.

## 2. 흐름

```
CharacterActionUI  (주사위 선택 / 주사위 호버 / 선택 해제 / 패널 닫힘)
   └─ ActionForecastView.Show(character, diceValue) / Hide()
         └─ 0.25초마다 ActionForecaster.Build(character, diceValue)
               ├─ OrbitManager.BuildMovePath                    → Path, Destination
               ├─ 경로 타일마다 ForecastTraverse                 → PathNotes, 얻을 상태
               ├─ 도착 타일 ForecastEndTurn (통과로 사라진 속성 제외) → EndTurnNotes
               ├─ PretendAt(Destination) { PlanAttack → 타격 루프 시뮬레이션 }  → Kind, Targets, ComboChange
               └─ 몬스터 의도가 도착 타일(또는 이 캐릭터)을 노리는가  → Threats
```

타격 루프는 실행과 같은 순서(타격 회차 × 대상)로 돌고, 앞선 타격으로 쓰러진 대상은 건너뛴다. 대상별로 방어도와 체력을 누적해 깎는다.

## 3. 표시 (`ActionForecastView`)

| 요소 | 형태 |
|---|---|
| 이동 | 기존 `MovePathPreview` (체브론 + 도착 타일 리프트·핑). 이제 주사위 선택 즉시 |
| 조준선 | 도착 타일 → 대상 몬스터 포물선 (`DashedArcLine`). 이동 경로와 같은 노랑 — "저기로 가서 → 이걸 친다"가 한 줄로 이어진다 |
| 예상 피해 | 대상 몬스터 몸 위 숫자 (실제 피해 팝업과 같은 글꼴). `-18` / `-20 처치` / `방어도 -5` / `0` |
| 예고 카드 | [이동] 버튼 위, 오른쪽 맞춤. 공격 종류 → 대상 줄 → 콤보 → 경로 → 턴 종료 → 위험. 나쁜 소식 빨강, 좋은 소식 초록 |

카드 문구는 `ActionForecastText`(순수 함수)가 만든다 — 표시 코드에는 계산도 문구도 없다.

표시 시점: 캐릭터 패널이 열려 있고 주사위가 선택됨 → 계속 표시. 다른 주사위에 마우스를 올리면 그 주사위로 잠깐 바뀌고(`CharacterActionUI.PreviewDice`), 떼면 선택한 주사위로 돌아온다. 이동 실행·취소·패널 닫힘·플레이어 턴 종료 시 사라진다.

## 4. 타일 속성에 예고 붙이기

`OnTraverse`나 `OnEndTurn`을 구현하는 타일 속성은 예고 훅도 같이 구현한다.

```csharp
public override void OnTraverse(Core.Character character) => Explosion(character);

public override void ForecastTraverse(Core.Character character, TileForecast forecast)
{
    forecast.Note($"지뢰 피해 {Value}", ForecastTone.Bad);
    forecast.MarkConsumed();   // 터지면 사라진다 → 같은 타일의 턴 종료 예고에서 빠진다
}
```

- `forecast.Note(문구, 성격)` — 같은 문구는 `×N`으로 합쳐진다.
- `forecast.GrantStatus(종류, 값)` — 이번 공격에 영향을 주는 상태를 얻는다면 알린다 (피해 수치에 반영된다). 같은 종류는 한 번만.
- `forecast.HasPendingStatus(종류)` — 이 경로의 앞선 타일에서 이미 얻기로 된 상태인가 (불꽃 "한 턴에 하나만 끔").
- `forecast.MarkConsumed()` — 이번 통과로 속성이 사라진다.
- 훅은 실제 효과를 내면 안 된다 — 적기만 한다.

피해 계산에만 영향을 주는 속성(예리함·약화)은 훅이 필요 없다 — 파이프라인 시뮬레이션이 이미 반영한다.

현재 구현: 지뢰·점액·꿀·뼈 방패·자수정·불꽃·속박·빙결·단단함·조화·부조화·치유·시약·활력.

## 5. 반영하지 않는 것 (알고 쓰는 한계)

- **회피** — 확률이라 수치에 넣지 않고 대상 줄에 `회피 N%`로만 알린다.
- **타격 도중 붙는 부가 효과** — 같은 행동 안에서 앞 타격이 붙인 상태(감전 등)가 뒤 타격에 주는 영향. 예고는 "지금 상태 기준"이다.
- **다단 타격 중 대상 방어도에 반응하는 리액터** — 시뮬레이션 중 실제 방어도는 줄지 않는다.
- **몬스터 공격의 예상 피해** — 범위 안인지만 알린다.
- **꿀 타일의 속박 누적** (한 턴에 N개 이상) — 회복·곰 방어도만 알린다.

## 6. 검증

- `ActionForecastSelfTests` (메뉴 **DiceOrbit → Run ActionForecast Self-Tests**) — `ResolveDamage`와 `TakeDamage` 일치, 타격 누적·방어도·무적, 카드·꼬리표 문구, 타일 예고 노트, 시뮬레이션 스코프 복원.
- Play 대조 — 예고를 만든 뒤 같은 주사위로 실제 이동·공격을 실행해 대상별 체력 손실·도착 타일·콤보 단계를 비교한다. 2026-10-04: 15건 전부 일치 — 전사 1·2·3단계(다중 대상), 기본공격, 콤보 끊김, 도적 다단(3×2), 표적 없는 구역, 마법사 1·2단계, 방어도 있는 몬스터, 시약 타일 촉매(7 → 9), 지뢰 경로, 처치. 확인하지 못한 범위는 스펙 §7.
