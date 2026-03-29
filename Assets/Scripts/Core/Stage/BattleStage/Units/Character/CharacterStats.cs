using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Skills;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 캐릭터 스탯 데이터
    /// </summary>
    [System.Serializable]
    public class CharacterStats : UnitStats
    {
        private static readonly int[] MaxHpIncreaseByLevel =
        {
            10,10,10,10,10,10,10,10,10,10,
            10,10,10,10,10,10,10,10,10,10,
            5,5,5,5,5,5,5,5,5,5,
            5,5,5,5,5,
            3,3,3,3,3,3,3,3,3,3,
            2,2,2,2,2,
        };

        private static readonly int[] PassiveCoefficientCurveA =
        {
            4,8,12,16,20,24,28,32,36,40,
            44,48,52,56,60,64,68,72,76,80,
            83,86,89,92,95,98,101,104,107,110,
            113,116,119,122,125,
            127,129,131,133,135,137,139,141,143,145,
            146,147,148,149,150,
        };

        private static readonly int[] PassiveCoefficientCurveB =
        {
            5,10,15,20,25,30,35,40,45,50,
            55,60,65,70,75,80,85,90,95,100,
            104,108,112,116,120,124,128,132,136,140,
            144,148,152,156,160,
            163,166,169,172,175,178,181,184,187,190,
            192,194,196,198,200,
        };

        [Header("Basic Info")]
        public string CharacterName = "Hero";
        public int Level = 1;
        
        [Header("Skills")]
        
        // 액티브/패시브를 함께 담는 단일 런타임 컨테이너입니다.
        public List<RuntimeAbility> RuntimeAbilities = new List<RuntimeAbility>();
        
        // Reference to the source preset for Draft Pool
        public Core.CharacterPreset SourcePreset;
        
        [Header("Visual")]
        public Sprite CharacterSprite;
        public Color SpriteColor = Color.white;

        [Header("Combat Stats")]
        public int MoveBuff = 0;
    public int MoveDebuff = 0;
        public int MoveOnThisTurn = 0; // 현재 턴에 이동한 거리 (디버프나 버프 계산용)

        public IEnumerable<RuntimeAbility> ActiveAbilities => RuntimeAbilities.Where(a => a != null && a.AbilityType == CharacterSkillType.Active);
        public IEnumerable<RuntimeAbility> PassiveAbilities => RuntimeAbilities.Where(a => a != null && a.AbilityType == CharacterSkillType.Passive);

        public int ActiveAbilityCount => RuntimeAbilities.Count(a => a != null && a.AbilityType == CharacterSkillType.Active);

        public RuntimeAbility GetActiveAbilityByIndex(int index)
        {
            if (index < 0) return null;

            int current = 0;
            foreach (var ability in RuntimeAbilities)
            {
                if (ability == null || ability.AbilityType != CharacterSkillType.Active) continue;
                if (current == index) return ability;
                current++;
            }

            return null;
        }

        public void NormalizeRuntimeAbilities()
        {
            if (RuntimeAbilities == null)
            {
                RuntimeAbilities = new List<RuntimeAbility>();
            }
        }

        /// <summary>
        /// 레벨업
        /// </summary>
        public void LevelUp()
        {
            // 기본 스탯 상승은 여기서 처리하고, 능력 레벨 상승은 별도 서비스에서 처리합니다.
            int hpIncrease = GetMaxHpIncreaseForLevel(Level);
            Level++;
            MaxHP += hpIncrease;
            CurrentHP = MaxHP; // 풀 회복
            Attack += 2;

            Debug.Log($"{CharacterName} leveled up to {Level}! HP +{hpIncrease} => {MaxHP}, ATK: {Attack}");
        }

        public static int GetMaxHpIncreaseForLevel(int level)
        {
            return GetCurveValue(MaxHpIncreaseByLevel, level);
        }

        public static int GetPassiveCoefficientA(int level)
        {
            return GetCurveValue(PassiveCoefficientCurveA, level);
        }

        public static int GetPassiveCoefficientB(int level)
        {
            return GetCurveValue(PassiveCoefficientCurveB, level);
        }

        public static float GetPassivePercentFromCurveA(int level, float basePercentAtLevel1)
        {
            return GetNormalizedPassivePercent(PassiveCoefficientCurveA, level, basePercentAtLevel1);
        }

        public static float GetPassivePercentFromCurveB(int level, float basePercentAtLevel1)
        {
            return GetNormalizedPassivePercent(PassiveCoefficientCurveB, level, basePercentAtLevel1);
        }

        private static int GetCurveValue(int[] curve, int level)
        {
            if (curve == null || curve.Length == 0) return 0;

            int normalizedLevel = Mathf.Max(1, level);
            int index = Mathf.Clamp(normalizedLevel - 1, 0, curve.Length - 1);
            return curve[index];
        }

        private static float GetNormalizedPassivePercent(int[] curve, int level, float basePercentAtLevel1)
        {
            if (curve == null || curve.Length == 0)
            {
                return Mathf.Max(0f, basePercentAtLevel1);
            }

            int levelOneValue = curve[0];
            int currentValue = GetCurveValue(curve, level);
            float delta = currentValue - levelOneValue;
            return Mathf.Max(0f, basePercentAtLevel1 + delta);
        }

    }
}
