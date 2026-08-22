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
        private const float BaseHeight = 2.2f;

        public static readonly Color DefaultPassiveColor = new Color(1f, 0.85f, 0.35f, 1f);
        public static readonly Color DefaultStatusColor  = new Color(0.65f, 0.9f, 1f,   1f);

        /// <summary>
        /// 유닛 위에 텍스트 버블을 띄웁니다. 표시 대기열을 거치므로 같은 프레임에 여러 개가
        /// 발생해도 피해 숫자와 뒤섞이지 않고 요청 순서대로 하나씩 뜹니다.
        /// (쌓임 오프셋도 대기열이 담당 — FloatingPopupQueue)
        /// </summary>
        public static void Notify(Unit unit, string text, Color color)
        {
            if (unit == null || string.IsNullOrWhiteSpace(text)) return;

            string shown = text;
            Color  tint  = color;
            FloatingPopupQueue.Enqueue(unit.transform, BaseHeight, pos => FloatingLabelPopup.Create(shown, tint, pos));
        }

        public static void NotifyPassive(Unit unit, string passiveName)
            => Notify(unit, passiveName, DefaultPassiveColor);

        public static void NotifyStatus(Unit unit, string statusName, Color color)
            => Notify(unit, statusName, color);
    }
}
