using System.Linq;
using DiceOrbit.Core;

namespace DiceOrbit.Data.MonsterPresets.Wave5.Shared
{
    /// <summary>불꽃 세트 공용 조회(순수 함수, 상태 없음).</summary>
    public static class FlameSet
    {
        public const string BossName = "불꽃 소녀";

        /// <summary>살아있는 불꽃 소녀(보스)를 이름으로 찾는다. 없으면 null.</summary>
        public static Monster FindBoss()
        {
            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) return null;
            return monsters.FirstOrDefault(m => m != null && m.IsAlive && m.Stats != null && m.Stats.MonsterName == BossName);
        }
    }
}
