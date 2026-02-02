using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// 모든 패시브 능력의 기본 클래스
    /// </summary>
    public abstract class PassiveAbility : ScriptableObject
    {
        public string PassiveName;
        [TextArea] public string Description;
        public Sprite Icon;

        // Triggers
        public virtual void OnTurnStart(Core.Character owner) { }
        public virtual void OnMove(Core.Character owner, int distance) { }
        public virtual void OnBeforeAttack(Core.Character owner, Core.Monster target, ref int damage) { }
        public virtual void OnAfterAttack(Core.Character owner, Core.Monster target) { }
        public virtual void OnDamageTaken(Core.Character owner, int damage) { }
        
        // 초기화 필요한 경우
        public virtual void Initialize(Core.Character owner) { }
    }
}
