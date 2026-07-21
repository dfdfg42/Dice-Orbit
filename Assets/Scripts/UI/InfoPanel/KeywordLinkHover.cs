using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 정보 패널 텍스트 안의 키워드 링크(&lt;link="kw:..."&gt;) 호버를 감지해
    /// 커서 옆 경량 툴팁(HoverTooltipUI)으로 정의를 띄운다.
    /// (구 키워드 섹션 대체 — 정의는 "물어볼 때"만, 패널 공간 절약)
    ///
    /// 키워드 링크가 삽입된 TMP는 Register()로 등록된다 (InfoPanelRows.AddText 경유).
    /// 파괴된 참조는 매 프레임 자연 정리.
    /// </summary>
    public class KeywordLinkHover : MonoBehaviour
    {
        private static readonly List<TextMeshProUGUI> _texts = new();

        /// <summary>키워드 링크가 포함된 TMP 등록.</summary>
        public static void Register(TextMeshProUGUI tmp)
        {
            if (tmp != null) _texts.Add(tmp);
        }

        private string _activeKeyword;

        private void Update()
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;

            string foundKeyword = null;
            string foundDesc = null;

            for (int i = _texts.Count - 1; i >= 0; i--)
            {
                var tmp = _texts[i];
                if (tmp == null) { _texts.RemoveAt(i); continue; }   // 파괴된 행 정리
                if (!tmp.gameObject.activeInHierarchy) continue;

                // 사각형 밖이면 링크 히트테스트 생략 (오버레이 캔버스 → 카메라 null)
                if (!RectTransformUtility.RectangleContainsScreenPoint(tmp.rectTransform, mousePos, null)) continue;

                int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmp, mousePos, null);
                if (linkIndex < 0) continue;

                string linkId = tmp.textInfo.linkInfo[linkIndex].GetLinkID();
                if (TooltipKeywordFormatter.TryGetDescriptionByLinkId(linkId, out var keyword, out var desc))
                {
                    foundKeyword = keyword;
                    foundDesc = desc;
                    break;
                }
            }

            if (foundKeyword == null)
            {
                if (_activeKeyword != null)
                {
                    _activeKeyword = null;
                    HoverTooltipUI.Instance?.HidePinned();
                }
                return;
            }

            if (foundKeyword == _activeKeyword) return;   // 같은 키워드 유지 중이면 갱신 불필요

            _activeKeyword = foundKeyword;
            HoverTooltipUI.EnsureInstance();
            HoverTooltipUI.Instance?.ShowPinned($"<b>[{foundKeyword}]</b>\n{foundDesc}");
        }
    }
}
