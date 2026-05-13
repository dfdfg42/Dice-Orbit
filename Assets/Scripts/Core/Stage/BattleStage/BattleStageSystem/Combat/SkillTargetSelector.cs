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
    /// ?ㅽ궗 ?寃??좏깮 ?쒖뒪??
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
        private ActiveSkillSlot currentSlot;
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

            // LineRenderer ?ㅼ젙
            if (targetLine == null)
            {
                targetLine = gameObject.AddComponent<LineRenderer>();
            }

            targetLine.startWidth = lineWidth;
            targetLine.endWidth = lineWidth;
            targetLine.positionCount = 2;
            targetLine.enabled = false;

            // ?먯꽑 ?④낵
            targetLine.material = new Material(Shader.Find("Sprites/Default"));
            targetLine.textureMode = LineTextureMode.Tile;
        }

        private void Update()
        {
            if (!isSelectingTarget) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // 留덉슦???꾩튂濡??쇱씤 ?낅뜲?댄듃
            UpdateTargetLine();
            UpdateDamagePreview();
            UpdateTilePreview();

            // 留덉슦???대┃?쇰줈 ?寃??좏깮
            if (mouse.leftButton.wasPressedThisFrame)
            {
                TrySelectTarget();
            }

            // ?고겢由?쑝濡?痍⑥냼
            if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelTargetSelection();
            }
        }

        /// <summary>
        /// ?寃??좏깮 紐⑤뱶 ?쒖옉
        /// </summary>
        public void StartTargetSelection(Character character, ActiveSkillSlot runtimeAbility, DiceData dice)
        {
            sourceCharacter = character;
            currentSlot = runtimeAbility;
            currentDice = dice;
            isSelectingTarget = true;
            sourceCharacter?.OnSkillTargetingStarted();

            // LineRenderer ?뺤씤 諛??쒖꽦??
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

            var skillName = currentSlot?.BaseSkill?.SkillName ?? "Unknown";
            Debug.Log($"Target selection started for {skillName} (Type: {currentSlot.TargetType})");

            // AllTiles: ?좏깮 ?쒖옉怨??숈떆??紐⑤뱺 ??쇱뿉 ?꾨━酉??쒖떆
            if (currentSlot.TargetType == CharacterSkillTargetType.AllTiles)
            {
                TileSkillPreviewManager.EnsureInstance();
                var previewStyle = currentSlot.PreviewStyle;
                if (_orbitManager != null)
                    TileSkillPreviewManager.Instance?.ShowPreview(_orbitManager.Tiles, previewStyle);
            }
        }

        /// <summary>
        /// ?寃??쇱씤 ?낅뜲?댄듃
        /// </summary>
        private void UpdateTargetLine()
        {
            if (sourceCharacter == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // ?쒖옉?? 罹먮┃???꾩튂
            Vector3 startPos = sourceCharacter.transform.position;
            targetLine.SetPosition(0, startPos);

            // 留덉슦???꾨옒 ?ㅻ툕?앺듃 ?뺤씤
            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            RaycastHit hit;

            bool validTarget = false;
            Vector3 endPos = startPos;

            // Raycast濡??寃?李얘린
            if (Physics.Raycast(ray, out hit))
            {
                GameObject targetObj = hit.collider.gameObject;
                validTarget = IsValidTarget(targetObj);

                // ?寃잛씠 ?좏슚?섎㈃ ?寃??꾩튂, ?꾨땲硫??덊듃 ?꾩튂
                if (validTarget)
                {
                    // 紐ъ뒪?곕굹 罹먮┃?곗쓽 以묒떖?쇰줈
                    endPos = targetObj.transform.position;
                }
                else
                {
                    // ?덊듃???꾩튂濡?
                    endPos = hit.point;
                }
            }
            else
            {
                // ?덊듃 ?ㅽ뙣 ???됰㈃?곸쓽 留덉슦???꾩튂
                Plane plane = new Plane(Vector3.up, sourceCharacter.transform.position);
                if (plane.Raycast(ray, out float distance))
                {
                    endPos = ray.GetPoint(distance);
                }
            }

            targetLine.SetPosition(1, endPos);

            // ?됱긽 ?낅뜲?댄듃
            Color lineColor = validTarget ? validTargetColor : invalidTargetColor;
            targetLine.startColor = lineColor;
            targetLine.endColor = lineColor;
        }

        private void UpdateDamagePreview()
        {
            if (currentSlot?.BaseSkill == null || sourceCharacter == null)
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
            if (currentSlot?.BaseSkill == null || targetUnit == null || sourceCharacter == null)
                return "?덉긽 ?쇳빐: -";

            var activeTemplate = currentSlot.RuntimeInstance;
            if (activeTemplate != null)
            {
                int coupledRaw = activeTemplate.CalculateRawDamage(sourceCharacter, currentSlot, currentDice.Value);
                return coupledRaw > 0 ? $"?덉긽 ?쇳빐: {coupledRaw}" : "?덉긽 ?쇳빐: -";
            }

            return "?덉긽 ?쇳빐: -";
        }

        /// <summary>
        /// ?寃??좏깮 ?쒕룄
        /// </summary>
        private void TrySelectTarget()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var targetType = currentSlot.TargetType;

            // AllTiles: ?대뵒???대┃?섎㈃ 紐⑤뱺 ??쇰줈 ?뺤젙
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

            // OneTile: TileData ?꾨? ?대┃?덉쓣 ?뚮쭔 ?뺤젙
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

            // ?좊떅 ?寃잜똿 (湲곗〈 濡쒖쭅)
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
        /// ?좏슚???寃잛씤吏 ?뺤씤
        /// </summary>
        private bool IsValidTarget(GameObject target)
        {
            switch (currentSlot.TargetType)
            {
                case CharacterSkillTargetType.OneEnemy:
                    return target.GetComponentInParent<Monster>() != null;
                case CharacterSkillTargetType.OneTile:
                    return target.GetComponentInParent<TileData>() != null;
                case CharacterSkillTargetType.AllTiles:
                    return true; // ?대뵒???대┃?섎㈃ ?뺤젙
                case CharacterSkillTargetType.None:
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// ?寃??좏깮 ?꾨즺 -> CharacterActionUI濡?肄쒕갚
        /// </summary>
        private void NotifyTargetSelected(GameObject target)
        {
            var resolved = ResolveTarget(target);
            if (resolved == null)
            {
                CancelTargetSelection();
                return;
            }

            // CharacterActionUI???寃잛씠 ?뺤젙?섏뿀?뚯쓣 ?뚮┝
            SkillManager.Instance.ConfirmSkillExecution(sourceCharacter, currentSlot, currentDice, new System.Collections.Generic.List<Unit>{resolved}, new System.Collections.Generic.List<TileData>());
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
        /// ?寃??좏깮 痍⑥냼
        /// </summary>
        public void CancelTargetSelection()
        {
            // ?덉빟 ?곹깭???二쇱궗?꾨? ?ㅼ떆 ?ъ슜 媛?ν븯寃??섎룎由?
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
        /// ?寃??좏깮 醫낅즺
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
            currentSlot = null;
            currentDice          = null;
        }

        // ?? ????寃잜똿 ???????????????????????????????????????????????

        /// <summary>
        /// OneTile: 留덉슦???꾨옒 ??쇱씠 諛붾??뚮쭔 ?꾨━酉곕? 媛깆떊?⑸땲??
        /// </summary>
        private void UpdateTilePreview()
        {
            if (currentSlot?.TargetType != CharacterSkillTargetType.OneTile) return;

            var tile = GetTileUnderMouse();
            if (tile == _lastPreviewTile) return;

            _lastPreviewTile = tile;
            TileSkillPreviewManager.EnsureInstance();
            var style = currentSlot.PreviewStyle;

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

            if (currentSlot.TargetType == CharacterSkillTargetType.AllTiles)
                targets = _orbitManager != null ? new List<TileData>(_orbitManager.Tiles) : new List<TileData>();
            else
                targets = singleTile != null ? new List<TileData> { singleTile } : new List<TileData>();

            SkillManager.Instance.ConfirmSkillExecution(sourceCharacter, currentSlot, currentDice, new System.Collections.Generic.List<Unit>(), targets);
        }
    }
}
