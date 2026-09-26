using System;
using DiceOrbit.UI.Skin;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 씬에 배치하는 스킨 버튼 (전용 스프라이트 스펙 2026-09-26 §4). 역할만 저장해 두고
    /// Awake/OnValidate에서 UiSkin의 4상태 스프라이트를 SpriteSwap으로 꽂는다 — 씬에 상태 스프라이트를 직접 배선하지 않는다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UiSkinButton : MonoBehaviour
    {
        [Tooltip("버튼 역할 — 「도구/Dice Orbit/UI 스킨 점검」으로 에셋 상태 확인")]
        [SerializeField] private SkinButton button = SkinButton.EndTurn;

        public SkinButton Button => button;

        public void SetButton(SkinButton role)
        {
            button = role;
            Apply();
        }

        private void Awake() => Apply();

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            Apply();   // 인스펙터에서 역할을 바꾸면 씬에 바로 반영
        }

        public void Apply()
        {
            var btn = GetComponent<Button>();
            if (btn == null) throw new InvalidOperationException($"[UiSkinButton] 「{name}」에 Button이 없습니다.");
            UiSkin.Current.ApplyButton(btn, button);
        }
    }
}
