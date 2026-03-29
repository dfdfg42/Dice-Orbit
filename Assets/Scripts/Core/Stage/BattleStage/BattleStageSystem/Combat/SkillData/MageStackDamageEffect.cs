using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Passives;
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

        public float GetBonusRatioForSource(Unit source)
        {
            if (source?.Passives?.ActivePassives != null)
            {
                foreach (var passive in source.Passives.ActivePassives)
                {
                    if (passive is FocusPassive focusPassive)
                    {
                        return focusPassive.BonusDamageRatioPerStack;
                    }
                }
            }

            return bonusDamageRatioPerStack;
        }

        public override void Execute(Unit source, List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            if (targets == null) return;
            
            int baseDamage = diceValue * baseMultiplier;
            
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

