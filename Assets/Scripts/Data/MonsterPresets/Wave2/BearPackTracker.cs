using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave2
{
    /// <summary>
    /// 곰 세트(아기곰 / 엄마곰)가 공유하는 웨이브 단위 상태.
    /// - HoneyEaten: 플레이어가 먹은 꿀 총량 (아기곰 피해 증가에 사용)
    /// - BabyBearHits: 아기곰이 피격당한 횟수 (엄마곰 [분노] 피해 증가에 사용)
    /// - LastBabyBearAttacker: 아기곰을 마지막으로 공격한 캐릭터 (엄마곰 [찢어] 타겟)
    /// 웨이브 시작마다 초기화되고, 잔여 꿀 타일도 함께 제거된다.
    /// </summary>
    public static class BearPackTracker
    {
        public static int HoneyEaten { get; private set; }
        public static int BabyBearHits { get; private set; }
        public static Character LastBabyBearAttacker { get; private set; }

        private static WaveManager hookedManager;

        public static void Reset()
        {
            HoneyEaten = 0;
            BabyBearHits = 0;
            LastBabyBearAttacker = null;
        }

        public static void RegisterHoneyEaten()
        {
            HoneyEaten++;
        }

        public static void RegisterBabyBearHit(Unit attacker)
        {
            BabyBearHits++;
            if (attacker is Character c) LastBabyBearAttacker = c;
        }

        /// <summary>웨이브 시작마다 상태 초기화 + 잔여 꿀 타일 제거 훅을 보장한다(중복 구독 방지).</summary>
        public static void EnsureWaveHook()
        {
            var wm = WaveManager.Instance;
            if (wm == null) return;
            if (hookedManager == wm) return;

            if (hookedManager != null) hookedManager.OnWaveStart -= OnWaveStart;
            wm.OnWaveStart += OnWaveStart;
            hookedManager = wm;
        }

        private static void OnWaveStart(int wave)
        {
            Reset();
            ClearHoneyTiles();
        }

        private static void ClearHoneyTiles()
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return;

            foreach (var tile in orbit.Tiles)
            {
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Honey);
            }
        }
    }
}
