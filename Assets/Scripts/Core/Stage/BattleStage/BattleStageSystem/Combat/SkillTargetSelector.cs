using UnityEngine;
using UnityEngine.InputSystem;
using DiceOrbit.Data;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;
using DiceOrbit.UI;
using DiceOrbit.Visuals;

namespace DiceOrbit.Core
{
    /// <summary>
    /// ?¤í‚¬ ?€ê²?? íƒ ?œìŠ¤??
    /// </summary>
    public class SkillTargetSelector : MonoBehaviour
    {
        public static SkillTargetSelector Instance { get; private set; }
        
        [Header("Visual")]
        [SerializeField] private LineRenderer targetLine;
        [SerializeField] private Color validTargetColor = Color.green;
        [SerializeField] private Color invalidTargetColor = Color.red;
        [SerializeField] private float lineWidth = 0.1f;
        
        private bool isSelectingTarget = false;
        private Character sourceCharacter;
        private RuntimeAbility currentRuntimeAbility;
        private DiceData currentDice;
        private Camera mainCamera;
        private Unit currentPreviewTarget;
        private TileData _lastPreviewTile;
        private OrbitManager _orbitManager;

        // Properties
        public bool IsSelectingTarget => isSelectingTarget;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            mainCamera = Camera.main;
            _orbitManager = FindFirstObjectByType<OrbitManager>();

            // LineRenderer ?¤ì •
            if (targetLine == null)
            {
                targetLine = gameObject.AddComponent<LineRenderer>();
            }

            targetLine.startWidth = lineWidth;
            targetLine.endWidth = lineWidth;
            targetLine.positionCount = 2;
            targetLine.enabled = false;

            // ?ì„  ?¨ê³¼
            targetLine.material = new Material(Shader.Find("Sprites/Default"));
            targetLine.textureMode = LineTextureMode.Tile;
        }

