using UnityEngine;
using System.Collections.Generic;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 웨이브 관리자 (1~8 Wave)
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        [Header("Settings")]
        public int MaxWave = 8;
        
        [Header("Runtime")]
        public int CurrentWave { get; private set; } = 1;
        public bool IsWaveActive { get; private set; } = false;

        public event System.Action<int> OnWaveStart;
        public event System.Action<int> OnWaveClear;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void StartFirstWave()
        {
            CurrentWave = 1;
            StartWave(CurrentWave);
        }

        public void StartNextWave()
        {
            if (CurrentWave < MaxWave)
            {
                CurrentWave++;
                StartWave(CurrentWave);
            }
            else
            {
                Debug.Log("[WaveManager] Game Completed!");
                GameFlowManager.Instance.OnCombatVictory(); // Trigger Victory State?
            }
        }

        private void StartWave(int wave)
        {
            IsWaveActive = true;
            Debug.Log($"[WaveManager] Wave {wave} Started!");
            
            // Spawn Monsters (Logic placeholder)
            SpawnMonsters(wave);

            OnWaveStart?.Invoke(wave);
        }

        private void SpawnMonsters(int wave)
        {
            // Refactor 2.0: Spawning Logic
            Debug.Log($"[WaveManager] Spawning monsters for Wave {wave}...");
            // TODO: Implement Monster Spawning based on Wave Difficulty
            // For now, assume monsters are placed or CombatManager handles it.
            
            // Temporary Logic for Prototype:
            // Just notify CombatManager or let CombatManager prompt start.
        }

        public void CheckWaveClear()
        {
            // Called by CombatManager when monsters are cleared
            if (IsWaveActive) 
            {
                // Logic to check if all monsters are dead?
                // Let's assume CombatManager calls this WHEN cleared.
                EndWave();
            }
        }

        private void EndWave()
        {
            IsWaveActive = false;
            Debug.Log($"[WaveManager] Wave {CurrentWave} Cleared!");
            
            OnWaveClear?.Invoke(CurrentWave);
            
            // Trigger Post-Wave Flow (Reward -> Recruit -> Next)
            GameFlowManager.Instance.OnWaveCleared(CurrentWave);
        }
    }
}
