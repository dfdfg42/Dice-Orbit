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
        }

        private void UpdateHover(bool overUI)
        {
            HoveredUnit = null;
            HoveredTile = null;
            if (overUI) return;

            var cam = GetCamera();
            if (cam == null || Mouse.current == null) return;

            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

            HoveredUnit = hit.collider.GetComponentInParent<IBattleInfoProvider>();
            if (HoveredUnit == null)
                HoveredTile = hit.collider.GetComponentInParent<TileData>();
        }

        private void UpdatePin(bool overUI)
        {
            if (Mouse.current == null) return;

            // 타게팅 모드: 클릭은 SkillTargetSelector 소유 — 핀 조작 금지 (스펙 §6)
            bool targeting = SkillTargetSelector.Instance != null && SkillTargetSelector.Instance.IsSelectingTarget;
            if (targeting) return;

            if (Mouse.current.leftButton.wasPressedThisFrame && !overUI && HoveredUnit != null)
            {
                // 같은 대상 재클릭 = 해제, 다른 대상 = 새로 고정
                PinnedUnit = ReferenceEquals(PinnedUnit, HoveredUnit) ? null : HoveredUnit;
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
                PinnedUnit = null;
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
