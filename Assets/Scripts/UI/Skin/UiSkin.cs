using System;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI.Skin
{
    /// <summary>스킨 부품 이름 — Apply 헬퍼와 씬 배치용 UiSkinImage(리스킨 3단계)가 공유한다.</summary>
    public enum SkinPart { Panel, Card, Chip, Tooltip, Slot, Divider, ButtonPrimary, ButtonSecondary }

    public enum ButtonKind { Primary, Secondary }

    /// <summary>버튼 1종의 상태별 스프라이트. 틴트가 아니라 스프라이트 교체(SpriteSwap)로 상태를 표현한다.</summary>
    [Serializable]
    public class ButtonSpriteSet
    {
        public Sprite Normal;
        public Sprite Hover;
        public Sprite Pressed;
        public Sprite Disabled;
    }

    /// <summary>
    /// UI 스킨 단일 권위 (리스킨 스펙 2026-09-25 §3). 팔레트·9-slice·버튼 상태·아이콘을 한 에셋에 모은다.
    /// 코드 생성 UI는 색상 상수 대신 <see cref="Current"/>를 읽고, 씬 UI는 UiSkinImage로 파트를 지정한다.
    /// 에셋이 없거나 필드가 비면 예외 — 기본색·기본 스프라이트로 조용히 굴러가지 않는다.
    /// 점검: 「도구/Dice Orbit/UI 스킨 점검」.
    /// </summary>
    [CreateAssetMenu(fileName = "UiSkin", menuName = "Dice Orbit/UI/UI Skin")]
    public class UiSkin : ScriptableObject
    {
        public const string ResourcePath = "UI/UiSkin";
        private static UiSkin _current;

        /// <summary>Resources/UI/UiSkin.asset. 없으면 InvalidOperationException.</summary>
        public static UiSkin Current
        {
            get
            {
                if (_current == null) _current = LoadOrThrow(ResourcePath);
                return _current;
            }
        }

        public static UiSkin LoadOrThrow(string resourcePath)
        {
            var skin = Resources.Load<UiSkin>(resourcePath);
            if (skin == null)
                throw new InvalidOperationException(
                    $"[UiSkin] Resources/{resourcePath}.asset 이 없습니다. " +
                    "Create > Dice Orbit > UI > UI Skin 으로 만들어 Assets/Resources/UI/UiSkin.asset 에 두세요.");
            return skin;
        }

        [Header("팔레트")]
        public Color Paper     = new Color(0.980f, 0.953f, 0.878f);        // #FAF3E0 크림 종이
        public Color PaperDeep = new Color(0.910f, 0.859f, 0.765f);        // #E8DBC3 칩
        public Color Ink       = new Color(0.294f, 0.259f, 0.361f);        // #4B425C 잉크
        public Color InkMuted  = new Color(0.42f, 0.40f, 0.36f);           // 설명 회색
        public Color Accent    = new Color(0.79f, 0.61f, 0.25f);           // 골드
        public Color Primary   = new Color(1.00f, 0.65f, 0.95f);           // #FFA6F2 핑크
        public Color Secondary = new Color(0.65f, 0.87f, 1.00f);           // #A6DDFF 하늘
        public Color Danger    = new Color(0.773f, 0.227f, 0.227f);        // #C53A3A HP
        public Color Dice      = new Color(0.16f, 0.42f, 0.65f);           // 주사위 청색
        public Color Modifier  = new Color(0.42f, 0.28f, 0.72f);           // #6A48B8 보라
        public Color Passive   = new Color(0.514f, 0.624f, 0.557f);        // #839F8E 세이지
        public Color Scrim     = new Color(0.043f, 0.051f, 0.078f, 0.85f); // 어두운 반투명 배경

        [Header("9-slice 스프라이트")]
        public Sprite Panel;
        public Sprite Card;
        public Sprite Chip;
        public Sprite Tooltip;
        public Sprite Slot;
        public Sprite Divider;

        [Header("버튼 (상태별 스프라이트)")]
        public ButtonSpriteSet ButtonPrimary = new ButtonSpriteSet();
        public ButtonSpriteSet ButtonSecondary = new ButtonSpriteSet();

        [Header("아이콘")]
        public Sprite Coin;
        public Sprite PotionSlotEmpty;
        public Sprite Close;

        public Sprite GetSprite(SkinPart part) => part switch
        {
            SkinPart.Panel => Panel,
            SkinPart.Card => Card,
            SkinPart.Chip => Chip,
            SkinPart.Tooltip => Tooltip,
            SkinPart.Slot => Slot,
            SkinPart.Divider => Divider,
            SkinPart.ButtonPrimary => ButtonPrimary.Normal,
            SkinPart.ButtonSecondary => ButtonSecondary.Normal,
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, "알 수 없는 SkinPart"),
        };

        public ButtonSpriteSet GetButtonSet(ButtonKind kind)
            => kind == ButtonKind.Primary ? ButtonPrimary : ButtonSecondary;

        public void ApplyPanel(Image image)   => ApplySliced(image, Panel,   nameof(Panel));
        public void ApplyCard(Image image)    => ApplySliced(image, Card,    nameof(Card));
        public void ApplyChip(Image image)    => ApplySliced(image, Chip,    nameof(Chip));
        public void ApplyTooltip(Image image) => ApplySliced(image, Tooltip, nameof(Tooltip));
        public void ApplySlot(Image image)    => ApplySliced(image, Slot,    nameof(Slot));
        public void ApplyDivider(Image image) => ApplySliced(image, Divider, nameof(Divider));

        /// <summary>버튼에 상태별 스프라이트를 꽂는다 (SpriteSwap). targetGraphic이 없으면 같은 오브젝트의 Image를 쓴다.</summary>
        public void ApplyButton(Button button, ButtonKind kind)
        {
            if (button == null) throw new ArgumentNullException(nameof(button));
            var set = GetButtonSet(kind);
            string field = kind == ButtonKind.Primary ? nameof(ButtonPrimary) : nameof(ButtonSecondary);
            Require(set.Normal,   field + ".Normal");
            Require(set.Hover,    field + ".Hover");
            Require(set.Pressed,  field + ".Pressed");
            Require(set.Disabled, field + ".Disabled");

            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image == null)
                throw new InvalidOperationException($"[UiSkin] 버튼 '{button.name}'에 Image가 없어 스프라이트를 꽂을 수 없습니다.");

            ApplySliced(image, set.Normal, field + ".Normal");
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = set.Hover,
                selectedSprite = set.Hover,
                pressedSprite = set.Pressed,
                disabledSprite = set.Disabled,
            };
        }

        private static void ApplySliced(Image image, Sprite sprite, string field)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            Require(sprite, field);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;   // 스프라이트가 색을 가진다 — 틴트 금지
        }

        private static void Require(Sprite sprite, string field)
        {
            if (sprite == null)
                throw new InvalidOperationException(
                    $"[UiSkin] {field} 스프라이트가 비어 있습니다 — 「도구/Dice Orbit/UI 스킨 점검」으로 확인하세요.");
        }
    }
}
