using UnityEngine;
using UnityEngine.InputSystem;
using DiceOrbit.Data;
using System.Collections.Generic;

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
        private SkillData currentSkill;
        private int diceValue;
        private Camera mainCamera;
        
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
        public void StartTargetSelection(Character character, SkillData skill, int dice)
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
            
            Debug.Log($"Target selection started for {skill.SkillName} (Type: {skill.TargetType})");
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
            switch (currentSkill.TargetType)
            {
                case SkillTargetType.SingleEnemy:
                case SkillTargetType.AllEnemies:
                    return target.GetComponent<Monster>() != null;
                    
                case SkillTargetType.Self:
                    return target.GetComponent<Character>() == sourceCharacter;
                    
                case SkillTargetType.Ally:
                case SkillTargetType.AllAllies:
                    var character = target.GetComponent<Character>();
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
            
            sourceCharacter = null;
            currentSkill = null;
        }
    }
}
