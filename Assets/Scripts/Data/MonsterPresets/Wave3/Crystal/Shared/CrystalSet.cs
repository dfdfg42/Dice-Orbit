using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared
{
    /// <summary>
    /// 수정 세트 공유 상태.
    /// - 수정 중첩(stack): 수정 핵 인스턴스에 귀속. 자수정 타일/결정화 패시브가 공급, 수정 핵 패턴이 소비.
    /// - 자수정 타일은 수정 핵 사망 후에도(지연 제거 전까지) 유지되고, 웨이브 종료(=다음 전투 시작) 시 정리.
    /// SlimeSet 패턴 미러.
    /// </summary>
    public static class CrystalSet
    {
        private static readonly Dictionary<Monster, int> Stacks = new();
        private static CombatManager hookedManager;

        /// <summary>현재 살아있는 수정 핵(이름으로 식별). 없으면 null.</summary>
        public static Monster GetCore()
        {
            var cm = CombatManager.Instance;
            if (cm == null) return null;
            return cm.GetAliveMonsters().FirstOrDefault(m => m != null && m.Stats != null && m.Stats.MonsterName == "수정 핵");
        }

        public static void AddStack(Monster core, int n)
        {
            if (core == null) return;
            Stacks.TryGetValue(core, out int cur);
            Stacks[core] = cur + Mathf.Max(0, n);
        }

        public static int GetStacks(Monster core)
        {
            if (core == null) return 0;
            Stacks.TryGetValue(core, out int cur);
            return cur;
        }

        public static void ResetStacks(Monster core)
        {
            if (core != null) Stacks[core] = 0;
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
            Stacks.Clear();
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return;
            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Amethyst);
        }
    }
}
