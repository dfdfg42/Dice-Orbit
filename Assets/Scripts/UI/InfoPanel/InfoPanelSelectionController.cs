using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DiceOrbit.Core;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 정보 패널에 표시할 대상 결정 (스펙 §6).
    /// 평시: 호버 즉시 갱신 + 클릭 고정(핀), 핀 중 타 대상 호버는 임시 표시.
    /// 타게팅 모드: 호버 추적만, 클릭 고정 비활성 (클릭 = 스킬 대상 지정).
    /// 빈 타일 직접 호버: 타일 단독 뷰.
    /// 이 컴포넌트는 클릭을 소비하지 않는다(읽기 전용) — 기존 클릭 동작과 충돌 없음.
    /// </summary>
    public class InfoPanelSelectionController : MonoBehaviour
    {
        private Camera _cachedCamera;
        private IBattleInfoProvider _externalHover;   // UI 요소(파티 로스터 등)가 지정한 호버 대상

        public IBattleInfoProvider PinnedUnit  { get; private set; }
        public IBattleInfoProvider HoveredUnit { get; private set; }
        public TileData            HoveredTile { get; private set; }   // 유닛 없이 타일만 호버
        public TileData            PinnedTile  { get; private set; }   // 클릭 고정된 타일

        /// <summary>패널이 렌더링해야 할 현재 타일 (유닛이 없을 때만 의미. 호버 우선, 없으면 핀).</summary>
        public TileData CurrentTile
        {
            get
            {
                if (PinnedTile == null) return HoveredTile;   // 파괴 체크 겸 (UnityEngine null)
                return HoveredTile != null ? HoveredTile : PinnedTile;
            }
        }

        /// <summary>패널이 렌더링해야 할 현재 유닛 (UI 지정 > 월드 호버 > 핀).</summary>
        public IBattleInfoProvider CurrentUnit
        {
            get
            {
                // 파괴된 참조 정리 (UnityEngine.Object 널 체크)
                if (_externalHover is Component ec && ec == null) _externalHover = null;
                if (PinnedUnit is Component pc && pc == null) PinnedUnit = null;

                if (_externalHover != null) return _externalHover;
                if (HoveredUnit != null) return HoveredUnit;
                return PinnedUnit;
            }
        }

        /// <summary>UI 요소(파티 로스터 등)가 특정 유닛을 패널에 임시 표시하도록 지정.</summary>
        public void SetExternalHover(IBattleInfoProvider unit) => _externalHover = unit;

        /// <summary>지정했던 UI 호버를 해제 (다른 UI가 이미 덮어썼으면 무시).</summary>
        public void ClearExternalHover(IBattleInfoProvider unit)
        {
            if (ReferenceEquals(_externalHover, unit)) _externalHover = null;
        }

        private void Update()
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            UpdateHover(overUI);
            UpdatePin(overUI);
            UpdateHoverTooltip(overUI);
        }

        // ── 커서 옆 호버 툴팁 ────────────────────────────────────────
        // 몸체 호버 → 패시브(+HP/상태이상), 의도 버블 호버 → 다음 행동.
        // 타게팅 중엔 끔 (SkillTargetSelector의 예상 피해/선택 카운터가 툴팁 소유).

        private object _lastTooltipTarget;
        private bool _lastTooltipIntent;
        private float _nextTooltipRefresh;
        private const float TooltipRefreshInterval = 0.5f;

        private void UpdateHoverTooltip(bool overUI)
        {
            bool targeting = SkillTargetSelector.Instance != null && SkillTargetSelector.Instance.IsSelectingTarget;

            // 우선순위 1: 몬스터 위 의도 버블에 커서가 있으면 → 다음 행동
            Monster intentMonster = (targeting || overUI) ? null : FindIntentBubbleMonsterUnderCursor();

            object target;
            bool intentMode;
            if (intentMonster != null) { target = intentMonster; intentMode = true; }
            else if (targeting)        { target = null;          intentMode = false; }  // 타게팅 중엔 몸체 툴팁 끔
            else                       { target = HoveredUnit;   intentMode = false; }

            bool changed = !ReferenceEquals(target, _lastTooltipTarget) || intentMode != _lastTooltipIntent;
            if (!changed && (target == null || Time.unscaledTime < _nextTooltipRefresh)) return;

            _lastTooltipTarget = target;
            _lastTooltipIntent = intentMode;
            _nextTooltipRefresh = Time.unscaledTime + TooltipRefreshInterval;

            if (target == null)
            {
                if (changed) HoverTooltipUI.Instance?.HidePinned();
                return;
            }

            string text = intentMode
                ? UnitInfoBuilder.BuildNextActionTooltip((Monster)target)
                : target switch
                {
                    Character ch => UnitInfoBuilder.BuildPassiveTooltip(ch),
                    Monster m    => UnitInfoBuilder.BuildPassiveTooltip(m),
                    _            => null,
                };

            if (string.IsNullOrEmpty(text))
            {
                HoverTooltipUI.Instance?.HidePinned();
                return;
            }

            HoverTooltipUI.EnsureInstance();
            HoverTooltipUI.Instance?.ShowPinned(text);
        }

        /// <summary>커서가 어느 몬스터의 의도 버블(월드 UI) 위에 있으면 그 몬스터 반환. 없으면 null.</summary>
        private Monster FindIntentBubbleMonsterUnderCursor()
        {
            if (Mouse.current == null) return null;
            var cm = CombatManager.Instance;
            if (cm == null || cm.ActiveMonsters == null) return null;

            Vector2 sp = Mouse.current.position.ReadValue();
            foreach (var m in cm.ActiveMonsters)
            {
                if (m == null || !m.IsAlive) continue;
                var ui = m.GetComponentInChildren<MonsterUI>();
                if (ui == null || !ui.IsIntentBubbleActive) continue;
                var rect = ui.IntentBubbleRect;
                if (rect == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, sp, ui.IntentWorldCamera))
                    return m;
            }
            return null;
        }

        private void UpdateHover(bool overUI)
        {
            HoveredUnit = null;
            HoveredTile = null;
            if (overUI) return;

            var cam = GetCamera();
            if (cam == null || Mouse.current == null) return;

            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                HoveredUnit = hit.collider.GetComponentInParent<IBattleInfoProvider>();
                if (HoveredUnit == null)
                    HoveredTile = hit.collider.GetComponentInParent<TileData>();
            }

            // 몸체를 못 맞혔으면 몬스터 의도 버블(공격 아이콘) 위인지 확인 → 그 몬스터를 호버 대상으로.
            // 이러면 아이콘 호버 시에도 CurrentUnit=몬스터가 되어, SyncWorldIndicators가
            // 그 몬스터의 공격 예정 타일 '둘레 외곽선'(MonsterThreatOutline)을 띄운다(몸체 호버와 동일).
            if (HoveredUnit == null)
            {
                var intentMonster = FindIntentBubbleMonsterUnderCursor();
                if (intentMonster != null)
                {
                    HoveredUnit = intentMonster;
                    HoveredTile = null;
                }
            }
        }

        private void UpdatePin(bool overUI)
        {
            if (Mouse.current == null) return;

            // 타게팅 모드: 클릭은 SkillTargetSelector 소유 — 핀 조작 금지 (스펙 §6)
            bool targeting = SkillTargetSelector.Instance != null && SkillTargetSelector.Instance.IsSelectingTarget;
            if (targeting) return;

            if (Mouse.current.leftButton.wasPressedThisFrame && !overUI)
            {
                if (HoveredUnit != null)
                {
                    // 같은 대상 재클릭 = 해제, 다른 대상 = 새로 고정 (유닛 핀이 타일 핀보다 우선)
                    PinnedUnit = ReferenceEquals(PinnedUnit, HoveredUnit) ? null : HoveredUnit;
                    PinnedTile = null;
                }
                else if (HoveredTile != null)
                {
                    // 타일 클릭 고정 (재클릭 = 해제)
                    PinnedTile = ReferenceEquals(PinnedTile, HoveredTile) ? null : HoveredTile;
                    PinnedUnit = null;
                }
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                PinnedUnit = null;
                PinnedTile = null;
            }
        }

        private Camera GetCamera()
        {
            if (_cachedCamera != null && _cachedCamera.isActiveAndEnabled) return _cachedCamera;
            _cachedCamera = Camera.main;
            if (_cachedCamera == null) _cachedCamera = FindFirstObjectByType<Camera>();
            return _cachedCamera;
        }
    }
}
