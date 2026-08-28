# 3단계 콤보 액티브 + 패시브 재설계 + 상태 시스템 개편 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 4캐릭터의 액티브를 주사위 게이트 연속 성공 기반 3단계 콤보로 통일하고, 패시브를 인원/구역 비례로 재설계하며, 중독을 StS식 정수 중첩으로 바꾸고, 감전/촉매/고양/약화를 "공격 행동 단위" 1회성 상태로 구현한다.

**Architecture:** 기존 CombatPipeline 리액터 구조를 유지한 채 (1) AttackContext에 직접피해 플래그를 추가해 중독이 방어도·리액터를 우회하고, (2) 정적 AttackActionScope가 다단·광역 공격을 하나의 행동으로 묶어 1회성 상태의 적용/제거 경계를 만들며, (3) ComboSystem이 캐릭터별 콤보 단계를 관리하고 AutoAttackSystem이 스테이지 실행 루프를 돈다. 기존 [SerializeReference] 클래스명(WarriorGreatswordActive 등)은 유지해 .asset 호환을 지킨다 — 새 필드는 C# 기본값으로 로드되므로 필드명을 새로 지어 옛 직렬화 값의 오염을 피한다.

**Tech Stack:** Unity 6000.3.8f1, Assembly-CSharp(asmdef 없음), MCP RunCommand 검증, 에디터 자가 테스트 하네스(메뉴 실행형).

## Global Constraints

- 발동 주사위: 전사 4+ / 도적 3↓ / 마법사 홀수 / 연금 짝수 (기존 requirement 게이트 그대로 사용)
- 이동해야만 공격, 미이동 턴 = 공격 없음 + 콤보 초기화
- 중독: 정수 중첩, 몬스터 턴 시작(행동 전) 틱 = 현재 수치, 틱 후 −1, 방어도·공격 보정 무시, 직접 체력 손실
- 촉매/고양/약화/감전: "공격 행동" 단위 적용 후 제거 (다단·광역 전체 커버)
- 밸런스 수치 전부 [SerializeField] Inspector 노출
- 기존 클래스명 유지(SerializeReference 호환), 무관 파일 불변경
- 검증: AssetDatabase.Refresh → GetConsoleLogs(Error)=0 + 에디터 자가 테스트 + MCP 기능 프로브 (typeof 게이트 금지)

---

### Task 1: 중독 재설계 + 직접피해 경로

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatContext.cs` — AttackContext에 `public bool IsDirectHpLoss;`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Pipeline/CombatPipeline.cs` — IsDirectHpLoss면 OnPreAction 회피·OnCalculateOutput 리액터 통지 스킵, ApplyAction에서 TakeDirectDamage 라우팅
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/UnitStats.cs` — `public int TakeDirectDamage(int damage)` (TempArmor 스킵, Invulnerable 존중)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Unit.cs` — `public virtual int TakeDirectDamage(int damage)` (팝업 동일 처리)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/CommonStatuses.cs` — PoisonStatus 재작성
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/StatusEffectManager.cs` — CreateEffect(Poison) → duration 무시하고 -1
- Modify: `Assets/Scripts/Data/Potions/PoisonPotion/PoisonPotion.cs` — `stacks` 필드로 전환

**Interfaces (Produces):**
- `AttackContext.IsDirectHpLoss : bool` — true면 회피/계산 리액터/방어도 전부 우회
- `Unit.TakeDirectDamage(int) : int`
- `PoisonStatus` — Value=중첩, Duration=-1, 자기 턴 시작(OnPreAction) 틱 후 Value--; 0이면 자기 제거

PoisonStatus 핵심 (테스트 가능하게 순수 부분 분리):

