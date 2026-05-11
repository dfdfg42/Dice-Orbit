using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    public abstract class CharacterPassive : ScriptableObject, IPassive
    {
        [SerializeField] protected string passiveName = "";
        [SerializeField, TextArea] protected string description = "패시브 설명";
        [SerializeField] protected int priority = 0;
        [SerializeField] protected Sprite icon;
        [SerializeField] protected bool isStackable = true;
        [SerializeField, Min(1)] protected int currentLevel = 1;

        protected Unit owner;

        public virtual string PassiveName => passiveName;
        public virtual string Description => description;
        public virtual int Priority => priority;
        public virtual Sprite Icon => icon;
        public virtual bool IsStackable => isStackable;
        public int CurrentLevel => currentLevel;

        public virtual string GetDynamicDescription() => string.Empty;

        public void ConfigureMetadata(string name, string desc, Sprite iconSprite = null)
        {
            if (!string.IsNullOrWhiteSpace(name)) passiveName = name;
            if (!string.IsNullOrWhiteSpace(desc)) description = desc;
            if (iconSprite != null) icon = iconSprite;
        }

        public virtual void Initialize(Unit ownerUnit)
        {
            owner = ownerUnit;
            ApplyLevel(currentLevel);
        }

        public void SetLevel(int level)
        {
            int normalized = Mathf.Max(1, level);
            if (currentLevel == normalized) return;
            currentLevel = normalized;
            ApplyLevel(currentLevel);
        }

        protected virtual void ApplyLevel(int level) { }

        public virtual IPassive Clone()
        {
            return Instantiate(this);
        }

        public abstract void OnReact(CombatTrigger trigger, CombatContext context);

        public virtual void OnOwnerSelected(Character c) { }
        public virtual void OnOwnerDeselected() { }

        public virtual bool AllowSamePassive(IPassive incoming) => isStackable;
    }
}
