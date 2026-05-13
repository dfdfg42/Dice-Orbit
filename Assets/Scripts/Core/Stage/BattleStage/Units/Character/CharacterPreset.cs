using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Core
{
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
        [SerializeReference]
        public List<CharacterActiveSkill> StartingActives = new List<CharacterActiveSkill>();

        [SerializeReference]
        public List<CharacterPassiveSkill> StartingPassives = new List<CharacterPassiveSkill>();

        public CharacterStats CreateStats()
        {
            var stats = new CharacterStats
            {
                CharacterName = this.CharacterName,
                Level         = 1,
                MaxHP         = this.MaxHP,
                CurrentHP     = this.MaxHP
            };

            foreach (var active in StartingActives)
            {
                if (active == null) continue;
                stats.ActiveAbilities.Add(new ActiveSkillSlot(active));
            }

            foreach (var passive in StartingPassives)
            {
                if (passive == null) continue;
                stats.PassiveInstances.Add(passive.Clone() as CharacterPassiveSkill);
            }

            stats.SourcePreset = this;
            return stats;
        }
    }
}
