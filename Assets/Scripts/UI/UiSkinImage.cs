using System;
using DiceOrbit.UI.Skin;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 씬에 배치하는 스킨 Image (2026-09-25 리스킨 3단계, 구 UiRoundedImage 개명 — .meta GUID 유지).
    /// 파트만 저장해 두고 Awake/OnValidate에서 UiSkin의 같은 헬퍼로 스프라이트를 꽂는다.
    /// 버튼 파트 + Button 컴포넌트가 있으면 상태 스프라이트(SpriteSwap)까지 세팅한다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UiSkinImage : MonoBehaviour
    {
        [Tooltip("스킨 파트 — 「도구/Dice Orbit/UI 스킨 점검」으로 에셋 상태 확인")]
        [SerializeField] private SkinPart part = SkinPart.Panel;

        public SkinPart Part => part;

        public void SetPart(SkinPart newPart)
        {
            part = newPart;
            Apply();
        }

        private void Awake() => Apply();

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            Apply();   // 인스펙터에서 파트를 바꾸면 씬에 바로 반영 (스프라이트 참조가 씬에 저장됨)
        }

        public void Apply()
        {
            var image = GetComponent<Image>();
            if (image == null) throw new InvalidOperationException($"[UiSkinImage] 「{name}」에 Image가 없습니다.");
            var skin = UiSkin.Current;

            if (part == SkinPart.ButtonPrimary || part == SkinPart.ButtonSecondary)
            {
                var button = GetComponent<Button>();
                if (button != null)
                {
                    skin.ApplyButton(button, part == SkinPart.ButtonPrimary ? ButtonKind.Primary : ButtonKind.Secondary);
                    return;
                }
            }
            skin.ApplySprite(image, part);
        }
    }
}
