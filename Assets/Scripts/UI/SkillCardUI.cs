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

        private CharacterSkill mySkill;
        private Action<CharacterSkill> onClickCallback;

        public void Setup(CharacterSkill skill, bool isNew, int currentLevel, Action<CharacterSkill> callback)
        {
            mySkill = skill;
            onClickCallback = callback;
            
            // Visuals
            if (iconImage != null) iconImage.sprite = skill.Icon;
            if (nameText != null) nameText.text = skill.SkillName;
            
            // Description logic
            string desc = "";
            DiceRequirement requirement = null;
            if (isNew)
            {
                desc = skill.GetDescription(1) ?? "No Description";
                requirement = skill.GetRequirement(1);
            }
            else
            {
                desc = skill.GetDescription(currentLevel + 1) ?? "Max Level Reached!";
                requirement = skill.GetRequirement(currentLevel + 1);
            }

            if (requirement != null)
            {
                desc += $"\n<color=#9EE6FF>{requirement.GetDescription()}</color>";
            }

            if (descText != null) descText.text = desc;

            if (typeText != null) typeText.text = skill.Type.ToString();

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
