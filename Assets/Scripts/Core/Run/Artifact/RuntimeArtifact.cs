using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 유물 런타임 베이스 — 유물 1개 = 서브클래스 1개 (Data/Artifacts/).
    /// ArtifactData.effect에 프로토타입으로 인라인 직렬화되고, 획득 시 CreateInstance로 복제된다.
    ///
    /// 훅은 인터페이스 DIM이 아니라 클래스 virtual로 분기한다 — 파생 클래스가 인터페이스를
    /// 재나열하지 않으면 DIM 매핑에 걸리지 않는 C# 함정 회피. 서브클래스는 반드시 override로 구현.
    /// 프로토타입 필드는 값 타입/불변만 (얕은 복사). 상태 갖는 훅은 !context.IsSimulation 가드 필수.
    /// </summary>
    [System.Serializable]
    public abstract class RuntimeArtifact : ICombatReactor
    {
        [System.NonSerialized] public ArtifactData data;   // 획득 시 CreateInstance가 주입

        public virtual int Priority => 11;   // 패시브(50~100) 뒤, 모디파이어(10~30) 대역

        // ── 규칙형 질의 효과 (기본 0 — 필요한 것만 override) ──
        public virtual float ShopDiscountPercent  => 0f;   // 상점 가격 -N%
        public virtual float RestHealBonusPercent => 0f;   // 휴식 회복 +N%p
        public virtual int   BattleGoldBonus      => 0;    // 전투 보상 골드 +N
        public virtual float ReviveHpBonusPercent => 0f;   // 점감 부활 HP +N%p
        public virtual int   BattleStartHeal      => 0;    // 전투 시작 시 파티 회복 +N

        // ── 전투 반응 훅 (기본 무동작 — 필요한 것만 override) ──
        public virtual void OnReact(CombatTrigger trigger, CombatContext context)
        {
            switch (context)
            {
                case AttackContext a:    OnAttack(trigger, a);    break;
                case HealContext h:      OnHeal(trigger, h);      break;
                case MoveContext m:      OnMove(trigger, m);      break;
                case TurnEventContext e: OnTurnEvent(trigger, e); break;
            }
        }

        public virtual void OnAttack(CombatTrigger trigger, AttackContext context) { }
        public virtual void OnHeal(CombatTrigger trigger, HealContext context) { }
        public virtual void OnMove(CombatTrigger trigger, MoveContext context) { }
        public virtual void OnTurnEvent(CombatTrigger trigger, TurnEventContext context) { }

        /// <summary>프로토타입 → 획득용 런타임 인스턴스 (얕은 복사 + 데이터 주입).</summary>
        public RuntimeArtifact CreateInstance(ArtifactData source)
        {
            var clone = (RuntimeArtifact)MemberwiseClone();
            clone.data = source;
            return clone;
        }
    }
}
