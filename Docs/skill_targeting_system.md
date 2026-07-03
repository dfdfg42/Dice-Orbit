# 스킬 타겟팅 시스템

캐릭터 액티브 스킬의 타겟 선택 방식 전반을 다루는 문서.  
단일 적 공격부터 복수 아군 버프, 다중 타일 설치까지 하나의 구조로 처리한다.

---

## 구성 파일

| 파일 | 역할 |
|------|------|
| `Assets/Scripts/.../Combat/Skills/CharacterActiveTemplate.cs` | `CharacterSkillTargetType` enum + `CharacterActiveSkill` 기본 클래스 |
| `Assets/Scripts/.../Combat/Skills/RuntimeAbility.cs` | `ActiveSkillSlot` — 런타임 스킬 인스턴스 |
| `Assets/Scripts/.../Combat/SkillManager.cs` | 스킬 준비·실행 조율 |
| `Assets/Scripts/.../Combat/SkillTargetSelector.cs` | 타겟 선택 UI + 멀티 선택 로직 |
| `Assets/Scripts/UI/CharacterActionUI.cs` | 스킬 카드 UI, 타겟 타입 라벨 |

---

## CharacterSkillTargetType

```csharp
public enum CharacterSkillTargetType
{
    None,        // 타겟 없음, 즉시 실행
    OneEnemy,    // 단일 적
    AllEnemies,  // 모든 적 (즉시 실행)
    OneAlly,     // 단일 아군
    AllAllies,   // 모든 아군 (즉시 실행)
    OneTile,     // 단일 타일
    AllTiles,    // 모든 타일 (즉시 실행)
    MultiEnemy,  // N명 적 순차 선택
    MultiAlly,   // N명 아군 순차 선택
    MultiTile,   // N개 타일 순차 선택
}
```

### 즉시 실행 vs 선택 필요

| 즉시 실행 (타겟 선택 없이 바로 발동) | 선택 필요 (SkillTargetSelector 진입) |
|---|---|
| `None`, `AllEnemies`, `AllAllies`, `AllTiles` | `OneEnemy`, `OneAlly`, `OneTile`, `MultiEnemy`, `MultiAlly`, `MultiTile` |

---

## targetCount 필드

`Multi*` 타입에서 몇 개를 선택할지 지정한다.  
인스펙터에서 `CharacterActiveSkill` 하위 클래스의 `Target Count` 필드로 설정.

```csharp
// CharacterActiveSkill
[Tooltip("MultiEnemy/MultiAlly/MultiTile 타입일 때 선택할 개수")]
[SerializeField] public int targetCount = 1;

public int TargetCount => Mathf.Max(1, targetCount);
```

---

## 전체 흐름

```
CharacterActionUI (스킬 카드 클릭)
  └─ SkillManager.PrepareSkill()
        ├─ [즉시 실행 타입]
        │    └─ ConfirmSkillExecution() 직행
        │         (AllEnemies → CombatManager.ActiveMonsters 전달)
        │         (AllAllies  → PartyManager.GetAliveCharacters() 전달)
        │         (AllTiles   → OrbitManager.Tiles 전달)
        │
        └─ [선택 필요 타입]
             └─ SkillTargetSelector.StartTargetSelection()
                   └─ 플레이어 입력 대기
                         ├─ 좌클릭: 타겟 누적
                         │    └─ N개 채워지면 ConfirmSkillExecution() 자동 호출
                         └─ 우클릭: 마지막 선택 undo / 0개면 전체 취소
```

---

## SkillTargetSelector 상세

### 핵심 상태

```csharp
private int                       _requiredCount;       // 목표 선택 개수
private readonly List<LineRenderer> _confirmedLines;    // 확정된 선 오브젝트
private readonly List<Vector3>      _confirmedPositions;// 선 시작점 (시전자 위치만)
private readonly List<Unit>         _pendingUnits;      // 누적된 유닛 선택
private readonly List<TileData>     _pendingTiles;      // 누적된 타일 선택
```

### 커서 선 (방사형 방식)