        private void Update()
        {
            if (!isSelectingTarget) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // ë§ˆìš°???„ì¹˜ë¡??¼ì¸ ?…ë°?´íŠ¸
            UpdateTargetLine();
            UpdateDamagePreview();
            UpdateTilePreview();

            // ë§ˆìš°???´ë¦­?¼ë¡œ ?€ê²?? íƒ
            if (mouse.leftButton.wasPressedThisFrame)
            {
                TrySelectTarget();
            }

            // ?°í´ë¦?œ¼ë¡?ì·¨ì†Œ
            if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelTargetSelection();
            }
        }

        /// <summary>
        /// ?€ê²?? íƒ ëª¨ë“œ ?œì‘
        /// </summary>
        public void StartTargetSelection(Character character, RuntimeAbility runtimeAbility, DiceData dice)
        {
            sourceCharacter = character;
            currentRuntimeAbility = runtimeAbility;
            currentDice = dice;
            isSelectingTarget = true;
            sourceCharacter?.OnSkillTargetingStarted();

            // LineRenderer ?•ì¸ ë°??œì„±??
            if (targetLine == null)
            {
                targetLine = GetComponent<LineRenderer>();
                if (targetLine == null)
                {
                    targetLine = gameObject.AddComponent<LineRenderer>();
                    targetLine.startWidth = lineWidth;
                    targetLine.endWidth = lineWidth;
                    targetLine.positionCount = 2;
                    targetLine.material = new Material(Shader.Find("Sprites/Default"));
                }
            }

            targetLine.enabled = true;

            var skillName = currentRuntimeAbility?.BaseSkill?.SkillName ?? "Unknown";
            Debug.Log($"Target selection started for {skillName} (Type: {currentRuntimeAbility.TargetType})");

            // AllTiles: ? íƒ ?œì‘ê³??™ì‹œ??ëª¨ë“  ?€?¼ì— ?„ë¦¬ë·??œì‹œ
            if (currentRuntimeAbility.TargetType == CharacterSkillTargetType.AllTiles)
            {
                TileSkillPreviewManager.EnsureInstance();
                var previewStyle = currentRuntimeAbility.BaseSkill?.PreviewStyle ?? TilePreviewStyle.Neutral;
                if (_orbitManager != null)
                    TileSkillPreviewManager.Instance?.ShowPreview(_orbitManager.Tiles, previewStyle);
            }
        }

        /// <summary>
        /// ?€ê²??¼ì¸ ?…ë°?´íŠ¸
        /// </summary>
        private void UpdateTargetLine()
        {
            if (sourceCharacter == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // ?œì‘?? ìºë¦­???„ì¹˜
            Vector3 startPos = sourceCharacter.transform.position;
            targetLine.SetPosition(0, startPos);

            // ë§ˆìš°???„ë˜ ?¤ë¸Œ?íŠ¸ ?•ì¸
            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            RaycastHit hit;

            bool validTarget = false;
            Vector3 endPos = startPos;

            // Raycastë¡??€ê²?ì°¾ê¸°
            if (Physics.Raycast(ray, out hit))
            {
                GameObject targetObj = hit.collider.gameObject;
                validTarget = IsValidTarget(targetObj);

                // ?€ê²Ÿì´ ? íš¨?˜ë©´ ?€ê²??„ì¹˜, ?„ë‹ˆë©??ˆíŠ¸ ?„ì¹˜
                if (validTarget)
                {
                    // ëª¬ìŠ¤?°ë‚˜ ìºë¦­?°ì˜ ì¤‘ì‹¬?¼ë¡œ
                    endPos = targetObj.transform.position;
                }
                else
                {
                    // ?ˆíŠ¸???„ì¹˜ë¡?
                    endPos = hit.point;
                }
            }
            else
            {
                // ?ˆíŠ¸ ?¤íŒ¨ ???‰ë©´?ì˜ ë§ˆìš°???„ì¹˜
                Plane plane = new Plane(Vector3.up, sourceCharacter.transform.position);
                if (plane.Raycast(ray, out float distance))
                {
                    endPos = ray.GetPoint(distance);
                }
            }

            targetLine.SetPosition(1, endPos);

            // ?‰ìƒ ?…ë°?´íŠ¸
            Color lineColor = validTarget ? validTargetColor : invalidTargetColor;
            targetLine.startColor = lineColor;
            targetLine.endColor = lineColor;
        }

        private void UpdateDamagePreview()
        {
            if (currentRuntimeAbility?.BaseSkill == null || sourceCharacter == null)
            {
                HoverTooltipUI.Instance?.HidePinned();
                currentPreviewTarget = null;
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                HoverTooltipUI.Instance?.HidePinned();
                currentPreviewTarget = null;
                return;
            }

            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                HoverTooltipUI.Instance?.HidePinned();
                currentPreviewTarget = null;
                return;
            }

            if (!IsValidTarget(hit.collider.gameObject))
            {
                HoverTooltipUI.Instance?.HidePinned();
                currentPreviewTarget = null;
                return;
            }

            var targetUnit = hit.collider.GetComponentInParent<Unit>();
            if (targetUnit == null || !targetUnit.IsAlive)
            {
                HoverTooltipUI.Instance?.HidePinned();
                currentPreviewTarget = null;
                return;
            }

            currentPreviewTarget = targetUnit;
            string text = BuildAppliedDamagePreview(targetUnit);
            HoverTooltipUI.EnsureInstance();
            HoverTooltipUI.Instance?.ShowPinned(text);
        }

        private string BuildAppliedDamagePreview(Unit targetUnit)
        {
            if (currentRuntimeAbility?.BaseSkill == null || targetUnit == null || sourceCharacter == null)
                return "?ˆìƒ ?¼í•´: -";

            var activeTemplate = currentRuntimeAbility.BaseSkill.ActiveTemplate;
            if (activeTemplate != null)
            {
                int coupledRaw = activeTemplate.CalculateRawDamage(sourceCharacter, currentRuntimeAbility, currentDice.Value);
                return coupledRaw > 0 ? $"?ˆìƒ ?¼í•´: {coupledRaw}" : "?ˆìƒ ?¼í•´: -";
            }

            return "?ˆìƒ ?¼í•´: -";
        }

        /// <summary>
        /// ?€ê²?? íƒ ?œë„
        /// </summary>
        private void TrySelectTarget()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var targetType = currentRuntimeAbility.TargetType;

            // AllTiles: ?´ë””???´ë¦­?˜ë©´ ëª¨ë“  ?€?¼ë¡œ ?•ì •
            if (targetType == CharacterSkillTargetType.AllTiles)
            {
                NotifyTileTargetSelected(null);
                EndTargetSelection();
                return;
            }

            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            if (!Physics.Raycast(ray, out RaycastHit hit))
                return;

            // OneTile: TileData ?„ë? ?´ë¦­?ˆì„ ?Œë§Œ ?•ì •
            if (targetType == CharacterSkillTargetType.OneTile)
            {
                var tile = hit.collider.GetComponentInParent<TileData>();
                if (tile != null)
                {
                    NotifyTileTargetSelected(tile);
                    EndTargetSelection();
                }
                return;
            }

            // ? ë‹› ?€ê²ŸíŒ… (ê¸°ì¡´ ë¡œì§)
            GameObject targetObj = hit.collider.gameObject;
            if (IsValidTarget(targetObj))
            {
                NotifyTargetSelected(targetObj);
                EndTargetSelection();
            }
            else
            {
                Debug.LogWarning("Invalid target for this skill!");
            }
        }

        /// <summary>
        /// ? íš¨???€ê²Ÿì¸ì§€ ?•ì¸
        /// </summary>
        private bool IsValidTarget(GameObject target)
        {
            switch (currentRuntimeAbility.TargetType)
            {
                case CharacterSkillTargetType.OneEnemy:
                    return target.GetComponentInParent<Monster>() != null;
                case CharacterSkillTargetType.OneTile:
                    return target.GetComponentInParent<TileData>() != null;
                case CharacterSkillTargetType.AllTiles:
                    return true; // ?´ë””???´ë¦­?˜ë©´ ?•ì •
                case CharacterSkillTargetType.None:
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// ?€ê²?? íƒ ?„ë£Œ -> CharacterActionUIë¡?ì½œë°±
        /// </summary>
        private void NotifyTargetSelected(GameObject target)
        {
            var resolved = ResolveTarget(target);
            if (resolved == null)
            {
                CancelTargetSelection();
                return;
            }

            // CharacterActionUI???€ê²Ÿì´ ?•ì •?˜ì—ˆ?Œì„ ?Œë¦¼
            CharacterActionUI.Instance?.ConfirmSkillTarget(resolved, sourceCharacter, currentRuntimeAbility, currentDice);
        }

        private Unit ResolveTarget(GameObject target)
        {
            var unit = target.GetComponentInParent<Unit>();
            if (unit == null)
            {
                Debug.LogError("Selected target does not have a Unit component!");
                return null;
            }
            else return unit;
        }

        /// <summary>
        /// ?€ê²?? íƒ ì·¨ì†Œ
        /// </summary>
        public void CancelTargetSelection()
        {
            // ?ˆì•½ ?íƒœ?€??ì£¼ì‚¬?„ë? ?¤ì‹œ ?¬ìš© ê°€?¥í•˜ê²??˜ëŒë¦?
            if (currentDice != null)
            {
                currentDice.State = DiceState.Available;
                DiceUI.Instance?.RefreshDiceVisual(currentDice);
            }

            sourceCharacter?.OnSkillResolved();
            EndTargetSelection();
            Debug.Log("Target selection cancelled");
        }

        /// <summary>
        /// ?€ê²?? íƒ ì¢…ë£Œ
        /// </summary>
        private void EndTargetSelection()
        {
            sourceCharacter?.OnSkillTargetingEnded();
            isSelectingTarget = false;

            if (targetLine != null)
                targetLine.enabled = false;

            HoverTooltipUI.Instance?.HidePinned();
            TileSkillPreviewManager.Instance?.HidePreview();

            currentPreviewTarget = null;
            _lastPreviewTile     = null;
            sourceCharacter      = null;
            currentRuntimeAbility = null;
            currentDice          = null;
        }

        // ?€?€ ?€???€ê²ŸíŒ… ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€

        /// <summary>
        /// OneTile: ë§ˆìš°???„ë˜ ?€?¼ì´ ë°”ë€??Œë§Œ ?„ë¦¬ë·°ë? ê°±ì‹ ?©ë‹ˆ??
        /// </summary>
        private void UpdateTilePreview()
        {
            if (currentRuntimeAbility?.TargetType != CharacterSkillTargetType.OneTile) return;

            var tile = GetTileUnderMouse();
            if (tile == _lastPreviewTile) return;

            _lastPreviewTile = tile;
            TileSkillPreviewManager.EnsureInstance();
            var style = currentRuntimeAbility.BaseSkill?.PreviewStyle ?? TilePreviewStyle.Neutral;

            if (tile != null)
                TileSkillPreviewManager.Instance?.ShowPreview(new[] { tile }, style);
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

        private void NotifyTileTargetSelected(TileData singleTile)
        {
            List<TileData> targets;

            if (currentRuntimeAbility.TargetType == CharacterSkillTargetType.AllTiles)
                targets = _orbitManager != null ? new List<TileData>(_orbitManager.Tiles) : new List<TileData>();
            else
                targets = singleTile != null ? new List<TileData> { singleTile } : new List<TileData>();

            CharacterActionUI.Instance?.ConfirmTileSkillTarget(targets, sourceCharacter, currentRuntimeAbility, currentDice);
        }
    }
}
