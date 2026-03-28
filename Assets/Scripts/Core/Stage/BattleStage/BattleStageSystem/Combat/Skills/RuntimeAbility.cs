using System;
using DiceOrbit.Data.Passives;
using UnityEngine;

namespace DiceOrbit.Data.Skills
{
    [Serializable]
    public class RuntimeAbility
    {
        // 액티브/패시브 공통으로 사용하는 원본 스킬 에셋입니다.
        public CharacterSkill BaseSkill;
        // 캐릭터별 런타임 성장 상태(현재 레벨)입니다.
        public int CurrentLevel;

        // 패시브 능력일 때만 채워지는 런타임 패시브 인스턴스입니다.
        [NonSerialized] public PassiveAbility RuntimePassiveInstance;

        public RuntimeAbility(CharacterSkill skill, int initialLevel = 1)
        {
            BaseSkill = skill;
            int max = skill != null ? Mathf.Max(1, skill.MaxLevel) : 1;
            CurrentLevel = Mathf.Clamp(initialLevel, 1, max);
        }

        public CharacterSkillType AbilityType => BaseSkill != null ? BaseSkill.Type : CharacterSkillType.Active;

        public CharacterSkillData CurrentSkillData => BaseSkill?.GetSkillData(CurrentLevel);

        public SkillLevelData GetCurrentLevelData()
        {
            return BaseSkill?.GetLevelData(CurrentLevel);
        }

        public SkillLevelData GetNextLevelData()
        {
            return BaseSkill?.GetLevelData(CurrentLevel + 1);
        }

        public bool IsMaxLevel => BaseSkill == null || CurrentLevel >= BaseSkill.MaxLevel;

        public bool TryUpgrade()
        {
            if (BaseSkill == null || IsMaxLevel) return false;
            // 레벨만 올리고, 실제 동작 반영은 스킬/패시브 실행 경로에서 처리합니다.
            CurrentLevel++;
            return true;
        }
    }
}
