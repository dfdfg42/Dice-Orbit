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
            Level++;
            MaxHP += 5;
            CurrentHP = MaxHP; // 풀 회복
            Attack += 2;

            Debug.Log($"{CharacterName} leveled up to {Level}! HP: {MaxHP}, ATK: {Attack}");
        }

    }
}
