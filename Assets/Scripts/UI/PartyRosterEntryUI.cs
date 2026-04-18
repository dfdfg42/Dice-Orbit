using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 파티 로스터 엔트리 1개를 표현하는 프리팹 바인딩 컴포넌트입니다.
    /// </summary>
    public class PartyRosterEntryUI : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private RectTransform root;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI nameLevelText;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("Style")]
        [SerializeField] private Color baseBackgroundColor = new Color(0f, 0f, 0f, 1f);

        public RectTransform Root => root != null ? root : (RectTransform)transform;
        public Image BackgroundImage => backgroundImage;
        public Image PortraitImage => portraitImage;
        public TextMeshProUGUI NameLevelText => nameLevelText;
        public Slider HpSlider => hpSlider;
        public TextMeshProUGUI HpText => hpText;

        public SpriteRenderer LivePortraitSource { get; set; }

        public Core.Character Character { get; private set; }

        private void Reset()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (portraitImage == null)
            {
                portraitImage = transform.Find("Portrait")?.GetComponent<Image>();
            }

            if (nameLevelText == null)
            {
                nameLevelText = GetComponentInChildren<TextMeshProUGUI>();
            }

            if (hpSlider == null)
            {
                hpSlider = GetComponentInChildren<Slider>();
            }

            if (hpText == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>();
                if (texts != null && texts.Length > 1)
                {
                    hpText = texts[texts.Length - 1];
                }
            }
        }

        public void Bind(Core.Character character)
        {
            Character = character;
        }

        public void SetAlpha(float alpha)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(baseBackgroundColor.r, baseBackgroundColor.g, baseBackgroundColor.b, baseBackgroundColor.a * alpha);
            }

            if (nameLevelText != null)
            {
                nameLevelText.alpha = alpha;
            }

            if (hpText != null)
            {
                hpText.alpha = alpha;
            }

            if (portraitImage != null)
            {
                portraitImage.color = new Color(1f, 1f, 1f, alpha);
            }
        }
    }
}
