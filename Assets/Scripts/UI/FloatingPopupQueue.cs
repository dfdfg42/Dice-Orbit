using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 떠오르는 팝업(피해 숫자·패시브/상태 버블)을 한 번에 하나씩 순서대로 띄우는 표시 대기열.
    ///
    /// 피해 계산은 파이프라인에서 동기로 끝나야 하므로 계산을 늦출 수는 없다.
    /// 대신 '보여주는 일'만 줄을 세워, 같은 프레임에 벌어진 사건들도 플레이어에게는
    /// 수호 -> 피해처럼 하나씩 읽히게 한다(요청 순서 = 표시 순서).
    ///
    /// 위치는 요청 시점이 아니라 표시 시점에 계산한다 — 대기 중 유닛이 움직여도 머리 위에 뜬다.
    /// </summary>
    public class FloatingPopupQueue : MonoBehaviour
    {
        public static FloatingPopupQueue Instance { get; private set; }

        [Header("표시 간격")]
        [Tooltip("이전 팝업이 '완전히 사라진 뒤' 다음까지 추가로 두는 간격(초). 팝업 수명은 자동으로 기다린다.")]
        [SerializeField] private float gapAfterFade = 0.05f;
        [Tooltip("대기열이 이만큼 밀리면 수명을 다 기다리지 않고 따라잡는다 — 연출이 전투보다 뒤처지지 않게")]
        [SerializeField] private int catchUpThreshold = 6;
        [Tooltip("따라잡기 중 팝업 수명의 몇 %만 기다릴지 (0.5 = 절반쯤 사라졌을 때 다음)")]
        [Range(0.2f, 1f)]
        [SerializeField] private float catchUpLifetimeScale = 0.5f;

        [Header("반복 억제")]
        [Tooltip("같은 유닛에 같은 문구가 이 시간 안에 다시 요청되면 무시한다(초). 피해 숫자처럼 키가 없는 요청은 억제하지 않는다.")]
        [SerializeField] private float duplicateWindow = 0.9f;

        [Header("겹침 방지")]
        [Tooltip("같은 유닛에 연달아 뜰 때 위로 밀어 올리는 간격(월드)")]
        [SerializeField] private float staggerStep = 0.55f;
        [Tooltip("이 시간이 지나면 같은 유닛의 쌓임을 초기화한다(초)")]
        [SerializeField] private float staggerResetSec = 1.1f;

        private struct Request
        {
            public Transform Anchor;
            public float Height;
            public System.Action<Vector3> Spawn;
        }

        private readonly Queue<Request> _queue = new Queue<Request>();
        // 버블이 전부 사라진 뒤 한꺼번에 띄울 요청(피해 숫자). 서로는 줄 서지 않는다.
        private readonly List<Request> _afterBubbles = new List<Request>();
        private readonly Dictionary<Transform, StackState> _stacks = new Dictionary<Transform, StackState>();
        private readonly Dictionary<(Transform, string), float> _lastByKey = new Dictionary<(Transform, string), float>();
        private Coroutine _drain;

        private struct StackState { public float LastTime; public int Count; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 모든 팝업 연출이 끝났는가 — 대기열이 비었고(마지막 버블 수명 대기 포함) 화면의 팝업도 전부 사라졌는가.
        /// 몬스터 턴 전환이 이걸 기다린다.
        /// </summary>
        public static bool AllPopupsFinished =>
            (Instance == null || (Instance._queue.Count == 0 && Instance._drain == null && Instance._afterBubbles.Count == 0))
            && FloatingLabelPopup.ActiveCount == 0;

        public static FloatingPopupQueue EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<FloatingPopupQueue>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("[FloatingPopupQueue]").AddComponent<FloatingPopupQueue>();
        }

        /// <summary>
        /// 팝업 하나를 대기열에 넣는다. spawn은 표시 시점에 계산된 월드 좌표를 받아 실제 팝업을 만든다 —
        /// 색·크기 같은 결정은 호출부가 그대로 쥔다.
        ///
        /// dedupKey를 주면 같은 유닛에 같은 키가 짧은 시간 안에 반복될 때 뒤엣것을 버린다.
        /// 패시브 이름처럼 반복이 정보가 아닌 경우에 쓴다 — 피해 숫자는 키 없이 넣어 매번 보이게 한다.
        /// </summary>
        public static void Enqueue(Transform anchor, float height, System.Action<Vector3> spawn, string dedupKey = null)
        {
            if (anchor == null || spawn == null) return;

            var queue = EnsureInstance();
            if (queue.IsSuppressedDuplicate(anchor, dedupKey)) return;

            queue._queue.Enqueue(new Request { Anchor = anchor, Height = height, Spawn = spawn });

            if (queue._drain == null && queue.isActiveAndEnabled)
                queue._drain = queue.StartCoroutine(queue.DrainRoutine());
        }

        /// <summary>
        /// 버블(앞서 요청된 순차 팝업)이 전부 사라진 뒤에 띄운다 — 피해 숫자용.
        /// 버블이 없으면 즉시 뜨고, 버블이 진행 중이면 끝나는 순간 밀린 것들이 한꺼번에 뜬다.
        /// 피해 숫자끼리는 줄 서지 않는다(동시 타격은 동시에 보여야 읽힌다).
        /// </summary>
        public static void EnqueueAfterBubbles(Transform anchor, float height, System.Action<Vector3> spawn)
        {
            if (anchor == null || spawn == null) return;

            var queue = EnsureInstance();
            if (queue._queue.Count == 0 && queue._drain == null)
            {
                spawn(anchor.position + Vector3.up * (height + queue.ResolveStagger(anchor)));
                return;
            }
            queue._afterBubbles.Add(new Request { Anchor = anchor, Height = height, Spawn = spawn });
        }

        private void FlushAfterBubbles()
        {
            if (_afterBubbles.Count == 0) return;
            foreach (var request in _afterBubbles)
                if (request.Anchor != null)
                    request.Spawn(request.Anchor.position + Vector3.up * (request.Height + ResolveStagger(request.Anchor)));
            _afterBubbles.Clear();
        }

        private IEnumerator DrainRoutine()
        {
            while (_queue.Count > 0)
            {
                var request = _queue.Dequeue();

                if (request.Anchor != null)
                {
                    request.Spawn(request.Anchor.position + Vector3.up * (request.Height + ResolveStagger(request.Anchor)));

                    // 이전 팝업이 완전히 사라진 뒤에 다음을 띄운다 — 한 번에 하나만 보이도록.
                    // 단, 많이 밀려 있으면 수명 일부만 기다려 따라잡는다 (사라진 유닛의 요청은 대기 없이 건너뜀).
                    float wait = _queue.Count >= catchUpThreshold
                        ? FloatingLabelPopup.Lifetime * catchUpLifetimeScale
                        : FloatingLabelPopup.Lifetime + gapAfterFade;
                    yield return new WaitForSeconds(wait);
                }
            }

            // 버블이 전부 사라졌다 — 기다리던 피해 숫자를 한꺼번에 방출.
            FlushAfterBubbles();
            _drain = null;
        }

        /// <summary>같은 유닛에 같은 문구가 방금 나갔으면 참. 반복 버블을 소음으로 보고 버린다.</summary>
        private bool IsSuppressedDuplicate(Transform anchor, string dedupKey)
        {
            if (string.IsNullOrEmpty(dedupKey)) return false;

            var id = (anchor, dedupKey);
            float now = Time.time;

            if (_lastByKey.TryGetValue(id, out float last) && now - last < duplicateWindow)
                return true;

            _lastByKey[id] = now;
            return false;
        }

        /// <summary>같은 유닛에 연달아 뜨는 팝업을 위로 쌓아 겹치지 않게 한다.</summary>
        private float ResolveStagger(Transform anchor)
        {
            float now = Time.time;
            int index = 0;

            if (_stacks.TryGetValue(anchor, out var state) && now - state.LastTime < staggerResetSec)
                index = state.Count;

            _stacks[anchor] = new StackState { LastTime = now, Count = index + 1 };
            return index * staggerStep;
        }
    }
}
