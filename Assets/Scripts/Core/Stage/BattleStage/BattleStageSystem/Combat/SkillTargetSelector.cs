using UnityEngine;
using UnityEngine.InputSystem;
using DiceOrbit.Data;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;
using DiceOrbit.UI;
using DiceOrbit.Visuals;

namespace DiceOrbit.Core
{
    public class SkillTargetSelector : MonoBehaviour
    {
        public static SkillTargetSelector Instance { get; private set; }

        [Header("Visual")]
        [SerializeField] private LineRenderer targetLine;
        [SerializeField] private Color validTargetColor   = Color.green;
        [SerializeField] private Color invalidTargetColor = Color.red;
        [SerializeField] private Color confirmedLineColor = new Color(0.3f, 0.8f, 1f, 1f);
        [SerializeField] private float lineWidth = 0.1f;

        // 선택 상태
        private bool            isSelectingTarget;
        private Character       sourceCharacter;
        private ActiveSkillSlot currentSlot;
        private DiceData        currentDice;
        private Camera          mainCamera;
        private OrbitManager    _orbitManager;

        // 멀티 선택 누적
        private int                      _requiredCount;
        private readonly List<LineRenderer> _confirmedLines     = new();
        private readonly List<Vector3>      _confirmedPositions = new();
        private readonly List<Unit>         _pendingUnits       = new();
        private readonly List<TileData>     _pendingTiles       = new();

        // 타일 프리뷰
        private TileData _lastPreviewTile;

        // 데미지 미리보기
        private Unit _currentPreviewTarget;

        public bool IsSelectingTarget => isSelectingTarget;

        // ── 초기화 ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            mainCamera    = Camera.main;
            _orbitManager = FindFirstObjectByType<OrbitManager>();

            if (targetLine == null) targetLine = gameObject.AddComponent<LineRenderer>();
            ConfigureLine(targetLine);
            targetLine.enabled = false;
        }

        // ── 매 프레임 ─────────────────────────────────────────────────────

        private void Update()
        {
            if (!isSelectingTarget) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            UpdateCursorLine();
            UpdateDamagePreview();
            UpdateTilePreview();

            if (mouse.leftButton.wasPressedThisFrame)  TryAddSelection();
            if (mouse.rightButton.wasPressedThisFrame) HandleRightClick();
        }

        // ── 공개 API ──────────────────────────────────────────────────────

        public void StartTargetSelection(Character character, ActiveSkillSlot slot, DiceData dice)
        {
            sourceCharacter   = character;
            currentSlot       = slot;
            currentDice       = dice;
            isSelectingTarget = true;

            _requiredCount = ResolveRequiredCount(slot);
            _confirmedPositions.Clear();
            _confirmedPositions.Add(character.transform.position);
            _pendingUnits.Clear();
            _pendingTiles.Clear();

            if (targetLine == null) targetLine = gameObject.AddComponent<LineRenderer>();
            targetLine.enabled = true;

            sourceCharacter?.OnSkillTargetingStarted();

            // AllTiles는 즉시 전체 타일 프리뷰
            if (slot.TargetType == CharacterSkillTargetType.AllTiles)
            {
                TileSkillPreviewManager.EnsureInstance();
                if (_orbitManager != null)
                    TileSkillPreviewManager.Instance?.ShowPreview(_orbitManager.Tiles, slot.PreviewStyle);
            }

            RefreshProgressTooltip();
        }

        public void CancelTargetSelection()
        {
            if (currentDice != null)
            {
                currentDice.State = DiceState.Available;
                DiceUI.Instance?.RefreshDiceVisual(currentDice);
            }
            sourceCharacter?.OnSkillResolved();
            EndTargetSelection();
        }

        // ── 선택 처리 ─────────────────────────────────────────────────────

