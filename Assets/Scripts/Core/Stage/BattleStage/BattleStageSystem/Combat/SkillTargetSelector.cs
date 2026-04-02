using UnityEngine;
using UnityEngine.InputSystem;
using DiceOrbit.Data;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;
using DiceOrbit.UI;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 스킬 타겟 선택 시스템
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
        private int diceValue;
        private Camera mainCamera;
        private Unit currentPreviewTarget;
        
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
            
            // LineRenderer 설정
            if (targetLine == null)
            {
                targetLine = gameObject.AddComponent<LineRenderer>();
            }
            
            targetLine.startWidth = lineWidth;
            targetLine.endWidth = lineWidth;
            targetLine.positionCount = 2;
            targetLine.enabled = false;
            
            // 점선 효과
            targetLine.material = new Material(Shader.Find("Sprites/Default"));
            targetLine.textureMode = LineTextureMode.Tile;
        }
        
        private void Update()
        {
            if (!isSelectingTarget) return;
            
            var mouse = Mouse.current;
            if (mouse == null) return;
            
            // 마우스 위치로 라인 업데이트
            UpdateTargetLine();
            UpdateDamagePreview();
            
            // 마우스 클릭으로 타겟 선택
            if (mouse.leftButton.wasPressedThisFrame)
            {
                TrySelectTarget();
            }
            
            // 우클릭으로 취소
            if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelTargetSelection();
            }
        }
        
        /// <summary>
        /// 타겟 선택 모드 시작
        /// </summary>
        public void StartTargetSelection(Character character, RuntimeAbility runtimeAbility, int dice)
        {
            sourceCharacter = character;
            currentRuntimeAbility = runtimeAbility;
            diceValue = dice;
            isSelectingTarget = true;
            sourceCharacter?.OnSkillTargetingStarted();
            
            // LineRenderer 확인 및 활성화
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
            
            var skillData = currentRuntimeAbility?.CurrentSkillData;
            Debug.Log($"Target selection started for {skillData?.SkillName ?? "Unknown"} (Type: {skillData?.skillTargetType})");
        }
        
        /// <summary>
        /// 타겟 라인 업데이트
        /// </summary>
        private void UpdateTargetLine()
        {
            if (sourceCharacter == null) return;
            
            var mouse = Mouse.current;
            if (mouse == null) return;
            
            // 시작점: 캐릭터 위치
            Vector3 startPos = sourceCharacter.transform.position;
            targetLine.SetPosition(0, startPos);
            
            // 마우스 아래 오브젝트 확인
            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            RaycastHit hit;
            
            bool validTarget = false;
            Vector3 endPos = startPos;
            
            // Raycast로 타겟 찾기
            if (Physics.Raycast(ray, out hit))
            {
                GameObject targetObj = hit.collider.gameObject;
                validTarget = IsValidTarget(targetObj);
                
                // 타겟이 유효하면 타겟 위치, 아니면 히트 위치
                if (validTarget)
                {
                    // 몬스터나 캐릭터의 중심으로
                    endPos = targetObj.transform.position;
                }
                else
                {
                    // 히트한 위치로
                    endPos = hit.point;
                }
            }
            else
            {
                // 히트 실패 시 평면상의 마우스 위치
                Plane plane = new Plane(Vector3.up, sourceCharacter.transform.position);
                if (plane.Raycast(ray, out float distance))
                {
                    endPos = ray.GetPoint(distance);
                }
            }
            
            targetLine.SetPosition(1, endPos);
            
            // 색상 업데이트
            Color lineColor = validTarget ? validTargetColor : invalidTargetColor;
            targetLine.startColor = lineColor;
            targetLine.endColor = lineColor;
        }

        private void UpdateDamagePreview()
        {
            if (currentRuntimeAbility?.CurrentSkillData == null || sourceCharacter == null)
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
            if (currentRuntimeAbility?.CurrentSkillData == null || targetUnit == null || sourceCharacter == null)
                return "예상 피해: -";

            var activeTemplate = currentRuntimeAbility.BaseSkill?.ActiveTemplate;
            if (activeTemplate != null)
            {
                int coupledRaw = activeTemplate.CalculateRawDamage(sourceCharacter, currentRuntimeAbility, diceValue);
                return coupledRaw > 0 ? $"예상 피해: {coupledRaw}" : "예상 피해: -";
            }

            var skillData = currentRuntimeAbility.CurrentSkillData;
            if (skillData?.Effects == null || skillData.Effects.Count == 0)
            {
                return "예상 피해: -";
            }

            int totalRaw = 0;
            foreach (var effect in skillData.Effects)
            {
                if (effect is Data.Skills.Effects.DiceMultiplierDamageEffect diceEffect)
                {
                    int resolvedMultiplier = diceEffect.GetMultiplierForSource(sourceCharacter);
                    totalRaw += diceValue * resolvedMultiplier;
                }
                else if (effect is Data.Skills.Effects.MageStackDamageEffect mageEffect)
                {
                    int resolvedBaseMultiplier = mageEffect.GetBaseMultiplierForSource(sourceCharacter);
                    int focusStacks = sourceCharacter.StatusEffects != null
                        ? sourceCharacter.StatusEffects.GetEffectValue(EffectType.Focus)
                        : 0;
                    float bonusRatio = mageEffect.GetBonusRatioForSource(sourceCharacter);
                    int baseDamage = diceValue * resolvedBaseMultiplier;
                    totalRaw += Mathf.RoundToInt(baseDamage * (1.0f + focusStacks * bonusRatio));
                }
            }

            return totalRaw > 0 ? $"예상 피해: {totalRaw}" : "예상 피해: -";
        }
        
        /// <summary>
        /// 타겟 선택 시도
        /// </summary>
        private void TrySelectTarget()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            
            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
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
        }
        
        /// <summary>
        /// 유효한 타겟인지 확인
        /// </summary>
        private bool IsValidTarget(GameObject target)
        {
            switch (currentSkill.skillTargetType)
            {
                case SkillTargetType.SingleEnemy:
                case SkillTargetType.AllEnemies:
                    return target.GetComponentInParent<Monster>() != null;
                    
                case SkillTargetType.Self:
                    return target.GetComponentInParent<Character>() == sourceCharacter;
                    
                case SkillTargetType.Ally:
                case SkillTargetType.AllAllies:
                    var character = target.GetComponentInParent<Character>();
                    return character != null && character != sourceCharacter;
                    
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// 타겟 선택 완료 -> SkillManager로 위임
        /// </summary>
        private void NotifyTargetSelected(GameObject target)
        {
            if (SkillManager.Instance == null) return;

            var resolved = ResolveTarget(target);
            SkillManager.Instance.OnTargetSelected(sourceCharacter, resolved, currentRuntimeAbility, diceValue);
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
        /// 타겟 선택 취소
        /// </summary>
        public void CancelTargetSelection()
        {
            sourceCharacter?.OnSkillResolved();
            EndTargetSelection();
            Debug.Log("Target selection cancelled");
        }
        
        /// <summary>
        /// 타겟 선택 종료
        /// </summary>
        private void EndTargetSelection()
        {
            sourceCharacter?.OnSkillTargetingEnded();
            isSelectingTarget = false;
            
            if (targetLine != null)
            {
                targetLine.enabled = false;
            }
            HoverTooltipUI.Instance?.HidePinned();
            currentPreviewTarget = null;
            
            sourceCharacter = null;
            currentRuntimeAbility = null;
        }

        private CharacterSkillData currentSkill => currentRuntimeAbility?.CurrentSkillData;
    }
}
