using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>튜토리얼 단계 재생 엔진. 오버레이를 구동하고 각 단계 진행조건까지 대기.
    /// 상태 전환(Tutorial→Recruit)은 BattleScene 내라 이 오브젝트가 지속되어 이어서 재생.</summary>
    public class TutorialDirector : MonoBehaviour
    {
        public static TutorialDirector Instance { get; private set; }

        private bool _confirmPressed;
        private Action _onComplete;
        private bool _aborted;

        /// <summary>스킵 시 실행할 콜백(시나리오/플로우가 주입). 실제 런으로 폴백.</summary>
        public Action TutorialSkipHandler;

        public static TutorialDirector EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("TutorialDirector");
            Instance = go.AddComponent<TutorialDirector>();
            return Instance;
        }

        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Play(List<TutorialStep> steps, Action onComplete)
        {
            _onComplete = onComplete;
            _aborted = false;
            StartCoroutine(Run(steps));
        }

        /// <summary>스킵/폴백 — 오버레이 제거. 완료 콜백은 호출하지 않음(호출자가 스킵 흐름 처리).</summary>
        public void Abort()
        {
            _aborted = true;
            DiceUI.Instance?.SetTutorialDiceLock(null);
            TutorialOverlayUI.Instance?.Hide();
            StopAllCoroutines();
        }

        private IEnumerator Run(List<TutorialStep> steps)
        {
            var overlay = TutorialOverlayUI.EnsureInstance();

            foreach (var step in steps)
            {
                if (_aborted) yield break;

                // 단계별 예외 격리 — 어떤 단계가 죽어도 전체가 멈추지 않게.
                try { step.OnEnter?.Invoke(); }
                catch (Exception e) { Debug.LogWarning($"[Tutorial] OnEnter 예외: {e}"); }

                _confirmPressed = false;
                bool isConfirm = step.Advance == TutorialAdvance.Confirm;

                overlay.ShowStep(
                    step.Instruction,
                    SafeTarget(step),
                    step.GateInput,
                    step.NoSpotlight,
                    onNext: isConfirm ? (Action)(() => _confirmPressed = true) : null,
                    onSkipAction: OnSkipRequested);

                DiceUI.Instance?.SetTutorialDiceLock(step.OnlyDieValue);   // 가이드된 눈만 선택 가능

                // 진행조건 대기: 매 프레임 대상 rect 갱신 + 조건 검사.
                while (!_aborted)
                {
                    if (!step.NoSpotlight)
                    {
                        var target = SafeTarget(step);
                        if (target != null)
                            overlay.HighlightScreenRect(TutorialOverlayUI.GetScreenRect(target));
                    }

                    bool done = isConfirm ? _confirmPressed : SafeDone(step);
                    if (done) break;
                    yield return null;
                }

                if (_aborted) yield break;
            }

            DiceUI.Instance?.SetTutorialDiceLock(null);
            overlay.Hide();
            var cb = _onComplete; _onComplete = null;
            cb?.Invoke();
        }

        private RectTransform SafeTarget(TutorialStep s)
        {
            try { return s.Target?.Invoke(); } catch { return null; }
        }
        private bool SafeDone(TutorialStep s)
        {
            try { return s.Done != null && s.Done(); } catch { return false; }
        }

        private void OnSkipRequested()
        {
            // 스킵 = 튜토리얼 포기. 호출자가 완료 플래그 세팅 + 실제 런 폴백을 처리.
            _aborted = true;
            DiceUI.Instance?.SetTutorialDiceLock(null);
            TutorialOverlayUI.Instance?.Hide();
            StopAllCoroutines();
            TutorialSkipHandler?.Invoke();
        }
    }
}
