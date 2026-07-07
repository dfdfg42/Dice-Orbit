using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 보유 유물의 단일 출처 + 효과 질의 창구 (스펙 §5).
    /// 소비처(상점/휴식/보상/부활/전투 시작)가 프로퍼티로 합산 효과를 읽는다 — 이벤트 배선 없음.
    ///
    /// relicPool이 비어 있으면 기본 세트를 런타임 생성 (에셋 셋업 전에도 동작).
    /// 획득: 엘리트 드랍(GrantRandom) + 상점 구매(GetShopOfferings → Grant).
    /// </summary>
    public class RelicManager : MonoBehaviour
    {
        public static RelicManager Instance { get; private set; }

        [Header("유물 풀 — 획득 '후보' 목록 (엘리트 드랍/상점 진열). 비우면 기본 세트 런타임 생성")]
        [SerializeField] private List<RelicDefinition> relicPool = new List<RelicDefinition>();

        [Header("시작 유물 — 게임 시작 시 바로 보유 (테스트/디버그용, HUD 렐릭칸에 즉시 표시)")]
        [SerializeField] private List<RelicDefinition> startingRelics = new List<RelicDefinition>();

        private readonly List<RelicDefinition> _owned = new List<RelicDefinition>();
        private string _pendingAnnouncement;   // 엘리트 드랍 안내 (보상 화면이 1회 소비)

        public IReadOnlyList<RelicDefinition> Owned => _owned;
        public event System.Action OnRelicsChanged;

        // ── 효과 질의 (소비처가 읽는다) ──
        public float ShopDiscount01   => Mathf.Clamp01(Sum(RelicEffectType.ShopDiscountPercent) / 100f);
        public float RestHealBonus01  => Sum(RelicEffectType.RestHealBonusPercent) / 100f;
        public int   BattleGoldBonus  => Mathf.RoundToInt(Sum(RelicEffectType.BattleGoldBonusFlat));
        public float ReviveHpBonus01  => Sum(RelicEffectType.ReviveHpBonusPercent) / 100f;
        public int   BattleStartHeal  => Mathf.RoundToInt(Sum(RelicEffectType.BattleStartHealFlat));

        private float Sum(RelicEffectType type)
            => _owned.Where(r => r != null && r.EffectType == type).Sum(r => r.Value);

        /// <summary>보유 유물의 전투 반응 효과 — CombatPipeline이 리액터로 수집 (구 ArtifactManager 대체).</summary>
        public IEnumerable<Pipeline.ICombatReactor> CombatReactors
        {
            get
            {
                foreach (var relic in _owned)
                    if (relic != null && relic.CombatEffect != null)
                        yield return relic.CombatEffect;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            EnsureDefaultPool();

            foreach (var relic in startingRelics)
                Grant(relic);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static RelicManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<RelicManager>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("RelicManager").AddComponent<RelicManager>();
        }

        // ── 획득 ──────────────────────────────────────────────

        public void Grant(RelicDefinition relic)
        {
            if (relic == null || _owned.Contains(relic)) return;
            _owned.Add(relic);
            Debug.Log($"[Relic] 획득: {relic.RelicName}");
            OnRelicsChanged?.Invoke();
        }

        /// <summary>미보유 풀에서 랜덤 1개 획득 (엘리트 드랍). 없으면 null.</summary>
        public RelicDefinition GrantRandom()
        {
            var candidates = relicPool.Where(r => r != null && !_owned.Contains(r)).ToList();
            if (candidates.Count == 0) return null;

            var picked = candidates[Random.Range(0, candidates.Count)];
            Grant(picked);
            _pendingAnnouncement = picked.RelicName;
            return picked;
        }

        /// <summary>보상 화면용 드랍 안내 1회 소비.</summary>
        public string ConsumePendingAnnouncement()
        {
            var msg = _pendingAnnouncement;
            _pendingAnnouncement = null;
            return msg;
        }

        /// <summary>상점 진열용: 미보유 유물 랜덤 count개.</summary>
        public List<RelicDefinition> GetShopOfferings(int count)
        {
            return relicPool.Where(r => r != null && !_owned.Contains(r))
                .OrderBy(_ => Random.value).Take(count).ToList();
        }

        // ── 기본 풀 (에셋 미지정 폴백) ─────────────────────────

        private void EnsureDefaultPool()
        {
            if (relicPool.Count > 0) return;

            relicPool.Add(CreateDefault("단골 도장", "상점 가격 20% 할인", RelicEffectType.ShopDiscountPercent, 20f, 100));
            relicPool.Add(CreateDefault("포근한 침낭", "휴식 회복량 +20%p", RelicEffectType.RestHealBonusPercent, 20f, 110));
            relicPool.Add(CreateDefault("황금 주사위", "전투 보상 골드 +25", RelicEffectType.BattleGoldBonusFlat, 25f, 130));
            relicPool.Add(CreateDefault("불사조 깃털", "부활 HP +15%p", RelicEffectType.ReviveHpBonusPercent, 15f, 150));
            relicPool.Add(CreateDefault("생명의 부적", "전투 시작 시 파티 전원 5 회복", RelicEffectType.BattleStartHealFlat, 5f, 120));
            Debug.Log("[RelicManager] 유물 풀이 비어 있어 기본 5종을 런타임 생성했습니다 (에셋으로 교체 권장).");
        }

        private static RelicDefinition CreateDefault(string name, string desc, RelicEffectType type, float value, int price)
        {
            var def = ScriptableObject.CreateInstance<RelicDefinition>();
            def.name = name;
            def.RelicName = name;
            def.Description = desc;
            def.EffectType = type;
            def.Value = value;
            def.ShopPrice = price;
            return def;
        }
    }
}
