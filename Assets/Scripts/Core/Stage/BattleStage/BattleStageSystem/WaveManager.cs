using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data.Waves;
using DiceOrbit.Data.Monsters;
using System.Linq;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 웨이브 관리자 (1~8 Wave)
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        [Header("Settings")]
        public int MaxWave = 4;
        [SerializeField] private WaveDatabase waveDatabase;
        [SerializeField] private Transform spawnRoot;
        [SerializeField] private float fallbackSpawnRadius = 2.5f;
        [SerializeField] private GameObject monsterPrefab;
        
        [Header("Runtime")]
        public int CurrentWave { get; private set; } = 0;
        public bool IsWaveActive { get; private set; } = false;

        private readonly List<Monster> spawnedMonsters = new List<Monster>();

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
            }
        }

        private void StartWave(int wave)
        {
            IsWaveActive = true;
            Debug.Log($"[WaveManager] Wave {wave} Started!");
            ResolveMaxWave();

            // Spawn Monsters
            SpawnMonsters(wave);

            OnWaveStart?.Invoke(wave);
        }

        private void SpawnMonsters(int wave)
        {
            Debug.Log($"[WaveManager] Spawning monsters for Wave {wave}...");

            CleanupSpawnedMonsters();

            var combatManager = CombatManager.Instance;
            if (combatManager != null)
            {
                combatManager.ClearMonsters();
            }

            var waveDef = GetWaveDefinition(wave);
            if (waveDef == null || waveDef.MonsterPresets == null || waveDef.MonsterPresets.Count == 0)
            {
                Debug.LogWarning("[WaveManager] No wave definition or monster presets found. Skipping spawn.");
                return;
            }

            if (monsterPrefab == null)
            {
                Debug.LogWarning("[WaveManager] monsterPrefab is not assigned. Skipping spawn.");
                return;
            }

            int spawnCount = Mathf.Max(1, waveDef.SpawnCount);
            var spawnPoints = GetSpawnPoints().OrderBy(_ => Random.value).ToList();

            var validPresets = waveDef.MonsterPresets.Where(p => p != null).ToList();
            if (validPresets.Count == 0)
            {
                Debug.LogWarning("[WaveManager] No valid monster presets found in wave definition.");
                return;
            }

            var spawnPlan = new List<MonsterPreset>();
            if (spawnCount <= validPresets.Count)
            {
                spawnPlan.AddRange(validPresets.OrderBy(_ => Random.value).Take(spawnCount));
            }
            else
            {
                spawnPlan.AddRange(validPresets.OrderBy(_ => Random.value));
                while (spawnPlan.Count < spawnCount)
                {
                    spawnPlan.Add(validPresets[Random.Range(0, validPresets.Count)]);
                }
            }

            for (int i = 0; i < spawnPlan.Count; i++)
            {
                MonsterPreset preset = spawnPlan[i];
                if (preset == null) continue;

                Vector3 spawnPos = GetSpawnPosition(spawnPoints, i);
                Quaternion rot = Quaternion.identity;

                var go = Object.Instantiate(monsterPrefab, spawnPos, rot, spawnRoot);
                var monster = go.GetComponent<Monster>() ?? go.GetComponentInChildren<Monster>();
                if (monster != null)
                {
                    monster.InitializeFromPreset(preset);
                    spawnedMonsters.Add(monster);
                    if (combatManager != null)
                    {
                        combatManager.RegisterMonster(monster);
                    }
                }
                else
                {
                    Debug.LogWarning($"[WaveManager] Spawned object '{go.name}' has no Monster component.");
                }
            }
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

            // Prepare reward for this wave
            // Prepare reward for this wave
            /*
            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.PrepareReward(GetWaveDefinition(CurrentWave));
            }
            */
            
            OnWaveClear?.Invoke(CurrentWave);
        }

        private void ResolveMaxWave()
        {
            if (waveDatabase != null && waveDatabase.Waves != null && waveDatabase.Waves.Count > 0)
            {
                MaxWave = waveDatabase.Waves.Count;
            }
        }

        public WaveDefinition GetWaveDefinition(int wave)
        {
            if (waveDatabase == null || waveDatabase.Waves == null || waveDatabase.Waves.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(wave - 1, 0, waveDatabase.Waves.Count - 1);
            return waveDatabase.Waves[index];
        }

        private List<WaveSpawnPoint> GetSpawnPoints()
        {
            var points = new List<WaveSpawnPoint>(Object.FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None));
            return points;
        }

        private Vector3 GetSpawnPosition(List<WaveSpawnPoint> points, int index)
        {
            if (points != null && points.Count > 0)
            {
                // Use each spawn point once before reusing.
                if (index < points.Count)
                {
                    return points[index].transform.position;
                }

                // If spawn count exceeds point count, place extras around reused points with an offset.
                var basePoint = points[index % points.Count].transform.position;
                int overlapTier = index / points.Count;
                float angle = overlapTier * 137.5f * Mathf.Deg2Rad;
                float distance = 0.8f + overlapTier * 0.6f;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                return basePoint + offset;
            }

            // fallback: spread around center
            float fallbackAngle = index * 137.5f * Mathf.Deg2Rad;
            float fallbackDistance = Mathf.Min(fallbackSpawnRadius, 0.8f + index * 0.6f);
            Vector2 circle = new Vector2(Mathf.Cos(fallbackAngle), Mathf.Sin(fallbackAngle)) * fallbackDistance;
            return new Vector3(circle.x, 0f, circle.y);
        }

        private void CleanupSpawnedMonsters()
        {
            foreach (var monster in spawnedMonsters)
            {
                if (monster != null)
                {
                    Destroy(monster.gameObject);
                }
            }
            spawnedMonsters.Clear();
        }
    }
}
