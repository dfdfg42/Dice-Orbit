using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.UI;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public abstract class CharacterPassiveSkill : IPassive
    {
        [Header("Info")]
        [SerializeField] protected string skillName = "";
        [SerializeField, TextArea(2, 4)] protected string description = "";
        [SerializeField] public Sprite icon;

        [Header("Passive Config")]
        [SerializeField] protected int  priority    = 0;
        [SerializeField] protected bool isStackable = true;

        protected Unit owner;

        public virtual string PassiveName  => skillName;
        public virtual string Description  => description;
        public virtual bool   IsStackable  => isStackable;
        public virtual int    Priority     => priority;

        public virtual string GetDynamicDescription() => string.Empty;

        // (레벨 시스템은 철거됨 — 성장은 전부 모디파이어로, 기획 REV05)

        public virtual void Initialize(Unit ownerUnit)
        {
            owner = ownerUnit;
        }

        public virtual IPassive Clone() => (CharacterPassiveSkill)MemberwiseClone();

        // OnReact는 ICombatReactor의 기본 디스패치(DIM)를 사용한다.
        // ⚠️ 훅은 반드시 "인터페이스를 나열한 이 베이스"에 virtual로 선언되어야 하고,
        // 자식은 반드시 override로 구현해야 한다. override 없이 public void로 선언하면
        // 인터페이스 매핑이 빈 DIM으로 굳어 훅이 절대 호출되지 않는다 (2026-07-21, 죽은 훅 21개 사고).
        public virtual void OnAttack(CombatTrigger trigger, AttackContext context) { }
        public virtual void OnHeal(CombatTrigger trigger, HealContext context) { }
        public virtual void OnMove(CombatTrigger trigger, MoveContext context) { }
        public virtual void OnTurnEvent(CombatTrigger trigger, TurnEventContext context) { }

        public virtual void OnOwnerSelected(Character c)  { }
        public virtual void OnOwnerDeselected()            { }
        public virtual void OnOwnerMoved(TileData newTile) { }

        public virtual bool AllowSamePassive(IPassive incoming) => isStackable;

        /// <summary>패시브가 실제로 발동했을 때 유닛 위에 이름 버블을 띄웁니다.</summary>
        protected void Notify() => CombatNotifier.NotifyPassive(owner, PassiveName);

        protected void Notify(string text) => CombatNotifier.NotifyPassive(owner, text);

        protected void Notify(string text, Color color) => CombatNotifier.Notify(owner, text, color);
    }
}
