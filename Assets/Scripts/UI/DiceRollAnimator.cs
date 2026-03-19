using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 턴 시작 시 주사위 획득 연출 애니메이터
    /// 화면 중앙에서 일렬 팝인 → 순서대로 점프 → 슬롯으로 이동
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

        [Header("Jump & Spin (Step 1)")]
        [Tooltip("위로 점프하며 솟구치는 시간")]
        [SerializeField] private float jumpUpDuration = 0.25f;
        [Tooltip("점프 높이 (px)")]
        [SerializeField] private float jumpHeight = 150f;
        [Tooltip("이동 중 회전 횟수 (360도 * N)")]
        [SerializeField] private float rotationCycles = 2f;

        [Header("Pause (Step 2)")]
        [Tooltip("공중에서 멈추는 시간")]
        [SerializeField] private float pauseDuration = 0.12f;

        [Header("Zoom In (Step 3)")]
        [Tooltip("슬롯으로 빠르게 빨려 들어가는 시간")]
        [SerializeField] private float zoomInDuration = 0.22f;
        [Tooltip("주사위 간 출발 딜레이")]
        [SerializeField] private float delayBetweenDice = 0.12f;

        [Header("Arrive Bounce")]
        [Tooltip("도착 시 bounce 스케일 최대값")]
        [SerializeField] private float bounceScale = 1.25f;
        [Tooltip("bounce 애니메이션 시간")]
        [SerializeField] private float bounceDuration = 0.15f;

        // 애니메이션 완료 콜백
        public System.Action OnAnimationComplete;

        private Canvas rootCanvas;

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
                // 각 element의 현재 상태 저장 (레이아웃 그룹 등에 의해 결정된 최종 상태)
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
            }

            // ─── 3단계: 동시에 팝인 (스케일 0 → 1, EaseOutBack) ───
            yield return StartCoroutine(PopIn(rects, popInDuration));

            // 잠깐 간격
            yield return new WaitForSeconds(0.05f);

            // ─── 4단계: 하나씩 순서대로 슬롯으로 이동 ───
            // 각 Coroutine을 순서 딜레이를 두고 발사
            for (int i = 0; i < count; i++)
            {
                int idx = i; // 클로저용
                StartCoroutine(MoveToSlot(
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
            float totalWait = (count - 1) * delayBetweenDice + jumpUpDuration + pauseDuration + zoomInDuration + bounceDuration + 0.1f;
            yield return new WaitForSeconds(totalWait);

            OnAnimationComplete?.Invoke();
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

        // ─── 개별 주사위 슬롯 이동 (3단계 시퀀스) ───
        private IEnumerator MoveToSlot(RectTransform rect, DiceElement element, Transform container, Vector2 localSlotPos, 
            Vector2 origAnchorMin, Vector2 origAnchorMax, Vector2 origPivot, Vector2 origSize, float initialDelay)
        {
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);

            if (rect == null) yield break;

            // 1. 공중 점프 & 회전
            Vector2 spawnPos = rect.anchoredPosition;
            Vector2 midAirPos = spawnPos + Vector2.up * jumpHeight;

            float elapsed = 0f;
            while (elapsed < jumpUpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / jumpUpDuration);
                float smoothT = EaseOutQuad(t);

                rect.anchoredPosition = Vector2.Lerp(spawnPos, midAirPos, smoothT);
                rect.localRotation = Quaternion.Euler(0, 0, t * 360f * rotationCycles);
                yield return null;
            }

            rect.anchoredPosition = midAirPos;
            rect.localRotation = Quaternion.Euler(0, 0, 360f * rotationCycles);

            // 2. 공중 일시정지
            if (pauseDuration > 0)
                yield return new WaitForSeconds(pauseDuration);

            if (rect == null) yield break;

            // 3. 슬롯으로 빠르게 Zoom In
            // 슬롯의 Canvas 기준 절대 위치를 계산
            RectTransform containerRect = container as RectTransform;
            Vector2 targetCanvasPos = GetCanvasLocalPosition(containerRect, localSlotPos);

            elapsed = 0f;
            while (elapsed < zoomInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / zoomInDuration);
                float smoothT = EaseInQuad(t);

                rect.anchoredPosition = Vector2.Lerp(midAirPos, targetCanvasPos, smoothT);
                
                // 슬롯에 도착할 때 회전도 정위치로 (0도)
                rect.localRotation = Quaternion.Lerp(Quaternion.Euler(0, 0, 360f * rotationCycles), Quaternion.identity, smoothT);

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
