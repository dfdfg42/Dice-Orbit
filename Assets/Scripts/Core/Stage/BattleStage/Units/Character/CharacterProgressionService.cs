using DiceOrbit.Data.Skills;

namespace DiceOrbit.Core
{
    public static class CharacterProgressionService
    {
        public static void ApplyLevelUp(Character character)
        {
            if (character == null || character.Stats == null) return;

            // 현재 정책: 레벨업마다 액티브 1개와 패시브 1개를 자동 강화합니다.
            ApplyAutoSelection(character);

            // 이미 등록된 패시브 인스턴스에 강화된 레벨을 반영합니다.
            character.SyncPassiveLevelsFromRuntime();
        }

        private static void ApplyAutoSelection(Character character)
        {
            TryUpgradeFirstByType(character, CharacterSkillType.Active);
            TryUpgradeFirstByType(character, CharacterSkillType.Passive);
        }

        private static bool TryUpgradeFirstByType(Character character, CharacterSkillType type)
        {
            foreach (var ability in character.Stats.RuntimeAbilities)
            {
                if (ability == null || ability.AbilityType != type) continue;
                // 동작 예측 가능성을 위해 "첫 매칭" 규칙으로 고정 선택합니다.
                if (ability.TryUpgrade()) return true;
            }

            return false;
        }
    }
}
