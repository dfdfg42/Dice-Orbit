using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave0.Shared
{
    /// <summary>
    /// 슬라임 세트 공유 상태.
    /// - 파란 슬라임이 이번 라운드 받은 누적 피해 ([박치기] 취소 판정).
    /// - 점액 타일은 몬스터 사망 후에도 유지되고 웨이브 종료(=다음 전투 시작) 시 정리.
    /// </summary>
    public static class SlimeSet
    {
        private static readonly Dictionary<Monster, int> DamageTakenThisRound = new();
        private static CombatManager hookedManager;

        public static void AddDamageTaken(Monster slime, int amount)
        {
            if (slime == null) return;
            DamageTakenThisRound.TryGetValue(slime, out int cur);
            DamageTakenThisRound[slime] = cur + Mathf.Max(0, amount);
        }

        public static int GetDamageTaken(Monster slime)
        {
            if (slime == null) return 0;
            DamageTakenThisRound.TryGetValue(slime, out int cur);
            return cur;
        }

        public static void ResetDamageTaken(Monster slime)
        {
            if (slime != null) DamageTakenThisRound[slime] = 0;
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
            DamageTakenThisRound.Clear();
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return;
            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Slime);
        }
    }
}
