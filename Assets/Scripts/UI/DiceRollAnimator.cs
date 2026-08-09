using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 턴 시작 시 주사위 획득 연출 애니메이터
    /// 화면 중앙에서 일렬 팝인(전원 텀블) → 하나씩 확정(착지+펀치) → 전부 확정되면 슬롯으로 비행
    /// </summary>
    public class DiceRollAnimator : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("Canvas 중앙 기준 세로 오프셋 (양수 = 위, 음수 = 아래)")]
        [SerializeField] private float centerYOffset = 0f;
        [Tooltip("주사위 간 가로 간격 (px)")]
        [SerializeField] private float diceSpacing = 120f;

        [Header("Pop In")]
        [Tooltip("중앙에서 팝인 되는 시간")]
        [SerializeField] private float popInDuration = 0.18f;

        [Header("Center Confirm (Step 1)")]
        [Tooltip("주사위 확정 간격 (하나씩 차례로)")]
        [SerializeField] private float confirmInterval = 0.35f;
        [Tooltip("3D 착지 전체 시간 (감속 60% + '탁' 스냅 40%)")]
        [SerializeField] private float confirmSettleDuration = 0.65f;
        [Tooltip("확정 순간 스케일 펀치 배율")]
        [SerializeField] private float confirmPunchScale = 1.16f;
        [Tooltip("스케일 펀치 시간")]
        [SerializeField] private float confirmPunchDuration = 0.16f;
        [Tooltip("전부 확정된 뒤 잠깐 보여주는 시간")]
        [SerializeField] private float afterConfirmBeat = 0.3f;

        [Header("Fly To Panel (Step 2)")]
        [Tooltip("슬롯으로 빠르게 빨려 들어가는 시간")]
        [SerializeField] private float zoomInDuration = 0.22f;
        [Tooltip("주사위 간 출발 딜레이")]
        [SerializeField] private float delayBetweenDice = 0.12f;

    [Header("Pre-Arrival Number Shuffle")]
    [Tooltip("이동 중 숫자 랜덤 변경 연출 사용 여부")]
    [SerializeField] private bool useTravelShuffle = true;
    [Tooltip("랜덤 숫자 갱신 간격")]
    [SerializeField] private float shuffleInterval = 0.03f;
    [Tooltip("연출용 최소 눈금")]
    [SerializeField] private int shuffleMinValue = 1;
    [Tooltip("연출용 최대 눈금")]
    [SerializeField] private int shuffleMaxValue = 6;

        [Header("Arrive Bounce")]
        [Tooltip("도착 시 bounce 스케일 최대값")]
        [SerializeField] private float bounceScale = 1.25f;
        [Tooltip("bounce 애니메이션 시간")]
        [SerializeField] private float bounceDuration = 0.15f;

        // 애니메이션 완료 콜백
        public System.Action OnAnimationComplete;

        private Canvas rootCanvas;
    private readonly Dictionary<DiceElement, Coroutine> activeShuffleRoutines = new Dictionary<DiceElement, Coroutine>();

        private void Awake()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
                rootCanvas = FindFirstObjectByType<Canvas>();
        }

        /// <summary>
        /// 주사위 획득 애니메이션 전체 시퀀스 실행
        /// </summary>
        /// <param name="elements">생성된 DiceElement 목록 (이미 diceContainer 하위에 있는 상태)</param>
        /// <param name="diceContainer">최종 목적지 컨테이너</param>
        public void PlayRollAnimation(List<DiceElement> elements, Transform diceContainer)
        {
            StartCoroutine(RollSequence(elements, diceContainer));
        }

        private IEnumerator RollSequence(List<DiceElement> elements, Transform diceContainer)
        {
            if (elements == null || elements.Count == 0)
            {
                OnAnimationComplete?.Invoke();
                yield break;
            }

            int count = elements.Count;

            // RectTransform 참조 수집
            var rects = new RectTransform[count];
            for (int i = 0; i < count; i++)
                rects[i] = elements[i].GetComponent<RectTransform>();

            // ─── 1단계: 슬롯 최종 위치 저장 (컨테이너 레이아웃이 결정된 후 1프레임 대기) ───
            yield return null; // Layout 계산 대기

            var slotPositions = new Vector2[count];
            var originalAnchorMins = new Vector2[count];
            var originalAnchorMaxs = new Vector2[count];
            var originalPivots = new Vector2[count];
            var originalSizes = new Vector2[count];
            
            for (int i = 0; i < count; i++)
            {
                // ClearDice()가 애니메이션 도중 호출되면 element가 파괴될 수 있음
                if (rects[i] == null)
                {
                    OnAnimationComplete?.Invoke();
                    yield break;
                }
                slotPositions[i] = rects[i].anchoredPosition;
                originalAnchorMins[i] = rects[i].anchorMin;
                originalAnchorMaxs[i] = rects[i].anchorMax;
                originalPivots[i] = rects[i].pivot;
                originalSizes[i] = rects[i].sizeDelta;
            }

            // ─── 2단계: Canvas 루트로 이동, 화면 중앙에 일렬 배치 ───
            Canvas canvas = rootCanvas;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Vector2 canvasSize = canvasRect.sizeDelta;

            float totalWidth = (count - 1) * diceSpacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < count; i++)
            {
                // Canvas 루트로 reparent
                rects[i].SetParent(canvas.transform, false);
                rects[i].SetAsLastSibling();

                // 중앙 배치를 위해 Anchor와 Pivot을 (0.5, 0.5) 정중앙으로 강제 설정
                rects[i].anchorMin = new Vector2(0.5f, 0.5f);
                rects[i].anchorMax = new Vector2(0.5f, 0.5f);
                rects[i].pivot = new Vector2(0.5f, 0.5f);

                // 스케일 0에서 시작
                rects[i].localScale = Vector3.zero;

                // 화면 중앙 기준 위치
                rects[i].anchoredPosition = new Vector2(startX + i * diceSpacing, centerYOffset);

                // 드래그 불가 상태 (CanvasGroup)
                var cg = elements[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.blocksRaycasts = false;

                // 처음 등장 시점부터 숫자 셔플 시작
                StartContinuousShuffle(elements[i]);
            }

            // ─── 3단계: 동시에 팝인 (스케일 0 → 1, EaseOutBack) ───
            yield return StartCoroutine(PopIn(rects, popInDuration));

            // 잠깐 간격
            yield return new WaitForSeconds(0.05f);

            // ─── 4단계: 가운데서 하나씩 확정 (텀블 정지 → 확정 면 착지 + 펀치) ───
            for (int i = 0; i < count; i++)
            {
                if (i > 0) yield return new WaitForSeconds(confirmInterval);
                if (rects[i] == null) continue;

                StopContinuousShuffle(elements[i], true);   // 3D: 확정 면으로 착지 / 2D: 실제 값 복원
                StartCoroutine(ConfirmPunch(rects[i]));
            }

            // 마지막 착지가 끝나고, 전부 확정된 모습을 잠깐 보여준다
            yield return new WaitForSeconds(confirmSettleDuration + afterConfirmBeat);

            // ─── 5단계: 전부 확정 후 슬롯으로 비행 ───
            for (int i = 0; i < count; i++)
            {
                int idx = i; // 클로저용
                StartCoroutine(FlyToSlot(
                    rects[idx],
                    elements[idx],
                    diceContainer,
                    slotPositions[idx],
                    originalAnchorMins[idx],
                    originalAnchorMaxs[idx],
                    originalPivots[idx],
                    originalSizes[idx],
                    idx * delayBetweenDice
                ));
            }

            // 마지막 주사위가 도착할 때까지 대기
            float totalWait = (count - 1) * delayBetweenDice + zoomInDuration + bounceDuration + 0.1f;
            yield return new WaitForSeconds(totalWait);

            StopAllContinuousShuffle(true);

            OnAnimationComplete?.Invoke();
        }

        // ─── 확정 순간 스케일 펀치 (감속이 끝나고 스냅이 꽂히는 타이밍에 '탁') ───
        private IEnumerator ConfirmPunch(RectTransform rect)
        {
            yield return new WaitForSeconds(confirmSettleDuration * 0.8f);
            if (rect == null) yield break;

            float half = confirmPunchDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                if (rect == null) yield break;
                rect.localScale = Vector3.one * Mathf.Lerp(1f, confirmPunchScale, Mathf.Clamp01(elapsed / half));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                if (rect == null) yield break;
                rect.localScale = Vector3.one * Mathf.Lerp(confirmPunchScale, 1f, Mathf.Clamp01(elapsed / half));
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        // ─── 동시 팝인 ───
        private IEnumerator PopIn(RectTransform[] rects, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = EaseOutBack(t);
                foreach (var r in rects)
                    if (r != null) r.localScale = Vector3.one * scale;
                yield return null;
            }
            foreach (var r in rects)
                if (r != null) r.localScale = Vector3.one;
        }

        // ─── 개별 주사위: 확정된 상태로 슬롯까지 비행 ───
        private IEnumerator FlyToSlot(RectTransform rect, DiceElement element, Transform container, Vector2 localSlotPos, 
            Vector2 origAnchorMin, Vector2 origAnchorMax, Vector2 origPivot, Vector2 origSize, float initialDelay)
        {
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);

            if (rect == null) yield break;

            // 확정된 채로 현재 위치에서 슬롯까지 가속 비행
            Vector2 startPos = rect.anchoredPosition;
            RectTransform containerRect = container as RectTransform;
            Vector2 targetCanvasPos = GetCanvasLocalPosition(containerRect, localSlotPos);

            float elapsed = 0f;
            while (elapsed < zoomInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / zoomInDuration);
                float smoothT = EaseInQuad(t);

                rect.anchoredPosition = Vector2.Lerp(startPos, targetCanvasPos, smoothT);
                yield return null;
            }

            rect.anchoredPosition = targetCanvasPos;
            rect.localRotation = Quaternion.identity;

            // ─── 도착 후 컨테이너로 복귀 ───
            rect.SetParent(container, false);
            // 앵커와 피벗 원상복구
            rect.anchorMin = origAnchorMin;
            rect.anchorMax = origAnchorMax;
            rect.pivot = origPivot;
            rect.sizeDelta = origSize;
            
            rect.anchoredPosition = localSlotPos;
            rect.localScale = Vector3.one;

            // ─── Bounce ───
            yield return StartCoroutine(Bounce(rect, bounceDuration));

            // 드래그 활성화
            var cg = element.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;
        }

        private void StartContinuousShuffle(DiceElement element)
        {
            if (element == null)
                return;

            // 3D 모드: 숫자 셔플 대신 큐브가 실제로 구른다
            if (element.View3D != null)
            {
                element.View3D.SetTumbling(true);
                return;
            }

            if (!useTravelShuffle)
                return;

            StopContinuousShuffle(element, false);
            activeShuffleRoutines[element] = StartCoroutine(CoShuffleDisplay(element));
        }

        private void StopContinuousShuffle(DiceElement element, bool restoreRealValue)
        {
            if (element == null)
                return;

            // 3D 모드: 텀블을 멈추고 확정 면으로 착지
            if (element.View3D != null)
            {
                element.View3D.SetTumbling(false);
                if (restoreRealValue && element.Data != null)
                    element.View3D.SettleToValue(element.Data.Value, 0.3f);
                activeShuffleRoutines.Remove(element);
                return;
            }

            if (activeShuffleRoutines.TryGetValue(element, out Coroutine routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            activeShuffleRoutines.Remove(element);

            if (restoreRealValue)
            {
                element.RefreshDisplayFromData();
            }
        }

        private void StopAllContinuousShuffle(bool restoreRealValue)
        {
            if (activeShuffleRoutines.Count == 0)
                return;

            var keys = new List<DiceElement>(activeShuffleRoutines.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                StopContinuousShuffle(keys[i], restoreRealValue);
            }
        }

        private IEnumerator CoShuffleDisplay(DiceElement element)
        {
            int minValue = Mathf.Min(shuffleMinValue, shuffleMaxValue);
            int maxValue = Mathf.Max(shuffleMinValue, shuffleMaxValue);
            float safeShuffleInterval = Mathf.Max(0.01f, shuffleInterval);
            WaitForSeconds wait = new WaitForSeconds(safeShuffleInterval);

            while (element != null)
            {
                int randomValue = Random.Range(minValue, maxValue + 1);
                element.SetDisplayValue(randomValue);
                yield return wait;
            }
        }

        private void OnDisable()
        {
            StopAllContinuousShuffle(true);
        }

        // ─── 도착 bounce ───
        private IEnumerator Bounce(RectTransform rect, float duration)
        {
            float half = duration * 0.5f;
            float elapsed = 0f;

            // 커지기
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, bounceScale, t);
                yield return null;
            }

            elapsed = 0f;
            // 줄어들기
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                rect.localScale = Vector3.one * Mathf.Lerp(bounceScale, 1f, t);
                yield return null;
            }

            rect.localScale = Vector3.one;
        }

        // ─── Container 내 로컬 위치 → Canvas 절대 앵커 위치 변환 ───
        private Vector2 GetCanvasLocalPosition(RectTransform containerRect, Vector2 localInContainer)
        {
            // container의 월드 코너를 구한 뒤 Canvas local로 변환
            Vector3[] corners = new Vector3[4];
            containerRect.GetWorldCorners(corners);

            // 컨테이너 pivot 기준 월드 포지션
            Vector3 containerWorldPos = containerRect.TransformPoint(localInContainer);

            // Canvas RectTransform
            RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();
            Vector2 canvasLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(null, containerWorldPos),
                rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
                out canvasLocal
            );
            return canvasLocal;
        }

        // ─── Easing 함수들 ───

        /// <summary>EaseOutQuad - 점프 감속</summary>
        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>EaseInQuad - 줌 가속</summary>
        private static float EaseInQuad(float t)
        {
            return t * t;
        }

        /// <summary>EaseOutBack - 살짝 튀어오르는 팝인</summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>EaseInOutQuad - 부드러운 이동 (Legacy)</summary>
        private static float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }
    }
}
