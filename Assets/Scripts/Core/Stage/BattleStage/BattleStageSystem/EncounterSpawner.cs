using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core.Run;
using DiceOrbit.Data.Waves;
using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 몹 세트 스포너 — 인스턴스화 + 프리셋 초기화 + 위치 배치 + 정체성 색.
    /// 장부/승패 판정/이벤트 없음 (전투 상태 권위는 CombatManager, 스펙 2026-07-21).
    /// </summary>
    public class EncounterSpawner : MonoBehaviour
    {
        public static EncounterSpawner Instance { get; private set; }

        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private Transform spawnRoot;
        [SerializeField] private float fallbackSpawnRadius = 2.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static EncounterSpawner EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<EncounterSpawner>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("EncounterSpawner").AddComponent<EncounterSpawner>();
        }

        /// <summary>몹 세트를 스폰해 목록으로 반환. 등록/전멸 감지는 호출자(CombatManager) 몫.</summary>
        public List<Monster> Spawn(EncounterDefinition encounter)
        {
            var spawned = new List<Monster>();

            var presets = encounter?.MonsterPresets?.Where(p => p != null).ToList();
            if (presets == null || presets.Count == 0)
            {
                Debug.LogWarning("[EncounterSpawner] 몹 세트가 비어 있습니다 — 스폰 생략.");
                return spawned;
            }
            if (monsterPrefab == null)
            {
                Debug.LogWarning("[EncounterSpawner] monsterPrefab 미지정 — 스폰 생략.");
                return spawned;
            }

            var points = Object.FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None)
                .OrderBy(_ => Random.value).ToList();

            for (int i = 0; i < presets.Count; i++)
            {
                var go = Object.Instantiate(monsterPrefab, GetSpawnPosition(points, i), Quaternion.identity, spawnRoot);
                var monster = go.GetComponent<Monster>() ?? go.GetComponentInChildren<Monster>();
                if (monster == null)
                {
                    Debug.LogWarning($"[EncounterSpawner] '{go.name}'에 Monster 컴포넌트가 없습니다.");
                    continue;
                }
                monster.InitializeFromPreset(presets[i]);
                spawned.Add(monster);
            }

            Debug.Log($"[EncounterSpawner] {spawned.Count}마리 스폰 완료.");

            Visuals.MonsterIdentityManager.EnsureInstance();
            Visuals.MonsterIdentityManager.Instance.Setup(spawned);
            return spawned;
        }

        private Vector3 GetSpawnPosition(List<WaveSpawnPoint> points, int index)
        {
            if (points != null && points.Count > 0)
            {
                if (index < points.Count) return points[index].transform.position;

                var basePoint = points[index % points.Count].transform.position;
                int overlapTier = index / points.Count;
                float angle = overlapTier * 137.5f * Mathf.Deg2Rad;
                float distance = 0.8f + overlapTier * 0.6f;
                return basePoint + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            }

            float fallbackAngle = index * 137.5f * Mathf.Deg2Rad;
            float fallbackDistance = Mathf.Min(fallbackSpawnRadius, 0.8f + index * 0.6f);
            return new Vector3(Mathf.Cos(fallbackAngle) * fallbackDistance, 0f, Mathf.Sin(fallbackAngle) * fallbackDistance);
        }
    }
}
