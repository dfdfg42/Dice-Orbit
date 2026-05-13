using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data;
using System;

namespace DiceOrbit.UI
{
    public class SkillCardUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private TextMeshProUGUI typeText; // Active/Passive
        [SerializeField] private TextMeshProUGUI levelText; // "New!" or "Lv.1 -> Lv.2"
        [SerializeField] private Button button;

        private CharacterActiveSkill mySkill;
        private Action<CharacterActiveSkill> onClickCallback;

        public void Setup(CharacterActiveSkill skill, bool isNew, int currentLevel, Action<CharacterActiveSkill> callback)
        {
            mySkill = skill;
            onClickCallback = callback;
            
            // Visuals
            if (iconImage != null) iconImage.sprite = skill.icon;
            if (nameText != null) nameText.text = skill.SkillName;
            
            // Description logic
            string desc = skill.Description ?? "No Description";
            var requirement = skill.requirement;
            if (requirement != null)
            {
                desc += $"\n<color=#9EE6FF>{requirement.GetDescription()}</color>";
            }

            if (descText != null) descText.text = desc;

            if (typeText != null) typeText.text = "Active";

            if (levelText != null)
            {
                if (isNew) levelText.text = "<color=yellow>New!</color>";
                else levelText.text = $"Lv.{currentLevel} -> Lv.{currentLevel + 1}";
            }

            // Click
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            onClickCallback?.Invoke(mySkill);

        }
    }
}
