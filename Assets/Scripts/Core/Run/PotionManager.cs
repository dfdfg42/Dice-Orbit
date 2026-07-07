using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 포션 인벤토리 (파티 공용 3슬롯) + 효과 실행 (스펙 §5).
    /// 사용: 전투 중 아무 때나, 행동 소모 없음. 슬롯 확장은 유물 거리(후속).
    ///
    /// potionPool이 비어 있으면 기본 세트를 런타임 생성.
    /// 획득: 상점 구매 + 전투 보상 저확률 드랍.
    /// </summary>
    public class PotionManager : MonoBehaviour
    {
        public static PotionManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private int slotCount = 3;

        [Header("포션 풀 — 획득 '후보' 목록 (상점 진열/드랍). 비우면 기본 세트 런타임 생성")]
        [SerializeField] private List<PotionDefinition> potionPool = new List<PotionDefinition>();

        [Header("시작 포션 — 게임 시작 시 슬롯에 지급 (테스트/디버그용)")]
        [SerializeField] private List<PotionDefinition> startingPotions = new List<PotionDefinition>();

        private readonly List<PotionDefinition> _slots = new List<PotionDefinition>();

        public int SlotCount => slotCount;
        public IReadOnlyList<PotionDefinition> Slots => _slots;
        public bool HasFreeSlot => _slots.Count < slotCount;
        public event System.Action OnChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            EnsureDefaultPool();

            foreach (var potion in startingPotions)
                TryAdd(potion);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static PotionManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<PotionManager>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("PotionManager").AddComponent<PotionManager>();
        }

        // ── 인벤토리 ──────────────────────────────────────────

        public bool TryAdd(PotionDefinition potion)
        {
            if (potion == null || !HasFreeSlot) return false;
            _slots.Add(potion);
            OnChanged?.Invoke();
            return true;
        }

        public void Discard(int index)
        {
            if (index < 0 || index >= _slots.Count) return;
            Debug.Log($"[Potion] 버림: {_slots[index].PotionName}");
            _slots.RemoveAt(index);
            OnChanged?.Invoke();
        }

        /// <summary>포션 사용. 조건 불충족(전투 전용을 밖에서 등)이면 false — 슬롯 유지.</summary>
        public bool TryUse(int index)
        {
            if (index < 0 || index >= _slots.Count) return false;
            var potion = _slots[index];

            if (potion.CombatOnly && GameFlowManager.Instance?.CurrentState != GameState.Combat)
            {
                Debug.Log($"[Potion] '{potion.PotionName}'은(는) 전투 중에만 사용 가능.");
                return false;
            }

            if (!Execute(potion)) return false;

            Debug.Log($"[Potion] 사용: {potion.PotionName}");
            _slots.RemoveAt(index);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>랜덤 드랍용: 풀에서 랜덤 1개 (슬롯 가득이면 null).</summary>
        public PotionDefinition GrantRandomDrop()
        {
            if (!HasFreeSlot || potionPool.Count == 0) return null;
            var picked = potionPool[Random.Range(0, potionPool.Count)];
            return TryAdd(picked) ? picked : null;
        }

        /// <summary>이름으로 풀에서 찾기 (세이브 복원용).</summary>
        public PotionDefinition FindInPool(string potionName)
            => potionPool.FirstOrDefault(p => p != null && p.PotionName == potionName);

        /// <summary>상점 진열용 랜덤 count개 (중복 종류 허용 안 함).</summary>
        public List<PotionDefinition> GetShopOfferings(int count)
        {
            return potionPool.Where(p => p != null).OrderBy(_ => Random.value).Take(count).ToList();
        }

        // ── 효과 실행 ─────────────────────────────────────────

        private static bool Execute(PotionDefinition potion)
        {
            var party = PartyManager.Instance?.Party;

            switch (potion.EffectType)
            {
                case PotionEffectType.HealLowestAlly:
                {
                    var target = party?.Where(c => c != null && c.IsAlive && c.Stats != null)
                        .OrderBy(c => (float)c.Stats.CurrentHP / Mathf.Max(1, c.Stats.MaxHP)).FirstOrDefault();
                    if (target == null) return false;
                    target.Stats.CurrentHP = Mathf.Min(target.Stats.MaxHP, target.Stats.CurrentHP + potion.Value);
                    return true;
                }

                case PotionEffectType.HealParty:
                {
                    if (party == null) return false;
                    foreach (var c in party)
                    {
                        if (c == null || !c.IsAlive || c.Stats == null) continue;
                        c.Stats.CurrentHP = Mathf.Min(c.Stats.MaxHP, c.Stats.CurrentHP + potion.Value);
                    }
                    return true;
                }

                case PotionEffectType.RerollDice:
                {
                    var dm = DiceManager.Instance;
                    if (dm == null || dm.AvailableDiceCount == 0) return false;
                    dm.RerollAvailableDice();
                    return true;
                }

                case PotionEffectType.CleanseParty:
                {
                    if (party == null) return false;
                    foreach (var c in party)
                    {
                        if (c == null || !c.IsAlive || c.Stats == null) continue;
                        c.Stats.MoveDebuff = 0;
                        c.Stats.BindDebuff = 0;
                    }
                    return true;
                }
            }
            return false;
        }

        // ── 기본 풀 (에셋 미지정 폴백) ─────────────────────────

        private void EnsureDefaultPool()
        {
            if (potionPool.Count > 0) return;

            potionPool.Add(CreateDefault("회복 물약", "가장 다친 아군의 HP를 30 회복", PotionEffectType.HealLowestAlly, 30, 40, false));
            potionPool.Add(CreateDefault("연회의 물약", "파티 전원의 HP를 15 회복", PotionEffectType.HealParty, 15, 55, false));
            potionPool.Add(CreateDefault("재굴림 물약", "남은 주사위를 전부 다시 굴린다 (전투 중)", PotionEffectType.RerollDice, 0, 60, true));
            potionPool.Add(CreateDefault("정화 물약", "파티의 이동 저하/속박을 해제", PotionEffectType.CleanseParty, 0, 45, false));
            Debug.Log("[PotionManager] 포션 풀이 비어 있어 기본 4종을 런타임 생성했습니다 (에셋으로 교체 권장).");
        }

        private static PotionDefinition CreateDefault(string name, string desc, PotionEffectType type, int value, int price, bool combatOnly)
        {
            var def = ScriptableObject.CreateInstance<PotionDefinition>();
            def.name = name;
            def.PotionName = name;
            def.Description = desc;
            def.EffectType = type;
            def.Value = value;
            def.ShopPrice = price;
            def.CombatOnly = combatOnly;
            return def;
        }
    }
}
