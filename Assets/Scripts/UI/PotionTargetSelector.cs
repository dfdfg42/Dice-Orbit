using UnityEngine;
using UnityEngine.InputSystem;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Visuals;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 대상 지정형 포션의 조준 모드 — HUD 포션 칩에서 캐릭터로 점선 아크를 끌어 선택.
    /// 스킬 조준과 같은 시각 언어 (DashedArcLine: 유효=초록, 무효=빨강 / 우클릭·ESC 취소).
    /// RunHudUI의 포션 클릭이 Begin()으로 진입시킨다.
    /// </summary>
    public class PotionTargetSelector : MonoBehaviour
    {
        public static PotionTargetSelector Instance { get; private set; }

        [Header("Visual")]
        [SerializeField] private Color validColor = Color.green;
        [SerializeField] private Color invalidColor = Color.red;
        [SerializeField] private float lineWidth = 0.1f;

        public bool IsSelecting { get; private set; }

        private int _potionIndex = -1;
        private Vector2 _chipScreenPos;
        private LineRenderer _arc;
        private LineRenderer _arrow;
        private Camera _cam;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _cam = Camera.main;
            _arc = DashedArcLine.CreateArc(transform, lineWidth);
            _arrow = DashedArcLine.CreateArrow(transform, lineWidth);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            Instance = FindFirstObjectByType<PotionTargetSelector>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                var go = new GameObject("PotionTargetSelector");
                Instance = go.AddComponent<PotionTargetSelector>();
            }
        }

        /// <summary>조준 모드 진입. chipScreenPos = 클릭한 포션 칩의 화면 좌표 (아크 시작점).</summary>
        public void Begin(int potionIndex, Vector2 chipScreenPos)
        {
            _potionIndex = potionIndex;
            _chipScreenPos = chipScreenPos;
            IsSelecting = true;
            if (_cam == null) _cam = Camera.main;

            DashedArcLine.SetVisible(_arc, _arrow, true);
            HoverTooltipUI.EnsureInstance();
            HoverTooltipUI.Instance?.ShowPinned("<b>대상 선택</b>\n회복할 아군을 클릭하세요 <size=80%><color=#8B8B8B>(우클릭 취소)</color></size>");
        }

        public void End()
        {
            IsSelecting = false;
            _potionIndex = -1;
            DashedArcLine.SetVisible(_arc, _arrow, false);
            HoverTooltipUI.Instance?.HidePinned();
        }

        private void Update()
        {
            if (!IsSelecting) return;

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            // 취소: 우클릭 / ESC
            if ((mouse != null && mouse.rightButton.wasPressedThisFrame) ||
                (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
            {
                End();
                return;
            }

            if (mouse == null || _cam == null) return;

            Vector2 mousePos = mouse.position.ReadValue();
            Vector3 start = ScreenToGround(_chipScreenPos) + Vector3.up * 0.2f;

            // 커서 아래 아군 탐색
            Character hovered = null;
            Vector3 end;
            Ray ray = _cam.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                hovered = hit.collider.GetComponentInParent<Character>();
                if (hovered != null && !hovered.IsAlive) hovered = null;
                end = hovered != null ? hovered.transform.position + Vector3.up * 0.3f : hit.point;
            }
            else
            {
                end = ScreenToGround(mousePos);
            }

            DashedArcLine.SetArcWithArrow(_arc, _arrow, start, end, hovered != null ? validColor : invalidColor);

            // 확정: 아군 위에서 좌클릭
            if (hovered != null && mouse.leftButton.wasPressedThisFrame)
            {
                PotionManager.Instance?.TryUseOn(_potionIndex, hovered);
                End();
            }
        }

        /// <summary>화면 좌표 → 지면(y=0) 월드 좌표 (HUD 칩의 월드 기준점).</summary>
        private Vector3 ScreenToGround(Vector2 screenPos)
        {
            Ray ray = _cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist)) return ray.GetPoint(dist);
            return ray.GetPoint(10f);
        }
    }
}
