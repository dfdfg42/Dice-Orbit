using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DiceOrbit.Core.Run;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Core.Tutorial
{
    /// <summary>
    /// 튜토리얼 고정 데모 셋업. 실제 런 상태(RunManager/세이브/영구덱)를 건드리지 않고
    /// 데모 파티(전사·도적 인접)·약체 몬스터만 스폰하고, 끝나면 ClearAll로 정리한다.
    /// </summary>
    public class TutorialScenario : MonoBehaviour
    {
        public static TutorialScenario Instance { get; private set; }

        [Header("데모 데이터 (Resources/TutorialScenario 프리팹에 배선)")]
        [SerializeField] private CharacterPreset warriorPreset;
        [SerializeField] private CharacterPreset roguePreset;
        [SerializeField] private MonsterPreset demoMonster; // 약체 타일공격 몬스터(GreenSlime)

        public Character Warrior { get; private set; }
        public Character Rogue { get; private set; }
        public Monster DemoMonster { get; private set; }

        public static TutorialScenario EnsureInstance()
        {
            if (Instance != null) return Instance;
            Instance = FindAnyObjectByType<TutorialScenario>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                var prefab = Resources.Load<TutorialScenario>("TutorialScenario");
                Instance = prefab != null ? Instantiate(prefab)
                                          : new GameObject("TutorialScenario").AddComponent<TutorialScenario>();
            }
            return Instance;
        }

        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>데모 파티(전사·도적 인접) 스폰 후 약체 몬스터로 전투 개시.</summary>
        public void SpawnDemo()
        {
            var spawner = Object.FindAnyObjectByType<CharacterSpawner>();
            int tileCount = Object.FindAnyObjectByType<OrbitManager>()?.TileCount ?? 12;

            // slotCount = 타일수 → gap 1칸 → slot 0/1 = 인접 타일. 전사 옆에 도적 → 전사 패시브(+50%) 조건.
            Warrior = spawner?.Spawn(warriorPreset, 0, tileCount);
            Rogue   = spawner?.Spawn(roguePreset,   1, tileCount);

            if (demoMonster != null && CombatManager.Instance != null)
            {
                var enc = new EncounterDefinition { MonsterPresets = new List<MonsterPreset> { demoMonster } };
                CombatManager.Instance.StartEncounter(enc, 1); // 몬스터 스폰 + 인트로 + 전투 개시
                DemoMonster = CombatManager.Instance.ActiveMonsters.FirstOrDefault(m => m != null);
            }
            else
            {
                Debug.LogError("[TutorialScenario] demoMonster/CombatManager 없음 — 데모 전투 개시 불가.");
            }
        }

        /// <summary>데모 정리 — 실제 런 시작 전에 데모 파티/몬스터 제거(파티 0으로 → Recruit가 2명 선택으로 시작).</summary>
        public void Cleanup()
        {
            PartyManager.Instance?.ClearAll();   // 데모 파티 제거 + 오브젝트 파괴
            foreach (var m in Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
                if (m != null) Destroy(m.gameObject);
            Warrior = null; Rogue = null; DemoMonster = null;
        }
    }
}
