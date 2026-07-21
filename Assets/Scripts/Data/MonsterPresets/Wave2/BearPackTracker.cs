using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave2
{
    /// <summary>
    /// 곰 세트(아기곰 / 엄마곰)가 공유하는 웨이브 단위 상태.
    /// - HoneyEaten: 플레이어가 먹은 꿀 총량 (아기곰 [꿀을 좋아해] 피해 증가에 사용)
    /// - 한 턴 꿀 밟기 횟수: 캐릭터가 한 턴에 밟은 꿀 타일 수 (3개 이상이면 이동 불가)
    /// - HoneyTileCount: 현재 필드에 깔린 꿀 타일 수 (엄마곰 [꿀 묻은 털] 조건에 사용)
    /// 웨이브 시작마다 초기화되고, 잔여 꿀 타일도 함께 제거된다.
    /// </summary>
    public static class BearPackTracker
    {
        public static int HoneyEaten { get; private set; }

        private static CombatManager hookedManager;

        // 캐릭터별 (마지막으로 카운트한 턴, 그 턴의 꿀 밟기 횟수)
        private static readonly Dictionary<Character, (int turn, int count)> HoneySteps = new();

        public static void Reset()
        {
            HoneyEaten = 0;
            HoneySteps.Clear();
        }

        public static void RegisterHoneyEaten()
        {
            HoneyEaten++;
        }

        /// <summary>이번 턴 해당 캐릭터의 꿀 밟기 횟수를 1 증가시키고 누적값을 반환한다.</summary>
        public static int RegisterHoneyStep(Character character, int currentTurn)
        {
            if (character == null) return 0;

            if (!HoneySteps.TryGetValue(character, out var entry) || entry.turn != currentTurn)
                entry = (currentTurn, 0);

            entry.count++;
            HoneySteps[character] = entry;
            return entry.count;
        }

        /// <summary>현재 필드에 깔린 꿀 타일 수.</summary>
        public static int HoneyTileCount()
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return 0;

            int count = 0;
            foreach (var tile in orbit.Tiles)
                if (tile != null && tile.HasAttribute(TileAttributeType.Honey)) count++;
            return count;
        }

        /// <summary>전투 시작마다 상태 초기화 + 잔여 꿀 타일 제거 훅을 보장한다(중복 구독 방지).</summary>
        public static void EnsureWaveHook()
        {
            var cm = CombatManager.Instance;
            if (cm == null) return;
            if (hookedManager == cm) return;

            if (hookedManager != null) hookedManager.OnCombatStart -= OnCombatStart;
            cm.OnCombatStart += OnCombatStart;
            hookedManager = cm;
        }

        private static void OnCombatStart()
        {
            Reset();
            ClearHoneyTiles();
        }

        private static void ClearHoneyTiles()
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return;

            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Honey);
        }
    }
}
