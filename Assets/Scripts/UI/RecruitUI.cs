using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DiceOrbit.Systems.Recruit;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 영입 UI (Start & Wave Recruit)
    /// </summary>
    public class RecruitUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject characterCardPrefab;
        [SerializeField] private Button rerollButton;
        [SerializeField] private TMPro.TextMeshProUGUI rerollCostText;
        [SerializeField] private GameObject panel; // To show/hide
        
        [Header("Prefabs (Spawning)")]
        [SerializeField] private GameObject characterUIPrefab; // CharacterUI 프리팹

        private void Start()
        {
            if (rerollButton != null)
                rerollButton.onClick.AddListener(OnRerollClicked);
            
            // Listen to Manager
            if (RecruitManager.Instance != null)
            {
                RecruitManager.Instance.OnOptionsUpdated += RefreshUI;
            }
        }

        private void OnDestroy()
        {
            if (RecruitManager.Instance != null)
            {
                RecruitManager.Instance.OnOptionsUpdated -= RefreshUI;
            }
        }

        public void Show()
        {
            if(panel != null) panel.SetActive(true);
            
            // Trigger Manager to Generate if needed?
            // Usually GameFlow -> RecruitManager.Generate -> Show UI.
            // But let's ensure we display current options.
            RefreshUI();
            UpdateRerollUI();
        }

        public void Hide()
        {
            if(panel != null) panel.SetActive(false);
        }

        private void RefreshUI()
        {
            if (RecruitManager.Instance == null) return;
            var options = RecruitManager.Instance.CurrentOptions;

            // Clear old
            foreach (Transform child in cardContainer) Destroy(child.gameObject);

            // Create new
            foreach (var preset in options)
            {
                var cardObj = Instantiate(characterCardPrefab, cardContainer);
                var card = cardObj.GetComponent<CharacterCard>();
                if (card != null)
                {
                    card.Setup(preset, OnCharacterSelected);
                }
            }
            
            UpdateRerollUI();
        }
        
        private void UpdateRerollUI()
        {
            if (rerollButton == null) return;
            
            if (RecruitManager.Instance != null)
            {
                int cost = RecruitManager.Instance.RerollCost;
                if(rerollCostText != null) rerollCostText.text = $"Reroll ({cost} G)";
                
                // Disable if not enough gold?
                // var gm = Core.GameManager.Instance;
                // rerollButton.interactable = (gm != null && gm.CurrentGold >= cost);
                // But initially gold might be 0, wait, user logic says "Gold used for reroll".
                // If it's the FIRST recruit, maybe Reroll is free? 
                // Or user starts with some gold? 
                // For now, simple interaction check.
            }
        }

        private void OnRerollClicked()
        {
            if (RecruitManager.Instance != null)
            {
                if (RecruitManager.Instance.RerollOptions())
                {
                    Debug.Log("[RecruitUI] Reroll success");
                }
                else
                {
                    Debug.Log("[RecruitUI] Not enough gold!");
                }
            }
        }

        private void OnCharacterSelected(CharacterPreset preset)
        {
            Debug.Log($"[RecruitUI] Selected: {preset.CharacterName}");
            
            // 1. Spawn Character
            CreatePlayerCharacter(preset);
            
            // 2. Notify GameFlow
            // If RecruitManager handles logic, better call RecruitManager.RecruitCharacter(preset)?
            // RecruitManager.Instance.RecruitCharacter(preset); 
            // NOTE: Our RecruitManager logic was placeholder/empty for spawning.
            // So we KEEP the spawning logic here for now, OR move it to RecruitManager.
            // I will MOVE spawning logic to RecruitManager later or keep it here if RecruitManager can't access prefabs easily.
            // Actually, best to keep Spawning in GameFlow or dedicated Spawner.
            // For now, I will keep CreatePlayerCharacter method here to ensure it works, 
            // but I should also notify RecruitManager that we picked someone so it can clear options or whatever.
            
            if (RecruitManager.Instance != null)
                RecruitManager.Instance.RecruitCharacter(preset); // Logic update: just notifies choice

            Core.GameFlowManager.Instance.OnRecruitComplete();
            Hide();
        }

        // --- Spawning Logic (Moved/Preserved from CharacterSelectionUI) ---
        private void CreatePlayerCharacter(CharacterPreset preset)
        {
            if (preset == null) return;
            
            // Note: This logic is identical to previous CharacterSelectionUI
            // ... (Duplicate logic for spawning)
            // Ideally should be helper: CharacterSpawner.Spawn(preset);
            
            // Reuse logic from previous file...
             var orbitManager = FindObjectOfType<Core.OrbitManager>(); // FindAnyObjectByType used in Unity 2023+
             if(orbitManager == null) return;
             
             var startTile = orbitManager.GetTile(0);
             var characterObj = new GameObject($"Player_{preset.CharacterName}");
             characterObj.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
             
             var sr = characterObj.AddComponent<SpriteRenderer>();
             if(preset.CharacterSprite != null) sr.sprite = preset.CharacterSprite;
             sr.color = preset.SpriteColor;
             
             var character = characterObj.AddComponent<Core.Character>();
             characterObj.AddComponent<BoxCollider>().size = new Vector3(1,1,0.1f);
             
             var stats = preset.CreateStats();
             character.InitializeStats(stats);
             
             characterObj.transform.position = startTile.Position + new Vector3(0, 1.5f, 1.0f);
             
             if(Core.PartyManager.Instance != null) 
                 Core.PartyManager.Instance.AddCharacter(character);
                 
             // Create UI
             if(characterUIPrefab != null)
             {
                 var uiObj = Instantiate(characterUIPrefab, characterObj.transform);
                 uiObj.transform.localPosition = new Vector3(0, 1.2f, 0);
                 var ui = uiObj.GetComponent<CharacterUI>();
                 if(ui != null) ui.SetCharacter(character);
             }
        }
    }
}
