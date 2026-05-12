using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Skills;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public abstract class CharacterPassive : CharacterSkillBase, IPassive
    {
        [SerializeField] protected int  priority    = 0;
        [SerializeField] protected bool isStackable = true;
        [SerializeField, Min(1)] protected int currentLevel = 1;

        protected Unit owner;

        // IPassive — description & name reuse base class fields
        public virtual string PassiveName            => skillName;
        public virtual string Description            => description;
        public virtual bool   IsStackable            => isStackable;
        public         int    CurrentLevel           => currentLevel;
        public virtual int    Priority               => priority;
        public override CharacterSkillType SkillType => CharacterSkillType.Passive;

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

        public virtual IPassive Clone() => (CharacterPassive)MemberwiseClone();

        public abstract void OnReact(CombatTrigger trigger, CombatContext context);

        public virtual void OnOwnerSelected(Character c)  { }
        public virtual void OnOwnerDeselected()            { }

        public virtual bool AllowSamePassive(IPassive incoming) => isStackable;
    }
}
