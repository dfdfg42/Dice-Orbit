using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data.Skills;
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
            if (isNew)
            {
                var lvl1 = skill.GetLevelData(1);
                desc = lvl1 != null ? lvl1.Description : "No Description";
            }
            else
            {
                var nextLv = skill.GetLevelData(currentLevel + 1);
                desc = nextLv != null ? nextLv.Description : "Max Level Reached!";
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
