using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 주사위 한 면의 겉모습 — 종류(<see cref="DieDefinitionSO"/>)마다 다른 면 그림 + 그 위 숫자 색 (2026-10-05).
    /// 주사위를 그리는 곳(트레이 DiceElement · 호버 툴팁의 6면 · 보상 화면 주사위 카드)이 모두 여기를 거친다 —
    /// 새로 주사위를 그리는 화면도 이 한 줄만 부르면 된다.
    /// </summary>
    public static class DieFaceStyle
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();

        /// <summary>
        /// body에 그 주사위의 면 그림을, number에 숫자 색을 입힌다 (둘 중 하나는 null이어도 된다).
        /// 면 그림이 비어 있으면 에러를 한 번 남기고 그대로 둔다 — 다른 주사위의 그림으로 대체하지 않는다.
        /// </summary>
        public static void Apply(DieDefinitionSO die, Image body, TMP_Text number)
        {
            if (die == null) throw new ArgumentNullException(nameof(die));

            if (die.Face == null)
            {
                if (Reported.Add(die.name))
                    Debug.LogError($"[DieFaceStyle] 주사위 '{die.name}'의 Face가 비어 있습니다 — 메뉴 [DiceOrbit/Assign Die Faces]로 배선하세요.", die);
                return;
            }

            if (body != null)
            {
                body.sprite = die.Face;
                body.type = Image.Type.Simple;
                body.preserveAspect = true;
            }
            if (number != null) number.color = die.NumberColor;
        }
    }
}
