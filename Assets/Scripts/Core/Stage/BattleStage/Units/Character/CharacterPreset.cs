using UnityEngine;
using System.Collections.Generic;
using System;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Passives;
using UnityEngine.Serialization;

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

    [FormerlySerializedAs("StartingPassives")]
    [SerializeReference, HideInInspector]
    private List<PassiveAbility> legacyStartingPassives = new List<PassiveAbility>();


        
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

            EnsurePassiveAbilityConfigured(stats);
            
            // Set Source Preset reference
            stats.SourcePreset = this;
            stats.NormalizeRuntimeAbilities();
            
            return stats;
        }

        private void EnsurePassiveAbilityConfigured(CharacterStats stats)
        {
            if (stats == null)
            {
                return;
            }

            if (HasPassiveAbility(stats))
            {
                return;
            }

            // 구 프리셋의 StartingPassives 직렬화 데이터를 신규 RuntimeAbility 구조로 마이그레이션합니다.
            if (legacyStartingPassives != null)
            {
                foreach (var legacyPassive in legacyStartingPassives)
                {
                    if (legacyPassive == null) continue;
                    stats.RuntimeAbilities.Add(new RuntimeAbility(CreatePassiveSkillFromTemplate(legacyPassive)));
                }
            }

            if (HasPassiveAbility(stats))
            {
                return;
            }

            // 완전히 비어 있는 경우(예: 알케/메이지) 기본 패시브를 보장합니다.
            var fallbackPassive = CreateDefaultPassiveSkillByCharacterName(CharacterName);
            if (fallbackPassive != null)
            {
                stats.RuntimeAbilities.Add(new RuntimeAbility(fallbackPassive));
            }
        }

        private static bool HasPassiveAbility(CharacterStats stats)
        {
            if (stats?.RuntimeAbilities == null) return false;

            foreach (var ability in stats.RuntimeAbilities)
            {
                if (ability != null && ability.AbilityType == CharacterSkillType.Passive)
                {
                    return true;
                }
            }

            return false;
        }

        private static CharacterSkill CreatePassiveSkillFromTemplate(PassiveAbility template)
        {
            var skill = ScriptableObject.CreateInstance<CharacterSkill>();
            string passiveName = string.IsNullOrWhiteSpace(template?.PassiveName) ? "Passive" : template.PassiveName;
            string passiveDescription = template?.Description ?? string.Empty;

            skill.SkillName = passiveName;
            skill.Description = passiveDescription;
            skill.Type = CharacterSkillType.Passive;
            skill.PassiveTemplate = template?.Clone();
            skill.MaxLevelOverride = 5;
            skill.Requirement = new DiceRequirement();
            skill.Levels = new List<SkillLevelData>();
            return skill;
        }

        private static CharacterSkill CreateDefaultPassiveSkillByCharacterName(string characterName)
        {
            string normalized = (characterName ?? string.Empty).Trim();

            if (ContainsAny(normalized, "전사", "Warrior"))
            {
                var passive = new BattleCryPassive { damageMultiplier = 1.05f };
                passive.ConfigureMetadata("전우애", "공격 피해량 5% 증가");
                return CreatePassiveSkillFromTemplate(passive);
            }

            if (ContainsAny(normalized, "연금술사", "Alchemist"))
            {
                var passive = new StableReactionPassive
                {
                    damageMultiplier = 1.10f,
                    healthThresholdRatio = 0.6f,
                };
                passive.ConfigureMetadata("안정 반응", "현재 체력이 60% 이상일 경우, 피해량 10% 증가");
                return CreatePassiveSkillFromTemplate(passive);
            }

            if (ContainsAny(normalized, "도적", "Rogue"))
            {
                var passive = new PositioningPassive
                {
                    damageMultiplier = 1.05f,
                    thresholdDistance = 5,
                };
                passive.ConfigureMetadata("위치 선정", "한 턴에 5칸 이상 이동 시 다음 공격의 피해량 5% 증가");
                return CreatePassiveSkillFromTemplate(passive);
            }

            if (ContainsAny(normalized, "마법사", "Mage"))
            {
                var passive = new FocusPassive { stacksPerTurn = 1 };
                passive.ConfigureMetadata("정신 집중", "매 턴 종료 시 집중 스택 +1 획득 (액티브 발동 시 스택당 5% 추가 피해 후 소모)");
                return CreatePassiveSkillFromTemplate(passive);
            }

            return null;
        }

        private static bool ContainsAny(string source, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(source) || keywords == null) return false;

            foreach (var keyword in keywords)
            {
                if (string.IsNullOrWhiteSpace(keyword)) continue;
                if (source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
