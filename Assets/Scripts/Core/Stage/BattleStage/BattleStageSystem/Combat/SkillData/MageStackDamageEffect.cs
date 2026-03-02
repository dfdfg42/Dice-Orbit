using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;

namespace DiceOrbit.Data.Skills.Effects
{
    [CreateAssetMenu(fileName = "MageStackDamageEffect", menuName = "Dice Orbit/Skills/Effects/Mage Stack Damage")]
    public class MageStackDamageEffect : SkillEffectBase
    {
        [Tooltip("주사위 눈금에 곱해질 기본 배율")]
        public int baseMultiplier = 12;
        
        [Tooltip("스택 1개당 추가되는 피해량 비율 (예: 0.05 = 5%)")]
        public float bonusDamageRatioPerStack = 0.05f;

        public override void Execute(Unit source, List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            if (targets == null) return;
            
            int baseDamage = diceValue * baseMultiplier;
            
            int focusStacks = source != null && source.StatusEffects != null ? source.StatusEffects.GetEffectValue(EffectType.Focus) : 0;
            
            float finalDamageFloat = baseDamage * (1.0f + (focusStacks * bonusDamageRatioPerStack));
            int finalDamage = Mathf.RoundToInt(finalDamageFloat);
            
            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                var context = new CombatContext(
                    source,
                    target,
                    new CombatAction("Energy Ball", ActionType.Attack, finalDamage)
                );
                CombatPipeline.Instance?.Process(context);
            }
            
            if (source != null && source.StatusEffects != null && focusStacks > 0)
            {
                source.StatusEffects.RemoveEffect(EffectType.Focus);
            }
        }
    }
}

