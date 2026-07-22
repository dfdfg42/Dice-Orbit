using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Artifacts;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 보유 유물의 단일 출처 + 효과 질의 창구 (스펙 2026-07-21).
    /// 유물 = ArtifactData(에셋) + RuntimeArtifact(획득 시 CreateInstance로 복제한 런타임 인스턴스).
    /// 보유 인스턴스 자체가 ICombatReactor라 CombatPipeline이 Artifacts를 그대로 수집한다.
    /// 규칙형 효과는 소비처(상점/휴식/보상/부활/전투 시작)가 프로퍼티로 합산값을 읽는다.
    /// artifactPool이 비어 있으면 기본 5종을 런타임 생성 (에셋 셋업 전에도 동작).
    /// </summary>
    public class ArtifactManager : MonoBehaviour
    {
        public static ArtifactManager Instance { get; private set; }

        [Header("유물 풀 — 획득 후보 (엘리트 드랍/상점 진열). 비우면 기본 세트 런타임 생성")]
        [SerializeField] private List<ArtifactData> artifactPool = new List<ArtifactData>();

        [Header("시작 유물 — 게임 시작 시 바로 보유 (테스트/디버그용)")]
        [SerializeField] private List<ArtifactData> startingArtifacts = new List<ArtifactData>();

        private readonly List<RuntimeArtifact> artifacts = new List<RuntimeArtifact>();

        public IReadOnlyList<RuntimeArtifact> Artifacts => artifacts;
        public event System.Action OnArtifactsChanged;

        // ── 규칙형 질의 (소비처가 읽는다) ──
        public float ShopDiscount01  => Mathf.Clamp01(artifacts.Sum(a => a.ShopDiscountPercent) / 100f);
        public float RestHealBonus01 => artifacts.Sum(a => a.RestHealBonusPercent) / 100f;
        public int   BattleGoldBonus => artifacts.Sum(a => a.BattleGoldBonus);
        public float ReviveHpBonus01 => artifacts.Sum(a => a.ReviveHpBonusPercent) / 100f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            EnsureDefaultPool();

            foreach (var data in startingArtifacts)
                Grant(data);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static ArtifactManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ArtifactManager>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("ArtifactManager").AddComponent<ArtifactManager>();
        }

        // ── 획득/제거 ──────────────────────────────────────────

        public bool Owns(ArtifactData data)
            => data != null && artifacts.Any(a => a.data == data);

        /// <summary>에셋 기준 획득 — effect 프로토타입을 복제해 보유 목록에 추가.</summary>
        public void Grant(ArtifactData data)
        {
            if (data == null || Owns(data)) return;
            if (data.effect == null)
            {
                Debug.LogError($"[Artifact] '{data.artifactName}' 에셋에 effect가 없습니다 — SubclassPicker로 유물 클래스를 지정하세요.");
                return;
            }
            artifacts.Add(data.effect.CreateInstance(data));
            Debug.Log($"[Artifact] 획득: {data.artifactName}");
            OnArtifactsChanged?.Invoke();
        }

        /// <summary>런타임 인스턴스 직접 추가 (디버그/특수 — data 없는 유물 허용, HUD는 클래스명 표시).</summary>
        public void AddArtifact(RuntimeArtifact artifact)
        {
            if (artifact == null) return;
            artifacts.Add(artifact);
            Debug.Log($"[Artifact] Added: {artifact.GetType().Name}");
            OnArtifactsChanged?.Invoke();
        }

        /// <summary>T 타입(상속 포함) 유물 전부 제거.</summary>
        public bool RemoveArtifact<T>() where T : RuntimeArtifact
        {
            int removed = artifacts.RemoveAll(a => a is T);
            if (removed == 0) return false;
            Debug.Log($"[Artifact] Removed {removed} × {typeof(T).Name}");
            OnArtifactsChanged?.Invoke();
            return true;
        }

        public bool RemoveArtifact(RuntimeArtifact artifact)
        {
            if (artifact == null || !artifacts.Remove(artifact)) return false;
            Debug.Log($"[Artifact] Removed: {artifact.GetType().Name}");
            OnArtifactsChanged?.Invoke();
            return true;
        }

        // ── 풀 (드랍/상점/세이브) ─────────────────────────────

        /// <summary>미보유 풀에서 랜덤 1개 획득. 없으면 null.</summary>
        public ArtifactData GrantRandom()
        {
            var candidates = artifactPool.Where(d => d != null && d.effect != null && !Owns(d)).ToList();
            if (candidates.Count == 0) return null;
            var picked = candidates[Random.Range(0, candidates.Count)];
            Grant(picked);
            return picked;
        }

        /// <summary>상점 진열용: 미보유 유물 랜덤 count개.</summary>
        public List<ArtifactData> GetShopOfferings(int count)
            => artifactPool.Where(d => d != null && d.effect != null && !Owns(d))
                .OrderBy(_ => Random.value).Take(count).ToList();

        /// <summary>이름으로 풀에서 찾기 (세이브 복원용).</summary>
        public ArtifactData FindInPool(string artifactName)
            => artifactPool.FirstOrDefault(d => d != null && d.artifactName == artifactName);

        // ── 기본 풀 (에셋 미지정 폴백) ─────────────────────────

        private void EnsureDefaultPool()
        {
            if (artifactPool.Count > 0) return;

            artifactPool.Add(CreateDefault("단골 도장", "상점 가격 20% 할인", new RegularStamp(), 100));
            artifactPool.Add(CreateDefault("포근한 침낭", "휴식 회복량 +20%p", new CozyBedroll(), 110));
            artifactPool.Add(CreateDefault("황금 주사위", "전투 보상 골드 +25", new GoldenDice(), 130));
            artifactPool.Add(CreateDefault("불사조 깃털", "부활 HP +15%p", new PhoenixFeather(), 150));
            artifactPool.Add(CreateDefault("생명의 부적", "전투 시작 시 파티 전원 5 회복", new LifeAmulet(), 120));
            Debug.Log("[ArtifactManager] 유물 풀이 비어 있어 기본 5종을 런타임 생성했습니다 (에셋으로 교체 권장).");
        }

        private static ArtifactData CreateDefault(string name, string desc, RuntimeArtifact effect, int price)
        {
            var data = ScriptableObject.CreateInstance<ArtifactData>();
            data.name = name;
            data.artifactName = name;
            data.artifactTooltip = desc;
            data.effect = effect;
            data.shopPrice = price;
            return data;
        }
    }
}