```csharp
public class PoisonStatus : StatusEffect
{
    public PoisonStatus(int stacks) : base(EffectType.Poison, stacks, -1, isStackable: true) { }

    /// <summary>이번 틱 피해량을 반환하고 중첩을 1 줄인다. (순수 — 테스트 대상)</summary>
    public int ConsumeTick()
    {
        int damage = Mathf.Max(0, Value);
        Value = Mathf.Max(0, Value - 1);
        return damage;
    }

    public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
    {
        base.OnTurnEvent(trigger, context);   // Duration=-1이라 감소 없음
        if (Owner == null || context.Phase != EventPhase.TurnStart) return;
        if (trigger != CombatTrigger.OnPreAction) return;
        if (context.SourceUnit != Owner || context.IsSimulation) return;

        int damage = ConsumeTick();
        if (damage > 0)
        {
            var tick = new AttackContext(null, Owner, "중독", damage) { IsDirectHpLoss = true };
            CombatPipeline.Instance?.Process(tick);
        }
        if (Value <= 0) Owner.StatusEffects?.RemoveEffect(EffectType.Poison);
    }
}
```

- [ ] 위 파일 수정 → Refresh → 콘솔 에러 0
- [ ] MCP 프로브: PoisonStatus(6) → ConsumeTick() 6회 결과 {6,5,4,3,2,1}, Value 0

### Task 2: AttackActionScope + 1회성 행동 상태 4종

**Files:**
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/AttackActionScope.cs`
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Effects/ActionStatuses.cs` — ShockStatus/CatalystStatus/InspireStatus/WeakenStatus
- Modify: `EffectData.cs` — EffectType에 `Shock, Catalyst, Inspire, Weaken` 추가 (열거형 끝에 — 기존 직렬화 값 보존)
- Modify: `StatusEffectManager.cs` — CreateEffect 스위치 4종 추가
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/Monster.cs` — ExecuteIntent를 scope Begin/End로 감싼다

**Interfaces (Produces):**
- `AttackActionScope.Begin(Unit source)` / `End()` / `CurrentActionId : int` (0=스코프 없음) / `MarkConsumed(StatusEffect, Unit owner)`
- 상태 4종: 스코프 안 → 같은 행동의 모든 타격에 적용 + End()에서 제거. 스코프 밖(몬스터 지연 컨텍스트 등) → 첫 적용 컨텍스트의 OnPostAction에서 자기 제거.
- 배율: Shock 대상 피해 +30% / Catalyst 소스 +25% / Inspire 소스 +30% / Weaken 소스 −25%. 전부 `IsDirectHpLoss` 컨텍스트엔 반응 불가(파이프라인이 통지 자체를 스킵).

- [ ] 구현 → Refresh → 콘솔 에러 0
- [ ] MCP 프로브: Begin→상태 소비→End→제거 확인, 스코프 밖 소비→즉시 제거 확인

### Task 3: ComboSystem

**Files:**
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Combo/ComboTracker.cs` — 순수 로직
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Combo/ComboSystem.cs` — MonoBehaviour 싱글턴
- Modify: `CombatManager.cs` — EndPlayerTurnRoutine에서 미이동 캐릭터 콤보 리셋, StartCombat/EndCombat에서 전체 리셋

**Interfaces (Produces):**
```csharp
public enum ComboOutcome { Advanced, Finished, BrokenByDice, BrokenByNoTarget, BrokenByNoMove }
public class ComboTracker            // 캐릭터 1명분, 순수
{
    public int Stage { get; }        // 0~2 = 다음에 쓸 단계 인덱스
    public int RegisterGateSuccess() // 사용할 단계 인덱스 반환(0~2), 내부 전진은 Confirm에서
    public void ConfirmExecuted()    // 실제 공격 발생 → 전진, 3단계였으면 0으로
    public void Reset()
}
public class ComboSystem : MonoBehaviour   // EnsureInstance 패턴
{
    public int PeekStage(Character ch)     // UI용 현재 단계(0~2)
    public ComboTracker GetTracker(Character ch)
    public void ResetCombo(Character ch, ComboOutcome reason)
    public void ResetAll()
    public event System.Action<Character,int,ComboOutcome> OnComboChanged  // UI 구독
}
```

- [ ] 구현 → Refresh → 콘솔 에러 0

### Task 4: ComboActiveSkill 기반 + AutoAttackSystem 개편

**Files:**
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/Skills/ComboActiveSkill.cs`
- Modify: `AutoAttackSystem.cs` — ResolveRoutine을 콤보 분기 + 스테이지 실행 루프로 재작성

