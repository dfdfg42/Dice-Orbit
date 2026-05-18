using System.Collections.Generic;
using DiceOrbit.Core;
using UnityEngine;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 전투 중 패시브·상태이상 발동 시 유닛 위에 떠오르는 텍스트 버블을 생성합니다.
    /// 같은 유닛에 짧은 시간 내 여러 알림이 몰리면 Y 오프셋으로 순차적으로 쌓습니다.
    /// </summary>
    public static class CombatNotifier
    {
        private const float BaseHeight    = 2.2f;
        private const float StaggerStep   = 0.65f;
        private const float BatchResetSec = 0.45f;

        // 유닛별 (마지막 알림 시간, 현재 배치 인덱스)
        private static readonly Dictionary<Unit, (float lastTime, int count)> Counters = new();

        public static readonly Color DefaultPassiveColor = new Color(1f, 0.85f, 0.35f, 1f);
        public static readonly Color DefaultStatusColor  = new Color(0.65f, 0.9f, 1f,   1f);

        /// <summary>유닛 위에 텍스트 버블을 띄웁니다.</summary>
        public static void Notify(Unit unit, string text, Color color)
        {
            if (unit == null || string.IsNullOrWhiteSpace(text)) return;

            float now   = Time.time;
            int   index = 0;

            if (Counters.TryGetValue(unit, out var state) && now - state.lastTime < BatchResetSec)
                index = state.count;

            Counters[unit] = (now, index + 1);

            Vector3 pos = unit.transform.position + Vector3.up * (BaseHeight + index * StaggerStep);
            FloatingLabelPopup.Create(text, color, pos);
        }

        public static void NotifyPassive(Unit unit, string passiveName)
            => Notify(unit, passiveName, DefaultPassiveColor);

        public static void NotifyStatus(Unit unit, string statusName, Color color)
            => Notify(unit, statusName, color);
    }
}
