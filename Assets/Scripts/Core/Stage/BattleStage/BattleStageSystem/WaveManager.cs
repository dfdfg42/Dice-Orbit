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
        private List<Data.Monsters.MonsterPreset> currentSpawnPlan = new List<Data.Monsters.MonsterPreset>();
        private int currentSpawnIndex = 0;

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
            Debug.Log($"[WaveManager] Preparing sequential spawn for Wave {wave}...");

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
            Debug.Log($"[WaveManager] SpawnCount={spawnCount} (Legacy, not used for sequential spawn)");

            var validPresets = waveDef.MonsterPresets.Where(p => p != null).ToList();
            if (validPresets.Count == 0)
            {
                Debug.LogWarning("[WaveManager] No valid monster presets found in wave definition.");
                return;
            }

            // 스폰 계획 생성 (모든 프리셋을 순서대로)
            currentSpawnPlan = new List<Data.Monsters.MonsterPreset>(validPresets);
            currentSpawnIndex = 0;

            // 첫 번째 몬스터만 스폰
            SpawnNextMonster();
        }

        /// <summary>
        /// 다음 몬스터를 스폰합니다 (순차 스폰)
        /// </summary>
        private void SpawnNextMonster()
        {
            if (currentSpawnPlan == null || currentSpawnPlan.Count == 0)
            {
                Debug.LogWarning("[WaveManager] No spawn plan available.");
                return;
            }

            if (currentSpawnIndex >= currentSpawnPlan.Count)
            {
                Debug.Log("[WaveManager] All monsters in spawn plan have been spawned.");
                return;
            }

            var preset = currentSpawnPlan[currentSpawnIndex];
            if (preset == null)
            {
                Debug.LogWarning($"[WaveManager] Preset at index {currentSpawnIndex} is null. Skipping.");
                currentSpawnIndex++;
                SpawnNextMonster(); // 다음 몬스터 시도
                return;
            }

            var spawnPoints = GetSpawnPoints().OrderBy(_ => Random.value).ToList();
            Vector3 spawnPos = GetSpawnPosition(spawnPoints, currentSpawnIndex);
            Quaternion rot = Quaternion.identity;

            var go = Object.Instantiate(monsterPrefab, spawnPos, rot, spawnRoot);
            var monster = go.GetComponent<Monster>() ?? go.GetComponentInChildren<Monster>();

            if (monster != null)
            {
                monster.InitializeFromPreset(preset);
                spawnedMonsters.Add(monster);

                // 몬스터 사망 이벤트 구독
                monster.OnDeath += OnMonsterDeath;

                var combatManager = CombatManager.Instance;
                if (combatManager != null)
                {
                    combatManager.RegisterMonster(monster);
                }

                Debug.Log($"[WaveManager] Spawned monster {currentSpawnIndex + 1}/{currentSpawnPlan.Count}: {preset.name}");
                currentSpawnIndex++;
            }
            else
            {
                Debug.LogWarning($"[WaveManager] Spawned object '{go.name}' has no Monster component.");
                currentSpawnIndex++;
                SpawnNextMonster(); // 다음 몬스터 시도
            }
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

            Debug.Log($"[WaveManager] Monster died. Remaining: {spawnedMonsters.Count}, Next spawn index: {currentSpawnIndex}/{currentSpawnPlan.Count}");

            // 다음 몬스터 스폰
            if (currentSpawnIndex < currentSpawnPlan.Count)
            {
                SpawnNextMonster();
            }
            else
            {
                // 모든 몬스터가 스폰되었고, 남은 몬스터도 없으면 웨이브 클리어
                if (spawnedMonsters.Count == 0)
                {
                    CheckWaveClear();
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
                    // 이벤트 구독 해제
                    monster.OnDeath -= OnMonsterDeath;
                    Destroy(monster.gameObject);
                }
            }
            spawnedMonsters.Clear();
            currentSpawnPlan.Clear();
            currentSpawnIndex = 0;
        }
    }
}
