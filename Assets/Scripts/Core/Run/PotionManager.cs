using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 포션 인벤토리 (파티 공용 3슬롯) + 효과 실행 (스펙 §5).
    /// 사용: 전투 중 아무 때나, 행동 소모 없음. 슬롯 확장은 유물 거리(후속).
    ///
    /// 모든 포션은 대응하는 Potion .asset을 갖는다 — 런타임 생성 없음, potionPool은 에셋 등록 필수.
    /// 획득: 상점 구매 + 전투 보상 저확률 드랍.
    /// </summary>
    public class PotionManager : MonoBehaviour
    {
        public static PotionManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private int slotCount = 3;

        [Header("포션 풀 — 획득 '후보' 목록 (상점 진열/드랍). 에셋 등록 필수 — 비어 있으면 후보 없음")]
        [SerializeField] private List<Potion> potionPool = new List<Potion>();

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

        public bool TryAdd(Potion potion)
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

        /// <summary>슬롯 전부 비우기 — 세이브 복원이 이전 런의 잔여 상태 위에 덮어쓰지 않게 한다.</summary>
        public void ClearAll()
        {
            if (_slots.Count == 0) return;
            _slots.Clear();
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

    }
}
