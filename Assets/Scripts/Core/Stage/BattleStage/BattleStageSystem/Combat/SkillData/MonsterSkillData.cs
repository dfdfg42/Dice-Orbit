using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data.Skills.Effects;

namespace DiceOrbit.Data
{
    [System.Serializable]
    public class MonsterSkillData : SkillData
    {
        [Header("Skill Effects")]
        public List<SkillEffectBase> Effects = new List<SkillEffectBase>();

        public override void Execute(Core.Unit source, List<Core.Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (Effects == null || Effects.Count == 0) return;

            foreach (var effect in Effects)
            {
                if (effect == null) continue;
                effect.Execute(source, targetUnits, targetTiles, diceValue);
            }
        }
    }
}
