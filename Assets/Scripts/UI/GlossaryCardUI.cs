using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 툴팁 보조 패널의 개별 카드 컴포넌트입니다.
    /// 키워드 카드와 상태이상 카드 모두 이 하나의 프리팹으로 처리합니다.
    ///
    /// ── 프리팹 구조 예시 ─────────────────────────────────────────
    ///   GlossaryCard (이 컴포넌트 + Image 배경 + VerticalLayoutGroup)
    ///    ├── HeaderRow (HorizontalLayoutGroup)
    ///    │    ├── IconImage   (Image)           ← 키워드 아이콘, 없으면 비활성
    ///    │    ├── NameText    (TMP)             ← 키워드/상태이상 이름 (색상 적용)
    ///    │    └── MetaText    (TMP)             ← 스택·지속 정보 (상태이상 전용)
    ///    └── DescText         (TMP)             ← 설명 텍스트
    /// </summary>
    public class GlossaryCardUI : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private Image            iconImage; // 키워드 아이콘 (없으면 비활성)
        [SerializeField] private TextMeshProUGUI  nameText;  // 이름 텍스트 (색상 적용)
        [SerializeField] private TextMeshProUGUI  metaText;  // 스택/지속 정보 (상태이상 전용)
        [SerializeField] private TextMeshProUGUI  descText;  // 설명 텍스트

        // ═══════════════════════════════════════════════════════
        // 공개 API — 카드 타입별 설정
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 이 카드를 키워드 카드로 설정합니다.
        /// 아이콘이 있으면 표시하고, MetaText(스택/지속)는 숨깁니다.
        /// </summary>
        /// <param name="name">키워드 이름 (예: "집중")</param>
        /// <param name="description">키워드 설명 텍스트</param>
        /// <param name="color">이름에 적용할 강조 색상</param>
        /// <param name="icon">아이콘 스프라이트 (null이면 아이콘 비활성)</param>
        public void SetKeyword(string name, string description, Color color, Sprite icon = null)
        {
            // 이름 텍스트 설정 및 색상 적용
            if (nameText != null)
            {
                nameText.text  = name;
                nameText.color = color;
            }

            // 아이콘: 있으면 표시, 없으면 비활성
            if (iconImage != null)
            {
                bool hasIcon = icon != null;
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(hasIcon);
            }

            // 상태이상 전용 MetaText(스택/지속)는 키워드 카드에서 숨깁니다
            if (metaText != null)
                metaText.gameObject.SetActive(false);

            // 설명 텍스트 설정
            if (descText != null)
                descText.text = description;
        }

        /// <summary>
        /// 이 카드를 상태이상 카드로 설정합니다.
        /// 아이콘은 숨기고 MetaText에 스택/지속 정보를 표시합니다.
        /// </summary>
        /// <param name="name">상태이상 표시 이름 (예: "집중")</param>
        /// <param name="stackText">스택 텍스트 (예: "x3"), 없으면 빈 문자열</param>
        /// <param name="durationText">지속 텍스트 (예: "(2T)"), 무한이면 "(∞T)"</param>
        /// <param name="description">상태이상 설명 텍스트</param>
        /// <param name="color">이름에 적용할 강조 색상</param>
        public void SetStatus(
            string name, string stackText, string durationText, string description, Color color)
        {
            // 이름 텍스트 설정 및 색상 적용
            if (nameText != null)
            {
                nameText.text  = name;
                nameText.color = color;
            }

            // 상태이상 카드는 아이콘을 사용하지 않습니다
            if (iconImage != null)
                iconImage.gameObject.SetActive(false);

            // MetaText: 스택 + 지속 정보를 한 줄로 조합합니다 (예: "x3  (2T)")
            if (metaText != null)
            {
                string meta = BuildMetaText(stackText, durationText);
                metaText.text = meta;
                // 내용이 있을 때만 활성화합니다
                metaText.gameObject.SetActive(!string.IsNullOrWhiteSpace(meta));
            }

            // 설명 텍스트 설정
            if (descText != null)
                descText.text = description;
        }

        // ═══════════════════════════════════════════════════════
        // 내부 헬퍼
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 스택 텍스트와 지속 텍스트를 하나의 문자열로 합칩니다.
        /// 둘 다 있으면 "x3  (2T)", 하나만 있으면 해당 값만 반환합니다.
        /// </summary>
        private static string BuildMetaText(string stackText, string durationText)
        {
            bool hasStack    = !string.IsNullOrWhiteSpace(stackText);
            bool hasDuration = !string.IsNullOrWhiteSpace(durationText);

            if (hasStack && hasDuration)  return $"{stackText}  {durationText}";
            if (hasStack)                 return stackText;
            if (hasDuration)              return durationText;
            return string.Empty;
        }
    }
}
