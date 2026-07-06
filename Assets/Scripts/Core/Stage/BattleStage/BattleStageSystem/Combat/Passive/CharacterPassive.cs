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

        // OnReact는 ICombatReactor의 기본 디스패치(default interface method)를 사용한다.
        // 자식은 OnAttack / OnHeal / OnMove / OnTurnEvent 훅 중 필요한 것만 구현한다.

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
