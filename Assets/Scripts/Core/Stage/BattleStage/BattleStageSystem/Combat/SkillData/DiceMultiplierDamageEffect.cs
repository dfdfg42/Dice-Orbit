using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Visuals;

namespace DiceOrbit.Data.Skills.Effects
{
    [CreateAssetMenu(fileName = "DiceMultiplierDamageEffect", menuName = "Dice Orbit/Skills/Effects/Dice Multiplier Damage")]
    public class DiceMultiplierDamageEffect : SkillEffectBase
    {
        [Tooltip("주사위 눈금에 곱해질 배율")]
        public int multiplier = 12;

        public override void Execute(Unit source, List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            int damage = diceValue * multiplier;
            
            if (targets == null) return;

            VfxManager.PlayCast(vfxProfile, source);
            
            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                var action = new CombatAction("Attack", ActionType.Attack, damage);
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
        }
    }
}
