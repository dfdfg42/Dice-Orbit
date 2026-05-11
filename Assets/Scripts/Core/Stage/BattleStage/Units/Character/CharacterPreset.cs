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
        public Sprite CharacterSprite;
        public Color SpriteColor = Color.white;
        public float VisualScale = 1.0f;

        [Header("Animation Sprites")]
        public Sprite IdleSprite;
        public Sprite MoveSprite;
        public Sprite DamageSprite;
        public Sprite SkillSprite;

        [Header("Animator")]
        [Tooltip("캐릭터 전용 Animator Controller 또는 Animator Override Controller")]
        public RuntimeAnimatorController AnimatorController;

        [Header("Starting Skills")]
        // 액티브/패시브 모두 SkillAsset 기반으로 일원화되어 등록됩니다.
        // Inspector에서 직접 SO를 드래그앤드롭으로 추가하세요.
        public List<SkillAsset> StartingSkills = new List<SkillAsset>();

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
                CharacterSprite = this.CharacterSprite,
                SpriteColor = this.SpriteColor
            };

            // 스킬 복사
            foreach (var skill in StartingSkills)
            {
                if (skill == null) continue;
                // 런타임 래퍼에서 캐릭터별 레벨 상태를 에셋과 분리해 관리합니다.
                stats.RuntimeAbilities.Add(new RuntimeAbility(skill));
            }

            stats.SourcePreset = this;
            stats.NormalizeRuntimeAbilities();

            return stats;
        }
    }
}
