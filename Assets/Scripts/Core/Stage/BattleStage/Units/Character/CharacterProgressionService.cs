namespace DiceOrbit.Core
{
    public static class CharacterProgressionService
    {
        public static void ApplyLevelUp(Character character)
        {
            if (character == null || character.Stats == null) return;

            // 액티브 첫 번째 업그레이드
            foreach (var ability in character.Stats.ActiveAbilities)
            {
                if (ability != null && ability.TryUpgrade()) break;
            }

            // 패시브 첫 번째 업그레이드
            foreach (var passive in character.Stats.PassiveInstances)
            {
                if (passive == null) continue;
                if (passive.CurrentLevel < passive.MaxLevel)
                {
                    passive.SetLevel(passive.CurrentLevel + 1);
                    break;
                }
            }
        }
    }
}