**Interfaces (Produces):**
```csharp
public abstract class ComboActiveSkill : CharacterActiveSkill
{
    public const int StageCount = 3;
    public abstract string GetStageName(int stage);
    public abstract float  GetStageMultiplier(int stage);
    public virtual  int    GetStageHits(int stage) => 1;             // 대상당 타격 수
    public abstract List<Unit> ResolveStageTargets(Character source, int stage, IReadOnlyList<int> passedZones);
    public virtual  void OnStageHitLanded(Character source, Unit target, int stage, int hitIndex) { }  // 중독/감전/약화 부여
    public virtual  void OnStageCompleted(Character source, List<Unit> targets, int stage) { }         // 방어도/고양/중독 2배
    public int CalculateStageDamage(Character source, int stage)
        => Mathf.Max(1, Mathf.RoundToInt((source?.Stats?.Attack ?? 0) * Mathf.Max(0.1f, GetStageMultiplier(stage))));
}
```

AutoAttackSystem.ResolveRoutine 새 흐름:
1. `FindEmpoweredAttack` → 게이트 판정
2. 게이트 실패 → `ComboSystem.ResetCombo(ch, BrokenByDice)` + 기본공격 (기존 CollectTargets — 마법사는 IAttackZoneProvider 반영) + Catalyst는 기본공격에도 적용(스코프로 자동)
3. 게이트 성공 → `stage = tracker.RegisterGateSuccess()` → `ResolveStageTargets` → 비었으면 `ResetCombo(BrokenByNoTarget)` 후 종료 → 있으면 `AttackActionScope.Begin(ch)` → 타격 루프(hits × targets, 발사체·간격 기존 방식) → `AttackActionScope.End()` → `OnStageCompleted` → `tracker.ConfirmExecuted()`
4. 기본공격도 `AttackActionScope.Begin/End`로 감싼다 (촉매가 기본공격에도 적용/소모)

- [ ] 구현 → Refresh → 콘솔 에러 0

### Task 5: 4캐릭터 콤보 액티브 (클래스명 유지, 내부 재작성)

**Files:** 각 `Assets/Scripts/Data/Character Preset/<캐릭터>/<기존 클래스>.cs`

| 캐릭터/클래스 | 1단계 | 2단계 | 3단계 |
|---|---|---|---|
| 전사 WarriorGreatswordActive | 균열 베기: 현 구역 1체 ×1.5 | 지진파: 현+양옆 전원 ×1.1 | 대지 가르기: 전체 ×1.8 + 아군 전원 TempArmor+10 |
| 도적 RogueAmbushActive | 쌍비수: 1체 ×0.7×2타 | 맹독비수: 1체 ×0.6×3타, 타당 중독2 | 죽음의 촉매: 1체 ×0.5×5타 후 중독 2배 |
| 마법사 MageEnergyBallActive | 비전 화살: 회로 내 1체 ×1.4 | 연쇄 번개: 회로 내 근접 2체 ×1.1 + 감전 | 궤도 붕괴: 회로 전 구역 ×1.8 |
| 연금 AlchemistThrowActive | 산성 플라스크: 현 구역 1체 ×1.0 + 약화 | 촉매 살포: 현+양옆 ×0.9 + 약화 | 현자의 폭탄: 전체 ×1.8 + 아군 전원 고양 |

