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
        public MonsterAI.MonsterAI AIPattern = new DiceOrbit.Data.MonsterAI.Patterns.SequentialPattern();

        [Header("Starting Passives")]
        [SerializeReference] // 다형성 직렬화 지원
        public List<DiceOrbit.Data.Passives.PassiveAbility> StartingPassives = new List<DiceOrbit.Data.Passives.PassiveAbility>();

        [Header("Death Effects")]
        [SerializeReference] // 사망 시 실행되는 효과들
        public List<DeathEffect> OnDeathEffects = new List<DeathEffect>();
        
        [Header("Visual")]
        public Sprite MonsterSprite;
        public Sprite AttackSprite;
        public Sprite DamageSprite;
        public Color SpriteColor = Color.white;
        public float VisualScale = 1.0f;

        public MonsterStats CreateStats()
        {
            var stats = BaseStats != null ? BaseStats.DeepCopy() : new MonsterStats();

            if (MonsterSprite != null)
            {
                stats.MonsterSprite = MonsterSprite;
            }
            if (AttackSprite != null)
            {
                stats.AttackSprite = AttackSprite;
            }
            if (DamageSprite != null)
            {
                stats.DamageSprite = DamageSprite;
            }
            stats.SpriteColor = SpriteColor;
            return stats;
        }

        public List<DiceOrbit.Data.Passives.PassiveAbility> GetStartingPassives()
        {
            return StartingPassives ?? new List<DiceOrbit.Data.Passives.PassiveAbility>();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (MonsterSprite == null)
            {
                MonsterSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/스프라이트/MonsterPreset/BasicIdle.png");
            }
            if (AttackSprite == null)
            {
                AttackSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/스프라이트/MonsterPreset/BasicAttack.png");
            }
            if (DamageSprite == null)
            {
                DamageSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/스프라이트/MonsterPreset/BasicDamaged.png");
            }
        }
#endif
    }
}
