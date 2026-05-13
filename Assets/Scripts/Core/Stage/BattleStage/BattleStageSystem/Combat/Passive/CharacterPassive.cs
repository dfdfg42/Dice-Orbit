using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

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

        public abstract void OnReact(CombatTrigger trigger, CombatContext context);

        public virtual void OnOwnerSelected(Character c)  { }
        public virtual void OnOwnerDeselected()            { }

        public virtual bool AllowSamePassive(IPassive incoming) => isStackable;
    }
}
