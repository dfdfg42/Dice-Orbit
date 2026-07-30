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
        [SerializeField] private List<Potion> potionPool = new List<Potion>();

        [Header("시작 포션 — 게임 시작 시 슬롯에 지급 (테스트/디버그용)")]
        [SerializeField] private List<Potion> startingPotions = new List<Potion>();

        private readonly List<Potion> _slots = new List<Potion>();

        public int SlotCount => slotCount;
        public IReadOnlyList<Potion> Slots => _slots;
        public bool HasFreeSlot => _slots.Count < slotCount;
        public event System.Action OnChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) 
            { 
                Destroy(gameObject); 
                return; 
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            EnsureDefaultPool();

            foreach (var potion in startingPotions)
                TryAdd(potion);
        }

        private void Start()
        {
            EnsureCombatHook();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_hookedCombat != null) _hookedCombat.OnCombatStart -= RerollRandomPotions;
        }

        // ── 랜덤 포션 변신 (스펙 2026-07-29 §4) ────────────────

        private CombatManager _hookedCombat;

        /// <summary>전투 시작 구독 보장 — 씬 리로드로 CombatManager가 바뀌어도 재구독 (Skeleton 훅 패턴).</summary>
        private void EnsureCombatHook()
        {
            var cm = CombatManager.Instance;
            if (cm == null || _hookedCombat == cm) return;

            if (_hookedCombat != null) _hookedCombat.OnCombatStart -= RerollRandomPotions;
            cm.OnCombatStart += RerollRandomPotions;
            _hookedCombat = cm;
        }

        /// <summary>슬롯의 랜덤 포션을 풀의 무작위 일반 포션으로 교체 (전투 시작마다).</summary>
        private void RerollRandomPotions()
        {
            bool changed = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!(_slots[i] is Data.Potions.RandomPotion)) continue;

                var candidates = potionPool.Where(p => p != null && !(p is Data.Potions.RandomPotion)).ToList();
                if (candidates.Count == 0) continue;

                var picked = candidates[Random.Range(0, candidates.Count)];
                Debug.Log($"[Potion] 랜덤 포션 변신 → {picked.PotionName}");
                _slots[i] = picked;
                changed = true;
            }
            if (changed) OnChanged?.Invoke();
        }

        public static PotionManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<PotionManager>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("PotionManager").AddComponent<PotionManager>();
        }

        // ── 인벤토리 ──────────────────────────────────────────

        public bool TryAdd(Potion potion)
        {
            if (potion == null || !HasFreeSlot) return false;
            _slots.Add(potion);
            EnsureCombatHook();   // 씬 리로드 후에도 변신 구독 유지
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

        /// <summary>대상 지정이 필요한 포션인가 (조준 아크 진입 대상 — PotionTargetSelector).</summary>
        public static bool RequiresTarget(Potion potion)
            => potion != null && potion.TargetType != PotionTargetType.None;

        /// <summary>포션 사용 (자동 대상형). 조건 불충족이면 false — 슬롯 유지.</summary>
        public bool TryUse(int index)
        {
            if (index < 0 || index >= _slots.Count) return false;
            var potion = _slots[index];

            if (RequiresTarget(potion))
            {
                Debug.LogWarning($"[Potion] '{potion.PotionName}'은(는) 대상 지정이 필요 — TryUseOn 사용.");
                return false;
            }

            if (potion.CombatOnly && GameFlowManager.Instance?.CurrentState != GameState.Combat)
            {
                Debug.Log($"[Potion] '{potion.PotionName}'은(는) 전투 중에만 사용 가능.");
                return false;
            }

            if (!potion.Use()) return false;

            Debug.Log($"[Potion] 사용: {potion.PotionName}");
            _slots.RemoveAt(index);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>대상 지정형 포션 사용.</summary>
        public bool TryUseOn(int index, Unit target)
        {
            if (index < 0 || index >= _slots.Count) return false;
            var potion = _slots[index];

            if (!RequiresTarget(potion))
            {
                return TryUse(index);
            }

            if (target == null) return false;

            if (potion.TargetType == PotionTargetType.Ally && !(target is Character)) return false;
            if (potion.TargetType == PotionTargetType.Enemy && !(target is Monster)) return false;

            if (potion.CombatOnly && GameFlowManager.Instance?.CurrentState != GameState.Combat)
            {
                Debug.Log($"[Potion] '{potion.PotionName}'은(는) 전투 중에만 사용 가능.");
                return false;
            }

            if (!potion.Use(target)) return false;

            Debug.Log($"[Potion] 사용: {potion.PotionName} → {target.name}");
            _slots.RemoveAt(index);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>타일 대상 포션 사용 (예: 중화 포션).</summary>
        public bool TryUseOnTile(int index, Data.TileData tile)
        {
            if (index < 0 || index >= _slots.Count) return false;
            var potion = _slots[index];

            if (potion.TargetType != PotionTargetType.Tile || tile == null) return false;

            if (potion.CombatOnly && GameFlowManager.Instance?.CurrentState != GameState.Combat)
            {
                Debug.Log($"[Potion] '{potion.PotionName}'은(는) 전투 중에만 사용 가능.");
                return false;
            }

            if (!potion.UseOnTile(tile)) return false;

            Debug.Log($"[Potion] 사용: {potion.PotionName} → 타일 {tile.name}");
            _slots.RemoveAt(index);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>랜덤 드랍용: 풀에서 랜덤 1개 (슬롯 가득이면 null).</summary>
        public Potion GrantRandomDrop()
        {
            if (!HasFreeSlot || potionPool.Count == 0) return null;
            var picked = potionPool[Random.Range(0, potionPool.Count)];
            return TryAdd(picked) ? picked : null;
        }

        /// <summary>이름으로 풀에서 찾기 (세이브 복원용).</summary>
        public Potion FindInPool(string potionName)
            => potionPool.FirstOrDefault(p => p != null && p.PotionName == potionName);

        /// <summary>상점 진열용 랜덤 count개 (중복 종류 허용 안 함).</summary>
        public List<Potion> GetShopOfferings(int count)
        {
            return potionPool.Where(p => p != null).OrderBy(_ => Random.value).Take(count).ToList();
        }

        // ── 기본 풀 (에셋 미지정 폴백) ─────────────────────────

        private class RuntimePotion : Potion
        {
            public System.Func<Unit, bool> onUse;
            public override bool Use(Unit target = null) => onUse?.Invoke(target) ?? false;
        }

        private void EnsureDefaultPool()
        {
            if (potionPool.Count > 0) return;

            //potionPool.Add(CreateDefault("회복 물약", "선택한 아군의 HP를 30 회복", PotionTargetType.Ally, 40, false, (t) => {
            //    if (t is Character c && c.Stats != null) { c.Stats.CurrentHP = Mathf.Min(c.Stats.MaxHP, c.Stats.CurrentHP + 30); return true; }
            //    return false;
            //}));

            //potionPool.Add(CreateDefault("연회의 물약", "파티 전원의 HP를 15 회복", PotionTargetType.None, 55, false, (t) => {
            //    var party = PartyManager.Instance?.Party;
            //    if (party == null) return false;
            //    foreach (var c in party) {
            //        if (c == null || !c.IsAlive || c.Stats == null) continue;
            //        c.Stats.CurrentHP = Mathf.Min(c.Stats.MaxHP, c.Stats.CurrentHP + 15);
            //    }
            //    return true;
            //}));

            //potionPool.Add(CreateDefault("재굴림 물약", "남은 주사위를 전부 다시 굴린다 (전투 중)", PotionTargetType.None, 60, true, (t) => {
            //    var dm = DiceManager.Instance;
            //    if (dm == null || dm.AvailableDiceCount == 0) return false;
            //    dm.RerollAvailableDice();
            //    return true;
            //}));

            //potionPool.Add(CreateDefault("정화 물약", "파티의 이동 저하/속박을 해제", PotionTargetType.None, 45, false, (t) => {
            //    var party = PartyManager.Instance?.Party;
            //    if (party == null) return false;
            //    foreach (var c in party) {
            //        if (c == null || !c.IsAlive || c.Stats == null) continue;
            //        c.Stats.MoveDebuff = 0;
            //        c.Stats.BindDebuff = 0;
            //    }
            //    return true;
            //}));

            //Debug.Log("[PotionManager] 포션 풀이 비어 있어 기본 4종을 런타임 생성했습니다 (에셋으로 교체 권장).");
        }

        private static Potion CreateDefault(string name, string desc, PotionTargetType targetType, int price, bool combatOnly, System.Func<Unit, bool> onUse)
        {
            var def = ScriptableObject.CreateInstance<RuntimePotion>();
            def.name = name;
            def.PotionName = name;
            def.Description = desc;
            def.TargetType = targetType;
            def.ShopPrice = price;
            def.CombatOnly = combatOnly;
            def.onUse = onUse;
            return def;
        }
    }
}
