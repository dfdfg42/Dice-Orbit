using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared
{
    /// <summary>
    /// 수정 세트 공유 로직.
    /// - 수정 중첩(stack): 수정 핵에 붙는 상태이상 <see cref="CrystalStackStatus"/>로 저장 → UI에 수치가 보이고 정적 전역이 사라짐.
    /// - 자수정 타일 정리: 웨이브 종료(=다음 전투 시작) 시 모든 타일의 자수정 속성 제거.
    /// </summary>
    public static class CrystalSet
    {
        private static CombatManager hookedManager;

        /// <summary>현재 살아있는 수정 핵(이름으로 식별). 없으면 null.</summary>
        public static Monster GetCore()
        {
            var cm = CombatManager.Instance;
            if (cm == null) return null;
            return cm.GetAliveMonsters().FirstOrDefault(m => m != null && m.Stats != null && m.Stats.MonsterName == "수정 핵");
        }

        /// <summary>수정 핵에 수정 중첩 n 추가(상태이상 누적).</summary>
        public static void AddStack(Monster core, int n)
        {
            if (core == null || core.StatusEffects == null || n <= 0) return;
            core.StatusEffects.AddEffect(new CrystalStackStatus(n));
        }

        /// <summary>수정 핵의 현재 수정 중첩. 없으면 0.</summary>
        public static int GetStacks(Monster core)
        {
            if (core == null || core.StatusEffects == null) return 0;
            return core.StatusEffects.GetEffectValue(EffectType.CrystalStack);
        }

        /// <summary>수정 중첩 0으로 초기화(상태이상 제거).</summary>
        public static void ResetStacks(Monster core)
        {
            if (core == null || core.StatusEffects == null) return;
            core.StatusEffects.RemoveEffect(EffectType.CrystalStack);
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
            // 수정 중첩은 수정 핵 인스턴스의 상태이상이라 새 전투에선 자동으로 사라짐. 자수정 타일만 정리한다.
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return;
            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Amethyst);
        }
    }

    /// <summary>
    /// [수정 중첩] 수정 핵에 쌓이는 스택 카운터. 상태 아이콘 줄에 수치가 표시된다(가시화).
    /// 순수 카운터(전투 훅 없음), 영구 지속(-1), 중첩 누적(IsStackable).
    /// </summary>
    public class CrystalStackStatus : StatusEffect
    {
        public CrystalStackStatus(int amount) : base(EffectType.CrystalStack, amount, -1, isStackable: true) { }
    }
}
