using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core.Run.Save;
using DiceOrbit.Data.Artifacts;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 보유 유물의 단일 출처 + 효과 질의 창구 (스펙 2026-07-21).
    /// 유물 = ArtifactData(에셋) + RuntimeArtifact(획득 시 CreateInstance로 복제한 런타임 인스턴스).
    /// 보유 인스턴스 자체가 ICombatReactor라 CombatPipeline이 Artifacts를 그대로 수집한다.
    /// 규칙형 효과는 소비처(상점/휴식/보상/부활/전투 시작)가 프로퍼티로 합산값을 읽는다.
    /// 모든 유물은 대응하는 ArtifactData .asset을 갖는다 — 런타임 생성 없음, artifactPool은 에셋 등록 필수.
    /// </summary>
    public class ArtifactManager : MonoBehaviour, IRunSaveParticipant
    {
        public static ArtifactManager Instance { get; private set; }

        [Header("유물 풀 — 획득 후보 (엘리트 드랍/상점 진열). 에셋 등록 필수 — 비어 있으면 후보 없음")]
        [SerializeField] private List<ArtifactData> artifactPool = new List<ArtifactData>();

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

        /// <summary>보유 유물 전부 제거 — 세이브 복원이 이전 런의 잔여 상태 위에 덮어쓰지 않게 한다.</summary>
        public void ClearAll()
        {
            if (artifacts.Count == 0) return;
            artifacts.Clear();
            OnArtifactsChanged?.Invoke();
        }

        // ── 세이브 참가자 ──────────────────────────────────────

        public void Capture(Save.RunSaveData data)
        {
            data.Artifacts.Clear();
            foreach (var artifact in artifacts)
            {
                if (artifact == null) continue;
                if (artifact.data == null)
                {
                    // AddArtifact(디버그/특수 경로)로 들어온 에셋 없는 유물은 saveId가 없어 복원할 수 없다.
                    // 조용히 사라지면 이어하기 후 유물이 하나 빈 것을 알 길이 없으므로 남긴다.
                    Debug.LogWarning($"[RunSave] 에셋 없는 런타임 유물 '{artifact.GetType().Name}'은(는) 저장되지 않습니다.");
                    continue;
                }
                data.Artifacts.Add(new ArtifactSaveData { Id = artifact.data.SaveId });
            }
        }

        public void Validate(Save.RunSaveData data, RunRestoreContext ctx)
        {
            foreach (var saved in data.Artifacts)
            {
                if (string.IsNullOrEmpty(saved.Id))
                {
                    ctx.Report.Fail("유물 Id가 비어 있습니다.");
                    continue;
                }
                if (ctx.Catalog == null || ctx.Catalog.FindArtifact(saved.Id) == null)
                    ctx.Report.Fail($"유물 '{saved.Id}'를 카탈로그에서 찾지 못했습니다.");
            }
        }

        public void Apply(Save.RunSaveData data, RunRestoreContext ctx)
        {
            ClearAll();
            foreach (var saved in data.Artifacts)
                Grant(ctx.Catalog.FindArtifact(saved.Id));
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
    }
}
