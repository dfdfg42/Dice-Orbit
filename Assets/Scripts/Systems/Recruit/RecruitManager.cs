using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data;

namespace DiceOrbit.Systems.Recruit
{
    /// <summary>
    /// 영입 관리자 (Recruit Manager)
    /// </summary>
    public class RecruitManager : MonoBehaviour
    {
        public static RecruitManager Instance { get; private set; }

        [Header("Settings")]
        public List<CharacterPreset> CharacterPool; // All available characters
        public int RecruitOptionsCount = 4;
        public int RerollCost = 100;

        [Header("Runtime")]
        public List<CharacterPreset> CurrentOptions = new List<CharacterPreset>();

        public event System.Action OnOptionsUpdated;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void GenerateOptions()
        {
            if (CharacterPool == null || CharacterPool.Count == 0)
            {
                Debug.LogWarning("[RecruitManager] Character Pool is empty!");
                return;
            }

            // Randomly select 4 (Unique?)
            // If pool is small, might have duplicates or fill with what we have.
            // Ideally Shuffle and Take 4.
            CurrentOptions = CharacterPool.OrderBy(x => Random.value).Take(RecruitOptionsCount).ToList();
            
            OnOptionsUpdated?.Invoke();
            Debug.Log($"[RecruitManager] Generated {CurrentOptions.Count} recruit options.");
        }

        public bool RerollOptions()
        {
            var gm = Core.GameManager.Instance;
            if (gm != null && gm.SpendGold(RerollCost))
            {
                GenerateOptions();
                return true;
            }
            return false;
        }

        public void RecruitCharacter(CharacterPreset preset)
        {
            var party = Core.PartyManager.Instance;
            if (party == null) return;

            if (party.IsPartyFull)
            {
                Debug.Log("[RecruitManager] Party is full! Swap logic needed (not implemented yet).");
                // TODO: Swap UI Logic
            }
            else
            {
                // Instantiate Logic needed (Similar to ShopManager placeholder)
                // For now, we assume we have a helper to Spawn Character.
                // In CharacterSelectionUI, we had `CreatePlayerCharacter`.
                // We should move `CreatePlayerCharacter` to a `CharacterFactory` or `GameFlowManager` helper.
                Debug.Log($"[RecruitManager] Recruited {preset.CharacterName}");
                
                // Hack: Call GameFlow to spawn? 
                // Or just Log for now as requested "Design".
                // We will implement spawning later properly.
            }
        }
    }
}
