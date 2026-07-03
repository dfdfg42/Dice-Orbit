namespace DiceOrbit.Core.Pipeline
{
    /// <summary>
    /// 전투 트리거 시점 정의
    /// </summary>
    public enum CombatTrigger
    {
        // 2. 액션 실행 전
        OnPreAction,        // 액션 준비 (실행 가능 여부, 비용 소모 확인)
        OnCalculateOutput,  // 데미지/힐량 계산 (버프/디버프/패시브 보정)

        // 3. 액션 실행 후
        OnHit,              // 적중 시 (방어, 반격)
        OnPostAction        // 모든 처리 완료 후
    }

    /// <summary>
    /// 전투 상황에 반응하는 객체 레이어 (Passive, Effect, Equipment 등).
    /// 기본 <see cref="OnReact"/>가 컨텍스트 구체 타입으로 디스패치하므로,
    /// 단순 리액터는 타입별 훅(<see cref="OnAttack"/> 등) 중 필요한 것만 구현하면 된다.
    /// 전파/특수 리액터(TileData, PassiveManager 등)는 <see cref="OnReact"/> 자체를 직접 구현한다.
    /// </summary>
    public interface ICombatReactor
    {
        /// <summary>실행 우선순위 (높을수록 먼저 실행, 데미지 계산 시 중요)</summary>
        int Priority { get; }

        /// <summary>
        /// 트리거 발생 시 호출. 기본 구현은 컨텍스트 구체 타입으로 디스패치한다.
        /// 모든 컨텍스트를 직접 처리/전파해야 하는 리액터는 이 메서드를 override한다.
        /// </summary>
        void OnReact(CombatTrigger trigger, CombatContext context)
        {
            switch (context)
            {
                case AttackContext a:    OnAttack(trigger, a);    break;
                case HealContext h:      OnHeal(trigger, h);      break;
                case MoveContext m:      OnMove(trigger, m);      break;
                case TurnEventContext e: OnTurnEvent(trigger, e); break;
            }
        }

        /// <summary>공격 컨텍스트 반응 (기본 무동작).</summary>
        void OnAttack(CombatTrigger trigger, AttackContext context) { }

        /// <summary>힐 컨텍스트 반응 (기본 무동작).</summary>
        void OnHeal(CombatTrigger trigger, HealContext context) { }

        /// <summary>이동 사건 반응 (기본 무동작).</summary>
        void OnMove(CombatTrigger trigger, MoveContext context) { }

        /// <summary>턴/타일 사건 반응 (기본 무동작).</summary>
        void OnTurnEvent(CombatTrigger trigger, TurnEventContext context) { }
    }
}
