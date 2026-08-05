using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave0.Shared
{
    /// <summary>
    /// 슬라임 세트 공유 로직.
    /// - 점액 타일은 몬스터 사망 후에도 유지되고 웨이브 종료(=다음 전투 시작) 시 정리.
    /// - 초록 슬라임 조건부 AI용 파란 슬라임 생존 판정.
    /// </summary>
    public static class SlimeSet
    {
        private static CombatManager hookedManager;

        /// <summary>파란 슬라임이 살아있는가 (초록 슬라임 조건부 AI용). 이름으로 식별.</summary>
        public static bool IsBlueSlimeAlive()
        {
            var cm = CombatManager.Instance;
            if (cm == null) return false;
            return cm.GetAliveMonsters().Any(m => m != null && m.Stats != null && m.Stats.MonsterName == "파란 슬라임");
        }

        public static void EnsureWaveHook()
        {
            var cm = CombatManager.Instance;
            if (cm == null || hookedManager == cm) return;
            if (hookedManager != null) hookedManager.OnCombatStart -= OnCombatStart;
            cm.OnCombatStart += OnCombatStart;
            hookedManager = cm;
        }

        private static void OnCombatStart()
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return;
            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Slime);
        }
    }
}
