using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;

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
        public float VisualScale = 1.0f;

        [Header("Animation Sprites")]
        public Sprite IdleSprite;
        public Sprite MoveSprite;
        public Sprite DamageSprite;
        public Sprite SkillSprite;
        

        [Header("Starting Skills (New System)")]
        // 액티브/패시브 모두 CharacterSkill 에셋으로 통일해서 등록합니다.
        public List<CharacterSkill> StartingSkills = new List<CharacterSkill>();


        
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
            
            // 스킬 복사 (New System)
            foreach(var skill in StartingSkills)
            {
                if(skill == null) continue;
                // 런타임 래퍼에서 캐릭터별 레벨 상태를 에셋과 분리해 관리합니다.
                stats.RuntimeAbilities.Add(new RuntimeAbility(skill));
            }
            
            // Set Source Preset reference
            stats.SourcePreset = this;
            stats.NormalizeRuntimeAbilities();
            
            return stats;
        }
    }
}