모든 선의 **시작점은 항상 캐릭터 위치**다.  
(`startPos = _confirmedPositions[0]` — `_confirmedPositions`에는 시전자 위치만 담긴다.)  
확정선·커서선 모두 캐릭터에서 각 타겟(또는 마우스)으로 **방사형(radial)**으로 뻗는다. 타겟끼리 이어지는 체인이 아니다.

```
          ┌──(확정선)──▶ 타겟1
캐릭터 ────┼──(확정선)──▶ 타겟2
          └──(커서선)──▶ 마우스
```

- **확정선**: 청록색 (`0.3, 0.8, 1.0`), 클릭마다 새 `LineRenderer` GameObject 생성
- **커서선**: 유효 타겟이면 초록, 아니면 빨강

### 우클릭 동작

```
선택된 것이 있음 → UndoLastSelection() : 마지막 확정선 + 선택 1개 제거
선택된 것이 없음 → CancelTargetSelection() : 전체 취소, 주사위 반환
```

### 유효 타겟 판정

| 타입 | 판정 기준 |
|---|---|
| `OneEnemy` / `MultiEnemy` | `Monster` 컴포넌트 존재 |
| `OneAlly` / `MultiAlly` | `Character` 컴포넌트 + 자기 자신 제외 + 생존 |
| `OneTile` / `MultiTile` | `TileData` 컴포넌트 존재 |

### 진행 상황 툴팁

`Multi*` 타입에서 `_requiredCount > 1`이면 `HoverTooltipUI`에 진행 상황을 표시한다.

```
"2 / 3 선택  (우클릭: 취소)"
```

---

## Execute() 에서 타겟 받기

스킬 기본 구현(`CharacterActiveSkill.Execute`)은 `List<Unit> targets`만 루프한다.  
타일을 사용하는 스킬은 `Execute()`를 오버라이드해 `targetTiles`를 처리한다.

```csharp
public override bool Execute(
    Character source, ActiveSkillSlot ability,
    List<Unit> targets, List<TileData> targetTiles, int diceValue)
{
    // 타일에 속성 부여 예시
    foreach (var tile in targetTiles)
        tile.AddAttribute(new TurretTileAttribute(...));
    return true;
}
```

아군 버프 스킬 예시:
```csharp
public override bool Execute(
    Character source, ActiveSkillSlot ability,
    List<Unit> targets, List<TileData> targetTiles, int diceValue)
{
    foreach (var ally in targets)
    {
        var context = new HealContext(source, ally, skillName, healAmount);
        CombatPipeline.Instance?.Process(context);
    }
    return true;
}
```

---

## 새 타겟 타입 스킬 만들기

### 단일 아군 버프 스킬

1. `CharacterActiveSkill` 상속 클래스 생성
2. 인스펙터에서 `Target Type` = `OneAlly`
3. `Execute()`에서 `targets[0]`에 버프 적용

### 복수 타일 설치 스킬

1. `CharacterActiveSkill` 상속 클래스 생성
2. 인스펙터에서 `Target Type` = `MultiTile`, `Target Count` = `2`
3. `Execute()`에서 `targetTiles`를 순회하며 `tile.AddAttribute(...)` 호출

### 전체 아군 버프 스킬

1. `Target Type` = `AllAllies`
2. 타겟 선택 없이 즉시 실행 — `SkillManager`가 `PartyManager.GetAliveCharacters()`를 자동으로 `targets`에 담아 전달

---

## 데이터 흐름

```
SkillTargetSelector
  └─ _pendingUnits / _pendingTiles 누적
        └─ ConfirmAndExecute()
              └─ SkillManager.ConfirmSkillExecution(source, slot, dice, units, tiles)
                    └─ ActionQueueManager.EnqueueAction(FinalExecutionRoutine)
                          └─ ActiveSkillSlot.Execute(source, units, tiles, diceValue)
                                └─ CharacterActiveSkill.Execute() 오버라이드
                                      └─ CombatPipeline.Process() per target
```

---

## 관련 문서

- [combat_floating_notification_system.md](combat_floating_notification_system.md) — 패시브·상태이상 알림 버블
- [ui_hover_tooltip_system_design.md](ui_hover_tooltip_system_design.md) — 툴팁 UI 설계