- 모든 배율/수치 [SerializeField] (새 필드명: stage1Multiplier 등 — 옛 multiplier 직렬화 값 오염 방지)
- 중독 2배: `target.StatusEffects.GetEffectValue(Poison)` → 0보다 크면 `AddEffect(CreateEffect(Poison, current, -1))` (합산=2배, 소비 없음)
- 마법사 회로 구역 조회는 Task 6의 `MageCircuitPassive` 정적 헬퍼 사용
- GetDynamicDescription = 3단계 요약 문자열

- [ ] 구현 → Refresh → 콘솔 에러 0

### Task 6: 패시브 4종 재작성 (클래스명 유지)

**Files:** 각 캐릭터 폴더의 기존 패시브 파일 + `IZoneReachProvider.cs` 옆에 `IAttackZoneProvider` 추가 + `ReagentTile.cs` + `AutoAttackSystem.CollectTargets`

- 방진 WarriorGuardPassive: 감쇄 = perAlly(10%) × min(같은 구역 다른 생존 아군 수, cap 3). 대상=전사 구역의 캐릭터 전원(전사 포함). 공격 피해만(직접피해는 파이프라인이 통지 스킵).
- 협공 RogueFlankPassive: 증가 = perAlly(30%) × 같은 구역 다른 생존 아군 수 (+RoguePositioningBoost 유지). 도적 소스 공격만.
- 마력 회로 MageRangedPassive: `IAttackZoneProvider.GetAttackableZones(ch)` = 자기 구역 + 다른 생존 아군 구역(중복 제거). IZoneReachProvider 제거. AutoAttackSystem.CollectTargets: provider 있으면 회로 구역들 중 자기 구역에서 원형 거리 최소인 주인 선택.
- 시약 준비 ReagentPrepPassive: 무작위 3타일 설치 유지, 스택/OnAttack 제거. ReagentTile: OnTraverse(모든 생존 캐릭터) → CatalystStatus 부여(비중첩·갱신), 타일 유지(RemoveAttribute 삭제), OnEndTurn 훅 제거.

- [ ] 구현 → Refresh → 콘솔 에러 0

### Task 7: UI — 콤보 표시 + 신규 상태 등록

**Files:**
- Modify: `Assets/Scripts/UI/CharacterUI.cs` — HP바 위 콤보 핍 3칸(코드 생성 Image), ComboSystem.OnComboChanged 구독: 단계 수만큼 점등, Finished=3칸 플래시 후 소등, Broken=붉은 플래시 후 소등
- Modify: `Assets/Scripts/UI/TooltipKeywordFormatter.cs` — 감전/촉매/고양/약화 표시명+설명, 중독 설명 갱신
- Modify: `StatusVisualLibrary` (위치 확인 후) — 4종 색상 등록 (아이콘 폴백 허용)

- [ ] 구현 → Refresh → 콘솔 에러 0

### Task 8: 에셋 갱신 + 자가 테스트 + 보고

- [ ] MCP SerializedObject로 4개 프리셋 .asset의 skillName/description 갱신 (게이트 requirement는 유지 확인만), PoisonPotion.asset 설명 갱신
- [ ] Create: `Assets/Scripts/Editor/CombatRedesignSelfTests.cs` — 메뉴 실행형 자가 테스트: 콤보 전이표, 중독 틱 수열, 인접 wrap, 스코프 소비/제거, 회로 구역 유니크. MCP RunCommand로 실행 → 전부 PASS
- [ ] 최종 Refresh + 콘솔 에러 0 + 씬 저장
- [ ] 보고: 구조 설명 / 변경 파일 / 테스트 결과 / 밸런스 포인트 / 에디터 확인 항목

## 이번 스코프에서 제외 (보고서에 명시)
- GreatswordWideSwing·MageFocusBoost 모디파이어는 새 체계에서 무효(죽은 코드) — 모디파이어 재설계 회차에서 처리
- 튜토리얼 문구는 "이동=공격" 골격이 유지되므로 동작하지만, 콤보 개념 안내는 후속
- 진형 공명(관계등) 이전 설계안은 이 스펙으로 대체됨
