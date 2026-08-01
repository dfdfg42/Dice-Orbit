using DiceOrbit.Core.Run.Save;
using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 골드(통화) 관리자. 런(run) 동안 누적되며 씬 재로드(다시 시작) 시 초기화된다.
    /// 현재는 보상으로 받아 누적만 하고, 추후 상점에서 소비할 수 있다.
    /// </summary>
    public class GoldManager : MonoBehaviour, IRunSaveParticipant
    {
        public static GoldManager Instance { get; private set; }

        [SerializeField] private int gold = 0;

        public int Gold => gold;

        /// <summary>골드량이 바뀔 때마다 호출 (새 잔액).</summary>
        public event System.Action<int> OnGoldChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static GoldManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<GoldManager>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }

            var go = new GameObject("GoldManager");
            return go.AddComponent<GoldManager>();
        }

        public void AddGold(int amount)
        {
            if (amount == 0) return;
            gold = Mathf.Max(0, gold + amount);
            OnGoldChanged?.Invoke(gold);
        }

        /// <summary>골드를 소비한다(상점용). 부족하면 false.</summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0 || gold < amount) return false;
            gold -= amount;
            OnGoldChanged?.Invoke(gold);
            return true;
        }

        public void ResetGold()
        {
            gold = 0;
            OnGoldChanged?.Invoke(gold);
        }

        // ── 세이브 참가자 ──────────────────────────────────────

        public void Capture(RunSaveData data) => data.Gold = gold;

        public void Validate(RunSaveData data, RunRestoreContext ctx)
        {
            // 정수 하나라 해결할 ID가 없다 — 검증할 것이 없다.
        }

        public void Apply(RunSaveData data, RunRestoreContext ctx)
        {
            gold = Mathf.Max(0, data.Gold);
            OnGoldChanged?.Invoke(gold);
        }
    }
}
