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
            Debug.Log($"[WaveManager] Spawning all monsters for Wave {wave}...");

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

            // SpawnCount는 레거시로 유지 (현재는 사용하지 않음)
            int spawnCount = Mathf.Max(1, waveDef.SpawnCount);
            Debug.Log($"[WaveManager] SpawnCount={spawnCount} (Legacy, ignored)");

            var validPresets = waveDef.MonsterPresets.Where(p => p != null).ToList();
            if (validPresets.Count == 0)
            {
                Debug.LogWarning("[WaveManager] No valid monster presets found in wave definition.");
                return;
            }

            var spawnPoints = GetSpawnPoints().OrderBy(_ => Random.value).ToList();

            // 모든 MonsterPresets를 한 번에 스폰
            for (int i = 0; i < validPresets.Count; i++)
            {
                var preset = validPresets[i];
                if (preset == null) continue;

                Vector3 spawnPos = GetSpawnPosition(spawnPoints, i);
                Quaternion rot = Quaternion.identity;

                var go = Object.Instantiate(monsterPrefab, spawnPos, rot, spawnRoot);
                var monster = go.GetComponent<Monster>() ?? go.GetComponentInChildren<Monster>();

                if (monster != null)
                {
                    monster.InitializeFromPreset(preset);
                    spawnedMonsters.Add(monster);

                    // 몬스터 사망 이벤트 구독
                    monster.OnDeath += OnMonsterDeath;

                    if (combatManager != null)
                    {
                        combatManager.RegisterMonster(monster);
                    }

                    Debug.Log($"[WaveManager] Spawned monster {i + 1}/{validPresets.Count}: {preset.name}");
                }
                else
                {
                    Debug.LogWarning($"[WaveManager] Spawned object '{go.name}' has no Monster component.");
                }
            }

            Debug.Log($"[WaveManager] Total {spawnedMonsters.Count} monsters spawned for Wave {wave}.");
        }

        /// <summary>
        /// 몬스터 사망 시 호출되는 이벤트 핸들러
        /// </summary>
        private void OnMonsterDeath(Monster deadMonster)
        {
            if (deadMonster != null)
            {
                // 이벤트 구독 해제
                deadMonster.OnDeath -= OnMonsterDeath;

                // 리스트에서 제거
                spawnedMonsters.Remove(deadMonster);
            }

            Debug.Log($"[WaveManager] Monster died. Remaining monsters: {spawnedMonsters.Count}");

            // 모든 몬스터가 죽었으면 Wave 클리어
            if (spawnedMonsters.Count == 0)
            {
                Debug.Log($"[WaveManager] All monsters defeated! Wave {CurrentWave} cleared.");
                CheckWaveClear();
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

            OnWaveClear?.Invoke(CurrentWave);

            // 다음 웨이브 자동 시작 (2초 딜레이)
            if (CurrentWave < MaxWave)
            {
                Debug.Log($"[WaveManager] Starting next wave in 2 seconds...");
                Invoke(nameof(StartNextWave), 2f);
            }
            else
            {
                Debug.Log("[WaveManager] All waves completed! Game finished!");
            }
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
                    // 이벤트 구독 해제
                    monster.OnDeath -= OnMonsterDeath;
                    Destroy(monster.gameObject);
                }
            }
            spawnedMonsters.Clear();
        }
    }
}
