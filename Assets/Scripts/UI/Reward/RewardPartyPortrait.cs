using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 강화 박자의 파티 초상 뷰 — 초상·이름·중첩 점. 씬 템플릿에 붙어 복제된다.
    /// 그리기만 한다: 선택·장착 가능 여부는 RewardUI가 정한다.
    /// </summary>
    public class RewardPartyPortrait : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image face;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI pipsText;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RewardHoverInfo hover;

        public Character Character { get; private set; }

        public void Bind(Character character, Action onClick)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            Character = character;

            nameText.text = DisplayName(character);
            var portrait = character.SourcePreset != null ? character.SourcePreset.Portrait : null;
            face.sprite = portrait;
            face.enabled = portrait != null;   // 초상 없는 프리셋은 빈 액자로 드러난다

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());

            SetSelected(false);
            SetEligible(true);
        }

        public void SetSelected(bool selected)
            => UiSkin.Current.ApplyButtonSelected(button, SkinButton.RewardPortrait, selected);

        /// <summary>고른 카드를 이 캐릭터가 받을 수 없으면 흐려지고 눌리지 않는다.</summary>
        public void SetEligible(bool eligible)
        {
            button.interactable = eligible;
            group.alpha = eligible ? 1f : 0.45f;
        }

        /// <summary>이름 옆 보조 표시 — 고른 카드의 중첩 점(●○○) 또는 장착 수.</summary>
        public void SetPips(string text) => pipsText.text = text;

        /// <summary>호버 툴팁 — 이 캐릭터가 장착한 모디파이어 목록.</summary>
        public void SetTooltip(string text) => hover.Description = text;

        public static string DisplayName(Character character)
            => character.Stats != null ? character.Stats.CharacterName : character.name;
    }
}
