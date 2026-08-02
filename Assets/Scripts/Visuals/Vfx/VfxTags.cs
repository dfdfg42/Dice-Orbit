using System.Collections.Generic;

namespace DiceOrbit.Visuals
{
    /// <summary>VFX 큐 태그 상수 + 계층 폴백 헬퍼 (GAS GameplayTag 스타일).</summary>
    public static class VfxTags
    {
        public const string Cast = "cast";
        public const string Impact = "impact";
        public const string Heal = "heal";
        public const string TileImpact = "tileImpact";
        public const string Death = "death";
        public const string Potion = "potion";
        public const string Artifact = "artifact";
        public const string CombatStart = "combatStart";
        public const string Victory = "victory";
        public const string Defeat = "defeat";
        public const string LevelUp = "levelUp";
        public const string Status = "status";
        public const string Summon = "summon";

        /// <summary>태그와 조상들을 구체→일반 순으로 반환. "impact.fire" → impact.fire, impact.</summary>
        public static IEnumerable<string> Lineage(string tag)
        {
            while (!string.IsNullOrEmpty(tag))
            {
                yield return tag;
                int i = tag.LastIndexOf('.');
                tag = i < 0 ? null : tag.Substring(0, i);
            }
        }
    }
}
