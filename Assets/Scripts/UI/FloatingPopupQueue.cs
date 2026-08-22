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
        [Tooltip("팝업 하나를 띄운 뒤 다음까지의 간격(초)")]
        [SerializeField] private float interval = 0.32f;
        [Tooltip("대기열이 이만큼 밀리면 간격을 줄여 따라잡는다 — 연출이 전투보다 뒤처지지 않게")]
        [SerializeField] private int catchUpThreshold = 6;
        [SerializeField] private float catchUpInterval = 0.1f;

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
        private readonly Dictionary<Transform, StackState> _stacks = new Dictionary<Transform, StackState>();
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
        /// </summary>
        public static void Enqueue(Transform anchor, float height, System.Action<Vector3> spawn)
        {
            if (anchor == null || spawn == null) return;

            var queue = EnsureInstance();
            queue._queue.Enqueue(new Request { Anchor = anchor, Height = height, Spawn = spawn });

            if (queue._drain == null && queue.isActiveAndEnabled)
                queue._drain = queue.StartCoroutine(queue.DrainRoutine());
        }

        private IEnumerator DrainRoutine()
        {
            while (_queue.Count > 0)
            {
                var request = _queue.Dequeue();

                if (request.Anchor != null)
                {
                    request.Spawn(request.Anchor.position + Vector3.up * (request.Height + ResolveStagger(request.Anchor)));

                    // 표시한 것에만 간격을 준다 — 사라진 유닛의 요청은 건너뛰고 바로 다음으로.
                    yield return new WaitForSeconds(_queue.Count >= catchUpThreshold ? catchUpInterval : interval);
                }
            }

            _drain = null;
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
