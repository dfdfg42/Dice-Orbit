using UnityEngine;
using UnityEngine.InputSystem;
using DiceOrbit.Data;
using System.Collections.Generic;
using DiceOrbit.Data.Skills.Effects;
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
        private CharacterSkillData currentSkill;
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
        public void StartTargetSelection(Character character, CharacterSkillData skill, int dice)
        {
            sourceCharacter = character;
            currentSkill = skill;
            diceValue = dice;
            isSelectingTarget = true;
            
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
            
            Debug.Log($"Target selection started for {skill.SkillName} (Type: {skill.skillTargetType})");
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
            if (currentSkill == null || sourceCharacter == null)
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
            if (currentSkill?.Effects == null || currentSkill.Effects.Count == 0 || targetUnit == null || sourceCharacter == null)
                return "예상 적용 피해: -";

            int totalRaw = 0;
            int totalApplied = 0;
            int remainingArmor = targetUnit.Stats != null ? Mathf.Max(0, targetUnit.Stats.TempArmor) : 0;
            int initialArmor = remainingArmor;
            int focusStacks = sourceCharacter.StatusEffects != null
                ? sourceCharacter.StatusEffects.GetEffectValue(EffectType.Focus)
                : 0;

            foreach (var effect in currentSkill.Effects)
            {
                if (effect == null) continue;

                if (effect is DiceMultiplierDamageEffect diceEffect)
                {
                    int raw = diceValue * diceEffect.multiplier;
                    totalRaw += raw;
                    int absorbed = Mathf.Min(raw, remainingArmor);
                    remainingArmor -= absorbed;
                    totalApplied += Mathf.Max(0, raw - absorbed);
                }
                else if (effect is MageStackDamageEffect mageEffect)
                {
                    int baseDamage = diceValue * mageEffect.baseMultiplier;
                    float multiplier = 1.0f + (focusStacks * mageEffect.bonusDamageRatioPerStack);
                    int raw = Mathf.RoundToInt(baseDamage * multiplier);
                    totalRaw += raw;
                    int absorbed = Mathf.Min(raw, remainingArmor);
                    remainingArmor -= absorbed;
                    totalApplied += Mathf.Max(0, raw - absorbed);
                }
            }

            if (totalRaw <= 0) return "예상 적용 피해: -";
            return $"예상 적용 피해: {totalApplied}\n(원본 {totalRaw} - 방어도 {initialArmor})";
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
            SkillManager.Instance.OnTargetSelected(sourceCharacter, resolved, currentSkill, diceValue);
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
            isSelectingTarget = false;
            
            if (targetLine != null)
            {
                targetLine.enabled = false;
            }
            HoverTooltipUI.Instance?.HidePinned();
            currentPreviewTarget = null;
            
            sourceCharacter = null;
            currentSkill = null;
        }
    }
}
