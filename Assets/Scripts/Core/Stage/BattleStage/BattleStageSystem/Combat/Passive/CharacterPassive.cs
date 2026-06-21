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
        [SerializeField] public int maxLevel = 1;

        [Header("Passive Config")]
        [SerializeField] protected int  priority    = 0;
        [SerializeField] protected bool isStackable = true;
        [SerializeField, Min(1)] protected int currentLevel = 1;

        protected Unit owner;

        public virtual string PassiveName  => skillName;
        public virtual string Description  => description;
        public virtual bool   IsStackable  => isStackable;
        public         int    CurrentLevel => currentLevel;
        public virtual int    Priority     => priority;
        public virtual int    MaxLevel     => Mathf.Max(1, maxLevel);

        public virtual string GetDynamicDescription() => string.Empty;

        public virtual void Initialize(Unit ownerUnit)
        {
            owner = ownerUnit;
            ApplyLevel(currentLevel);
        }

        public void SetLevel(int level)
        {
            int n = Mathf.Max(1, level);
            if (currentLevel == n) return;
            currentLevel = n;
            ApplyLevel(currentLevel);
        }

        protected virtual void ApplyLevel(int level) { }

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