        private void TryAddSelection()
        {
            var targetType = currentSlot.TargetType;

            // AllTiles: 클릭 즉시 확정
            if (targetType == CharacterSkillTargetType.AllTiles)
            {
                ConfirmAndExecute();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;

            if (IsTileTargetType(targetType))
            {
                var tile = hit.collider.GetComponentInParent<TileData>();
                if (tile != null) AddTileSelection(tile);
                return;
            }

            if (!IsValidTarget(hit.collider.gameObject)) return;
            var unit = hit.collider.GetComponentInParent<Unit>();
            if (unit != null && unit.IsAlive) AddUnitSelection(unit);
        }

        private void HandleRightClick()
        {
            if (_pendingUnits.Count > 0 || _pendingTiles.Count > 0)
                UndoLastSelection();
            else
                CancelTargetSelection();
        }

        private void AddUnitSelection(Unit unit)
        {
            if (_pendingUnits.Contains(unit)) return;
            CreateConfirmedLine(_confirmedPositions[0], unit.transform.position);
            _pendingUnits.Add(unit);
            RefreshProgressTooltip();

            if (_pendingUnits.Count >= _requiredCount)
                ConfirmAndExecute();
        }

        private void AddTileSelection(TileData tile)
        {
            if (_pendingTiles.Contains(tile)) return;
            CreateConfirmedLine(_confirmedPositions[0], tile.transform.position);
            _pendingTiles.Add(tile);
            RefreshProgressTooltip();

            if (_pendingTiles.Count >= _requiredCount)
                ConfirmAndExecute();
        }

        private void UndoLastSelection()
        {
            if (_confirmedLines.Count > 0)
            {
                Destroy(_confirmedLines[_confirmedLines.Count - 1].gameObject);
                _confirmedLines.RemoveAt(_confirmedLines.Count - 1);
            }

            if (_pendingUnits.Count > 0)
                _pendingUnits.RemoveAt(_pendingUnits.Count - 1);
            else if (_pendingTiles.Count > 0)
                _pendingTiles.RemoveAt(_pendingTiles.Count - 1);

            RefreshProgressTooltip();
        }

        private void ConfirmAndExecute()
        {
            var units = new List<Unit>(_pendingUnits);
            var tiles = new List<TileData>(_pendingTiles);

            if (currentSlot.TargetType == CharacterSkillTargetType.AllTiles && _orbitManager != null)
                tiles = new List<TileData>(_orbitManager.Tiles);

            SkillManager.Instance.ConfirmSkillExecution(sourceCharacter, currentSlot, currentDice, units, tiles);
            EndTargetSelection();
        }

        // ── 커서 선 ──────────────────────────────────────────────────────

        private void UpdateCursorLine()
        {
            if (sourceCharacter == null || targetLine == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // 시작점 = 항상 캐릭터 위치 (방사형)
            Vector3 startPos = _confirmedPositions[0];
            targetLine.SetPosition(0, startPos);

            Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());
            bool    validTarget = false;
            Vector3 endPos      = startPos;

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                validTarget = IsValidTarget(hit.collider.gameObject);
                endPos      = validTarget ? hit.collider.GetComponentInParent<Transform>().position : hit.point;
            }
            else
            {
                var plane = new Plane(Vector3.up, sourceCharacter.transform.position);
                if (plane.Raycast(ray, out float dist)) endPos = ray.GetPoint(dist);
            }

            targetLine.SetPosition(1, endPos);
            var color = validTarget ? validTargetColor : invalidTargetColor;
            targetLine.startColor = color;
            targetLine.endColor   = color;
        }

        // ── 데미지 미리보기 ──────────────────────────────────────────────

        private void UpdateDamagePreview()
        {
            var t = currentSlot?.TargetType;
            if (t != CharacterSkillTargetType.OneEnemy && t != CharacterSkillTargetType.MultiEnemy)
            {
                HoverTooltipUI.Instance?.HidePinned();
                _currentPreviewTarget = null;
                return;
            }

            if (currentSlot?.BaseSkill == null || sourceCharacter == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit) || !IsValidTarget(hit.collider.gameObject))
            {
                HoverTooltipUI.Instance?.HidePinned();
                _currentPreviewTarget = null;
                return;
            }

            var targetUnit = hit.collider.GetComponentInParent<Unit>();
            if (targetUnit == null || !targetUnit.IsAlive)
            {
                HoverTooltipUI.Instance?.HidePinned();
                _currentPreviewTarget = null;
                return;
            }

