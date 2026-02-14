using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;

namespace DiceOrbit.Data.Monsters
{
    /// <summary>
    /// 몬스터 프리셋 (스탯, 스킬, AI, 비주얼 정의)
    /// </summary>
    [CreateAssetMenu(fileName = "New MonsterPreset", menuName = "DiceOrbit/Monster/Monster Preset")]
    public class MonsterPreset : ScriptableObject
    {
        [Header("Stats")]
        public MonsterStats BaseStats;
        
        [Header("AI & Skills")]
        public MonsterAI.MonsterAI AIPattern;
        
        [Header("Starting Passives")]
        public List<Passives.PassiveAbility> StartingPassives = new List<Passives.PassiveAbility>();
        
        [Header("Visual")]
        public Sprite MonsterSprite;
        public Color SpriteColor = Color.white;

        public MonsterStats CreateStats()
        {
            var stats = BaseStats != null ? BaseStats.DeepCopy() : new MonsterStats();

            if (MonsterSprite != null)
            {
                stats.MonsterSprite = MonsterSprite;
            }
            stats.SpriteColor = SpriteColor;
            return stats;
        }

        public List<Passives.PassiveAbility> GetStartingPassives()
        {
            return StartingPassives ?? new List<Passives.PassiveAbility>();
        }
    }
}
