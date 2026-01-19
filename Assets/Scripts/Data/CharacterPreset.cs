using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 캐릭터 프리셋 (선택 가능한 캐릭터)
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterPreset", menuName = "DiceOrbit/Character Preset")]
    public class CharacterPreset : ScriptableObject
    {
        [Header("Basic Info")]
        public string CharacterName = "Hero";
        public Sprite Portrait;
        
        [Header("Description")]
        [TextArea(3, 5)]
        public string Description;
        
        [Header("Base Stats")]
        public int MaxHP = 30;
        public int Attack = 5;
        public int Defense = 0;
        public Sprite CharacterSprite;
        public Color SpriteColor = Color.white;
        
        [Header("Starting Skills")]
        public List<SkillData> ActiveSkills = new List<SkillData>();
        public List<SkillData> PassiveSkills = new List<SkillData>();
        
        /// <summary>
        /// CharacterStats 생성
        /// </summary>
        public CharacterStats CreateStats()
        {
            var stats = new CharacterStats
            {
                CharacterName = this.CharacterName,
                Level = 1,
                MaxHP = this.MaxHP,
                CurrentHP = this.MaxHP,
                Attack = this.Attack,
                Defense = this.Defense,
                CharacterSprite = this.CharacterSprite,
                SpriteColor = this.SpriteColor
            };
            
            // 스킬 복사
            stats.ActiveSkills = new List<SkillData>(ActiveSkills);
            stats.PassiveSkills = new List<SkillData>(PassiveSkills);
            
            return stats;
        }
    }
}
