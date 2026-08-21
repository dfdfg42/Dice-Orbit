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
        [Tooltip("세이브 식별자 — 자동으로 채워집니다. 직접 수정하지 마세요.")]
        [SerializeField] private string saveId;

        /// <summary>세이브가 이 에셋을 다시 찾는 키. 한 번 정해지면 바뀌지 않는다.</summary>
        public string SaveId => saveId;

        [Header("Basic Info")]
        public string CharacterName = "Hero";
        public Sprite Portrait;
        public Sprite CharacterWindowSprite;

        [Header("Description")]
        [TextArea(3, 5)]
        public string Description;

        [Header("Base Stats")]
        public int MaxHP = 30;
        [Tooltip("기본 공격력. 매 턴 자동 기본공격의 피해량이며, 위치 패시브·모디파이어가 여기에 더해진다.")]
        public int Attack = 5;
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

        [Header("Modifier Configuration")]
        [HideInInspector]
        public string ModifierContextTypeName;

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
                MaxHP         = this.MaxHP,
                CurrentHP     = this.MaxHP,
                Attack        = this.Attack
            };

            if (Attack <= 0)
                Debug.LogError($"[CharacterPreset] '{CharacterName}'의 Attack이 {Attack}이다 — 자동 기본공격이 피해를 주지 못한다. 프리셋에 공격력을 설정할 것.");

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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(saveId)) saveId = name;
        }
#endif
    }
}
