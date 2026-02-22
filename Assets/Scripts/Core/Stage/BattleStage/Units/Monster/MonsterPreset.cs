using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Passives;

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
        [SerializeReference] // Inspector에서 AI 타입 선택 가능
        public MonsterAI.MonsterAI AIPattern;
        
        [Header("Starting Passives")]
        [SerializeReference] // 다형성 직렬화 지원
        public List<DiceOrbit.Data.Passives.PassiveConfig> StartingPassives = new List<DiceOrbit.Data.Passives.PassiveConfig>();
        
        [Header("Visual")]
        public Sprite MonsterSprite;
        public Color SpriteColor = Color.white;
        public float VisualScale = 1.0f;

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

        public List<DiceOrbit.Data.Passives.PassiveConfig> GetStartingPassives()
        {
            return StartingPassives ?? new List<DiceOrbit.Data.Passives.PassiveConfig>();
        }
    }
}
