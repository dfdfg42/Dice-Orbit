using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;
using TMPro;

namespace DiceOrbit.UI
{
    public class LevelUpUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform cardContainer; // Parent of card buttons
        [SerializeField] private GameObject cardPrefab;   // Prefab for a single skill card

        [Header("Runtime")]
        private Data.CharacterStats targetCharacter;
        private List<Core.SkillDraftManager.DraftOption> currentOptions;

        public void Show(Data.CharacterStats character)
        {
            targetCharacter = character;
            panel.SetActive(true);
            
            // 1. Get Options
            currentOptions = Core.SkillDraftManager.Instance.DraftSkills(character);
            
            // 2. Clear old cards
            foreach(Transform t in cardContainer) Destroy(t.gameObject);
            
            // 3. Spawn Cards
            foreach(var option in currentOptions)
            {
                var card = Instantiate(cardPrefab, cardContainer).GetComponent<SkillCardUI>();
                if(card != null)
                {
                    card.Setup(option, OnCardSelected);
                }
            }
            
            // Pause Game
            Time.timeScale = 0;
        }

        private void OnCardSelected(Core.SkillDraftManager.DraftOption option)
        {
            ApplySkill(option);
            Close();
        }

        private void ApplySkill(Core.SkillDraftManager.DraftOption option)
        {
            var skill = option.Skill;
            
            // 1. Check if we have it
            RuntimeSkill runtimeSkill = null;
            if(skill.Type == CharacterSkillType.Active)
            {
                runtimeSkill = targetCharacter.RuntimeActiveSkills.Find(s => s.BaseSkill == skill);
                if(runtimeSkill == null)
                {
                    // Add new
                    targetCharacter.RuntimeActiveSkills.Add(new RuntimeSkill(skill));
                    Debug.Log($"Learned new Active Skill: {skill.SkillName}");
                }
                else
                {
                    // Upgrade
                    runtimeSkill.Upgrade();
                    Debug.Log($"Upgraded Active Skill: {skill.SkillName} to Lv.{runtimeSkill.CurrentLevel}");
                }
            }
            else // Passive
            {
                runtimeSkill = targetCharacter.RuntimePassiveSkills.Find(s => s.BaseSkill == skill);
                if(runtimeSkill == null)
                {
                    // Add new
                    targetCharacter.RuntimePassiveSkills.Add(new RuntimeSkill(skill));
                    Debug.Log($"Learned new Passive Skill: {skill.SkillName}");
                }
                else
                {
                    // Upgrade
                    runtimeSkill.Upgrade();
                    Debug.Log($"Upgraded Passive Skill: {skill.SkillName} to Lv.{runtimeSkill.CurrentLevel}");
                }
            }
        }

        private void Close()
        {
            panel.SetActive(false);
            Time.timeScale = 1;
            
            // Resume Game Flow
            if(Core.GameFlowManager.Instance != null)
            {
                Core.GameFlowManager.Instance.OnLevelUpComplete();
            }
        }
    }
}
