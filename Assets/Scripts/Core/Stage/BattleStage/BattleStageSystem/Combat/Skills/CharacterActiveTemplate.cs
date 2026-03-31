using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Visuals;
using UnityEngine;

namespace DiceOrbit.Data.Skills
{
    [System.Serializable]
    public abstract class CharacterActiveTemplate
    {
        [SerializeField] protected CombatVfxProfile vfxProfile;

        public CombatVfxProfile VfxProfile => vfxProfile;

        public void SetVfxProfile(CombatVfxProfile profile)
        {
            vfxProfile = profile;
        }

        public abstract int CalculateRawDamage(Character source, RuntimeAbility ability, int diceValue);

        public abstract string BuildPreview(Character source, RuntimeAbility ability, int diceValue);

        public virtual bool Execute(Character source, RuntimeAbility ability, List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            if (source == null || ability == null) return false;

            int rawDamage = CalculateRawDamage(source, ability, diceValue);
            if (rawDamage <= 0 || targets == null)
            {
                OnAfterResolved(source, ability);
                return true;
            }

            VfxManager.PlayCast(vfxProfile, source);

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                var action = new CombatAction(ability.BaseSkill.SkillName, ActionType.Attack, rawDamage);
                if (vfxProfile != null)
                {
                    action.AddTag("CustomVfx");
                }

                var context = new CombatContext(source, target, action);
                CombatPipeline.Instance?.Process(context);

                if (context.IsEffected)
                {
                    VfxManager.PlayHit(vfxProfile, target);
                }
            }

            OnAfterResolved(source, ability);
            return true;
        }

        public virtual void OnAfterResolved(Character source, RuntimeAbility ability)
        {
        }

        public virtual CharacterActiveTemplate Clone()
        {
            return (CharacterActiveTemplate)MemberwiseClone();
        }
    }
}
