using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Core.Combo
{
    /// <summary>
    /// 캐릭터별 콤보 단계의 단일 권위 (2026-08-28). 트래커는 캐릭터마다 독립이며,
    /// 전투 시작/종료·미이동 턴·사망 시 초기화는 CombatManager/AutoAttackSystem이 이곳을 호출한다.
    /// UI(콤보 핍)는 OnComboChanged를 구독한다.
    /// </summary>
    public class ComboSystem : MonoBehaviour
    {
        public static ComboSystem Instance { get; private set; }

        private readonly Dictionary<Character, ComboTracker> _trackers = new Dictionary<Character, ComboTracker>();

        /// <summary>(캐릭터, 변경 후 단계 0~2, 이유). Finished는 3단계 발동 직후 0이 되어 도착한다.</summary>
        public event System.Action<Character, int, ComboOutcome> OnComboChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static ComboSystem EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ComboSystem>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("[ComboSystem]").AddComponent<ComboSystem>();
        }

        public ComboTracker GetTracker(Character character)
        {
            if (character == null) return null;
            if (!_trackers.TryGetValue(character, out var tracker))
            {
                tracker = new ComboTracker();
                _trackers[character] = tracker;
            }
            return tracker;
        }

        /// <summary>UI용 — 현재 단계(0~2). 트래커가 없으면 0.</summary>
        public int PeekStage(Character character)
            => character != null && _trackers.TryGetValue(character, out var t) ? t.Stage : 0;

        /// <summary>강화공격 실행 확정 — 전진/순환시키고 UI에 알린다.</summary>
        public void ReportExecuted(Character character)
        {
            var tracker = GetTracker(character);
            if (tracker == null) return;
            var outcome = tracker.ConfirmExecuted();
            OnComboChanged?.Invoke(character, tracker.Stage, outcome);
        }

        /// <summary>콤보 끊김/초기화. 이미 0이면 조용히 넘어간다 (기본공격마다 깜빡이지 않게).</summary>
        public void ResetCombo(Character character, ComboOutcome reason)
        {
            var tracker = GetTracker(character);
            if (tracker == null) return;
            if (tracker.Stage == 0) return;
            tracker.Reset();
            OnComboChanged?.Invoke(character, 0, reason);
        }

        /// <summary>전투 시작/종료 시 전체 초기화.</summary>
        public void ResetAll()
        {
            foreach (var pair in _trackers)
            {
                if (pair.Value.Stage == 0) continue;
                pair.Value.Reset();
                if (pair.Key != null) OnComboChanged?.Invoke(pair.Key, 0, ComboOutcome.ResetBySystem);
            }
            _trackers.Clear();
        }
    }
}
