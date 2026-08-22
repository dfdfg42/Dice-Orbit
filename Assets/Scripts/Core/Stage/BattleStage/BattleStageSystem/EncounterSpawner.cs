using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core.Run;
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
        [Tooltip("몬스터를 구역 중심 방향 이 거리에 배치한다. 클수록 중앙에서 멀어져 궤도에 가까워진다(궤도 반지름 8).")]
        [SerializeField] private float monsterZoneRadius = 5.5f;

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

        /// <summary>몹 세트를 스폰해 목록으로 반환. 등록/전멸 감지는 호출자(CombatManager) 몫.
        /// startHidden=true면 스케일 0으로 숨겨 스폰 (전투 시작 연출이 순차로 드러냄).</summary>
        public List<Monster> Spawn(EncounterDefinition encounter, bool startHidden = false)
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

            var zones = Zones.CombatZoneManager.EnsureInstance();
            zones.ClearRegistrations();

            if (presets.Count > zones.ZoneCount)
                Debug.LogError($"[EncounterSpawner] 몹 {presets.Count}마리는 구역 {zones.ZoneCount}개를 넘는다 — 구역당 1마리 규칙 위반. 몹 세트를 {zones.ZoneCount}마리 이하로 구성할 것.");

            for (int i = 0; i < presets.Count; i++)
            {
                int zone = i % zones.ZoneCount;
                var spawnPos = zones.GetZoneCenterPosition(zone, monsterZoneRadius);
                var go = Object.Instantiate(monsterPrefab, spawnPos, Quaternion.identity, spawnRoot);
                var monster = go.GetComponent<Monster>() ?? go.GetComponentInChildren<Monster>();
                if (monster == null)
                {
                    Debug.LogWarning($"[EncounterSpawner] '{go.name}'에 Monster 컴포넌트가 없습니다.");
                    continue;
                }
                monster.InitializeFromPreset(presets[i]);
                zones.RegisterMonster(monster, zone);
                if (startHidden)
                {
                    monster.IntroBaseScale = monster.transform.localScale;   // 원래 스케일 보존 (프리셋별로 다름)
                    monster.transform.localScale = Vector3.zero;             // 연출이 순차로 드러냄
                }
                spawned.Add(monster);
            }

            Debug.Log($"[EncounterSpawner] {spawned.Count}마리 스폰 완료.");

            Visuals.MonsterIdentityManager.EnsureInstance();
            Visuals.MonsterIdentityManager.Instance.Setup(spawned);
            Visuals.ZoneFloorRenderer.EnsureInstance();
            return spawned;
        }

    }
}
