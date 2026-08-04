using UnityEngine;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>
    /// 튜토리얼 UI 공용 스킨 — 패널/버튼 스프라이트의 단일 소스(Resources/TutorialSkin).
    /// 오버레이 말풍선(TutorialOverlayUI)과 시작 프롬프트(TutorialPromptUI)가 같은 룩을 공유한다.
    /// 애셋이 없으면 각 UI가 단색으로 폴백한다.
    /// </summary>
    [CreateAssetMenu(menuName = "DiceOrbit/Tutorial Skin", fileName = "TutorialSkin")]
    public class TutorialSkin : ScriptableObject
    {
        public Sprite panelSprite;    // 패널/말풍선 배경 (오른쪽 패널, 9-slice)
        public Sprite buttonSprite;   // 버튼 배경 (환경설정 UISprite, 9-slice)

        private static TutorialSkin _cached;

        /// <summary>Resources/TutorialSkin 로드(캐시). 없으면 null.</summary>
        public static TutorialSkin Get()
        {
            if (_cached == null) _cached = Resources.Load<TutorialSkin>("TutorialSkin");
            return _cached;
        }
    }
}
