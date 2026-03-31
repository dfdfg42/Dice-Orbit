using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Visuals;

namespace DiceOrbit.Data.Skills.Effects
{
    [CreateAssetMenu(fileName = "MageStackDamageEffect", menuName = "Dice Orbit/Skills/Effects/Mage Stack Damage")]
    public class MageStackDamageEffect : SkillEffectBase
    {
        [Tooltip("주사위 눈금에 곱해질 기본 배율")]
        public int baseMultiplier = 12;
        
        [Tooltip("스택 1개당 추가되는 피해량 비율 (예: 0.05 = 5%)")]
        public float bonusDamageRatioPerStack = 0.05f;

        public int GetBaseMultiplierForSource(Unit source)
        {
            int level = ResolveSourceActiveAbilityLevel(source);
            return GetBaseMultiplierForLevel(level);
        }

        public int GetBaseMultiplierForLevel(int level)
        {
            int normalizedLevel = Mathf.Max(1, level);
            int baseValue = Mathf.Max(1, baseMultiplier);
            return baseValue + (normalizedLevel - 1);
        }

        public float GetBonusRatioForSource(Unit source)
        {
            int level = ResolveSourceActiveAbilityLevel(source);
            return GetBonusRatioForLevel(level);
        }

        public float GetBonusRatioForLevel(int level)
        {
            int normalizedLevel = Mathf.Max(1, level);
            float baseRatio = Mathf.Max(0f, bonusDamageRatioPerStack);
            return baseRatio + ((normalizedLevel - 1) * 0.01f);
        }

        public override void Execute(Unit source, List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            if (targets == null) return;
            
            int resolvedBaseMultiplier = GetBaseMultiplierForSource(source);
            int baseDamage = diceValue * resolvedBaseMultiplier;
            
            int focusStacks = source != null && source.StatusEffects != null ? source.StatusEffects.GetEffectValue(EffectType.Focus) : 0;
            float bonusRatio = GetBonusRatioForSource(source);
            
            float finalDamageFloat = baseDamage * (1.0f + (focusStacks * bonusRatio));
            int finalDamage = Mathf.RoundToInt(finalDamageFloat);

            VfxManager.PlayCast(vfxProfile, source);
            
            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                var action = new CombatAction("Energy Ball", ActionType.Attack, finalDamage);
                if (vfxProfile != null)
                {
                    action.AddTag("CustomVfx");
                }

                var context = new CombatContext(
                    source,
                    target,
                    action
                );
                CombatPipeline.Instance?.Process(context);

                if (context.IsEffected)
                {
                    VfxManager.PlayHit(vfxProfile, target);
                }
            }
            
            if (source != null && source.StatusEffects != null && focusStacks > 0)
            {
                source.StatusEffects.RemoveEffect(EffectType.Focus);
            }
        }
    }
}

