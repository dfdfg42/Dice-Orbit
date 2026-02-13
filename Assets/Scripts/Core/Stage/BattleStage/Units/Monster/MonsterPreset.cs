using UnityEngine;
using System.Collections.Generic;

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
        public List<SkillData> Skills = new List<SkillData>();
        
        [Header("Passives")]
        public List<Passives.PassiveAbility> PassiveAbilities = new List<Passives.PassiveAbility>();
        
        [Header("Visual")]
        public Sprite MonsterSprite;
        public Color SpriteColor = Color.white;
    }
}
