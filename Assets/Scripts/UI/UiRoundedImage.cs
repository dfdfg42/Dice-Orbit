using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 씬에 배치하는 둥근 사각 Image. UiRoundedSprite는 런타임 생성 텍스처라 씬에 직렬화할 수 없으므로,
    /// 반경만 저장해 두고 Awake에서 스프라이트를 꽂는다 — 보상창 등 하이라키 authored UI 용 (2026-08-28).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UiRoundedImage : MonoBehaviour
    {
        [Tooltip("모서리 반경(px)")]
        [SerializeField] private int radius = 25;

        private void Awake()
        {
            var img = GetComponent<Image>();
            img.sprite = UiRoundedSprite.Get(Mathf.Max(1, radius));
            img.type = Image.Type.Sliced;
        }
    }
}
