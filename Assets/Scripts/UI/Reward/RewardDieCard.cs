using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Data;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 주사위 박자의 주사위 카드 뷰 — 이름(등급 색)·등급·6면·사용 효과·설명을 호버 없이 전부 보여 준다.
    /// "새 주사위" 큰 카드(씬에 고정)와 "내 덱" 작은 카드(템플릿 복제)가 같은 컴포넌트를 쓴다.
    /// 그리기만 한다: 교체 대상 선택은 RewardUI.
    /// </summary>
    public class RewardDieCard : MonoBehaviour
    {
        private const string NoEffectText = "사용 효과 없음";

        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI rarityText;
        [Tooltip("주사위 면 숫자 라벨 6칸 — 각 라벨의 부모가 면 그림(칸)")]
        [SerializeField] private TextMeshProUGUI[] faceLabels;
        [SerializeField] private TextMeshProUGUI effectText;
        [Tooltip("보조 설명 (새 주사위 카드에만 있다 — 덱 카드는 비워 둔다)")]
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private CanvasGroup group;

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>보상으로 나온 새 주사위 (정의 에셋 그대로).</summary>
        public void BindDefinition(DieDefinitionSO die)
        {
            if (die == null) throw new ArgumentNullException(nameof(die));
            Fill(die.Name, die.RarityLabel, die.RarityColor, die.Faces, die.Effect, die.Description);
            button.onClick.RemoveAllListeners();
        }

        /// <summary>덱의 한 칸 (면 변형·부여 효과 반영). 클릭 = 교체 대상 선택.</summary>
        public void BindInstance(DieInstance instance, Action onClick)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            var baseDie = instance.BaseDie;
            if (baseDie == null) throw new InvalidOperationException("[RewardDieCard] 덱 주사위에 BaseDie가 없다.");
            Fill(baseDie.Name, baseDie.RarityLabel, baseDie.RarityColor, instance.Faces, instance.Effect, null);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            UiSkin.Current.ApplyButtonSelected(button, SkinButton.RewardChoiceCard, selected);
            Rect.localScale = Vector3.one * (selected ? 1.04f : 1f);
        }

        public void SetDimmed(bool dimmed) => group.alpha = dimmed ? 0.6f : 1f;

        private void Fill(string dieName, string rarityLabel, Color rarityColor, int[] faces, DieEffect effect, string description)
        {
            nameText.text = dieName;
            nameText.color = rarityColor;
            rarityText.text = rarityLabel;
            rarityText.color = rarityColor;

            int faceCount = faces != null ? faces.Length : 0;
            for (int i = 0; i < faceLabels.Length; i++)
            {
                bool has = i < faceCount;
                faceLabels[i].transform.parent.gameObject.SetActive(has);
                if (has) faceLabels[i].text = faces[i].ToString();
            }

            string preview = effect != null ? effect.Preview() : null;
            effectText.text = string.IsNullOrEmpty(preview) ? NoEffectText : preview;
            effectText.color = string.IsNullOrEmpty(preview) ? UiSkin.Current.InkMuted : UiSkin.Current.Ink;

            if (descriptionText != null) descriptionText.text = description ?? string.Empty;
        }
    }
}