            _currentPreviewTarget = targetUnit;
            var tmpl = currentSlot.RuntimeInstance;
            if (tmpl != null)
            {
                int raw = tmpl.CalculateRawDamage(sourceCharacter, currentSlot, currentDice.Value);
                HoverTooltipUI.EnsureInstance();
                HoverTooltipUI.Instance?.ShowPinned(raw > 0 ? $"예상 피해: {raw}" : "예상 피해: -");
            }
        }

        // ── 타일 프리뷰 ──────────────────────────────────────────────────

        private void UpdateTilePreview()
        {
            var t = currentSlot?.TargetType;
            if (t != CharacterSkillTargetType.OneTile && t != CharacterSkillTargetType.MultiTile) return;

            var tile = GetTileUnderMouse();
            if (tile == _lastPreviewTile) return;

            _lastPreviewTile = tile;
            TileSkillPreviewManager.EnsureInstance();

            if (tile != null)
                TileSkillPreviewManager.Instance?.ShowPreview(new[] { tile }, currentSlot.PreviewStyle);
            else
                TileSkillPreviewManager.Instance?.HidePreview();
        }

        private TileData GetTileUnderMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || mainCamera == null) return null;
            Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit)) return null;
            return hit.collider.GetComponentInParent<TileData>();
        }

        // ── 유효성 판정 ──────────────────────────────────────────────────

        private bool IsValidTarget(GameObject target)
        {
            return currentSlot.TargetType switch
            {
                CharacterSkillTargetType.OneEnemy   => target.GetComponentInParent<Monster>()   != null,
                CharacterSkillTargetType.MultiEnemy => target.GetComponentInParent<Monster>()   != null,
                CharacterSkillTargetType.OneAlly    => IsValidAlly(target),
                CharacterSkillTargetType.MultiAlly  => IsValidAlly(target),
                CharacterSkillTargetType.OneTile    => target.GetComponentInParent<TileData>()  != null,
                CharacterSkillTargetType.MultiTile  => target.GetComponentInParent<TileData>()  != null,
                CharacterSkillTargetType.AllTiles   => true,
                _                                   => false,
            };
        }

        private bool IsValidAlly(GameObject target)
        {
            var character = target.GetComponentInParent<Character>();
            return character != null && character != sourceCharacter && character.IsAlive;
        }

        private static bool IsTileTargetType(CharacterSkillTargetType t) =>
            t == CharacterSkillTargetType.OneTile  ||
            t == CharacterSkillTargetType.MultiTile ||
            t == CharacterSkillTargetType.AllTiles;

        // ── 진행상황 툴팁 ─────────────────────────────────────────────────

        private void RefreshProgressTooltip()
        {
            if (_requiredCount <= 1) return;

            int current = _pendingUnits.Count + _pendingTiles.Count;
            HoverTooltipUI.EnsureInstance();
            HoverTooltipUI.Instance?.ShowPinned($"{current} / {_requiredCount} 선택  (우클릭: 취소)");
        }

        private static int ResolveRequiredCount(ActiveSkillSlot slot)
        {
            return slot.TargetType switch
            {
                CharacterSkillTargetType.MultiEnemy => Mathf.Max(1, slot.TargetCount),
                CharacterSkillTargetType.MultiAlly  => Mathf.Max(1, slot.TargetCount),
                CharacterSkillTargetType.MultiTile  => Mathf.Max(1, slot.TargetCount),
                _                                   => 1,
            };
        }

        // ── 종료 & 정리 ──────────────────────────────────────────────────

        private void EndTargetSelection()
        {
            sourceCharacter?.OnSkillTargetingEnded();
            isSelectingTarget = false;

            if (targetLine != null) targetLine.enabled = false;

            foreach (var lr in _confirmedLines)
                if (lr != null) Destroy(lr.gameObject);
            _confirmedLines.Clear();
            _confirmedPositions.Clear();
            _pendingUnits.Clear();
            _pendingTiles.Clear();

            HoverTooltipUI.Instance?.HidePinned();
            TileSkillPreviewManager.Instance?.HidePreview();

            _currentPreviewTarget = null;
            _lastPreviewTile      = null;
            sourceCharacter       = null;
            currentSlot           = null;
            currentDice           = null;
        }

        // ── 라인 헬퍼 ─────────────────────────────────────────────────────

        private LineRenderer CreateConfirmedLine(Vector3 from, Vector3 to)
        {
            var go = new GameObject("_ConfirmedLine");
            var lr = go.AddComponent<LineRenderer>();
            ConfigureLine(lr);
            lr.startColor    = confirmedLineColor;
            lr.endColor      = confirmedLineColor;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            _confirmedLines.Add(lr);
            return lr;
        }

        private void ConfigureLine(LineRenderer lr)
        {
            lr.material      = new Material(Shader.Find("Sprites/Default"));
            lr.textureMode   = LineTextureMode.Tile;
            lr.startWidth    = lineWidth;
            lr.endWidth      = lineWidth;
            lr.positionCount = 2;
        }
    }
}
