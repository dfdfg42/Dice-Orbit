using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Data;
using DiceOrbit.Core;
using TMPro;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 행동 선택 패널 (이동 / 스킬)
    /// </summary>
    public class ActionPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panelObject;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button skillButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI infoText;
        
        // Current state
        private DiceData currentDice;
        private object currentTarget;
        private bool waitingForDice = false; // 주사위 대기 상태
        
        private void Awake()
        {
            // 버튼 이벤트 연결
            if (moveButton != null)
            {
                moveButton.onClick.AddListener(OnMoveClicked);
            }
            
            if (skillButton != null)
            {
                skillButton.onClick.AddListener(OnSkillClicked);
            }
            
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelClicked);
            }
            
            // 초기에는 숨김
            HidePanel();
        }
        
        /// <summary>
        /// 패널 표시 (캐릭터 선택됨, 주사위 대기 중)
        /// </summary>
        public void ShowPanelForCharacter(object target)
        {
            currentTarget = target;
            currentDice = null;
            waitingForDice = true;
            
            // 정보 텍스트 업데이트
            if (infoText != null)
            {
                infoText.text = "Drag a dice here!";
            }
            
            // 버튼 비활성화 (주사위 드롭 전까지)
            SetButtonsEnabled(false);
            
            // 패널 표시
            if (panelObject != null)
            {
                panelObject.SetActive(true);
            }
        }
        
        /// <summary>
        /// 주사위 드롭 처리
        /// </summary>
        public void OnDiceDropped(DiceData dice)
        {
            if (!waitingForDice || currentTarget == null)
            {
                Debug.LogWarning("Not waiting for dice or no target selected");
                return;
            }
            
            currentDice = dice;
            waitingForDice = false;
            
            // 정보 텍스트 업데이트
            if (infoText != null)
            {
                infoText.text = $"Dice Value: {dice.Value}\nChoose Action:";
            }
            
            // 버튼 활성화
            SetButtonsEnabled(true);
        }
        
        /// <summary>
        /// 버튼 활성화/비활성화
        /// </summary>
        private void SetButtonsEnabled(bool enabled)
        {
            if (moveButton != null)
            {
                moveButton.interactable = enabled;
            }
            
            if (skillButton != null)
            {
                skillButton.interactable = enabled;
            }
        }
        
        /// <summary>
        /// 패널 숨김
        /// </summary>
        public void HidePanel()
        {
            Debug.Log("ActionPanel.HidePanel() called");
            
            if (panelObject != null)
            {
                panelObject.SetActive(false);
                Debug.Log("ActionPanel hidden");
            }
            
            currentDice = null;
            currentTarget = null;
            waitingForDice = false;
        }
        
        /// <summary>
        /// 이동 버튼 클릭
        /// </summary>
        private void OnMoveClicked()
        {
            if (currentDice == null) return;
            
            // DiceManager에 할당
            var diceManager = Core.DiceManager.Instance;
            if (diceManager != null)
            {
                bool success = diceManager.AssignDice(currentDice, currentTarget, ActionType.Move);
                
                if (success)
                {
                    Debug.Log($"Move action assigned: {currentDice.Value} steps");
                    
                    // Character 클래스 확인
                    var character = currentTarget as Character;
                    if (character != null)
                    {
                        character.Move(currentDice.Value);
                    }
                    else
                    {
                        // 레거시 TestCharacter 지원
                        var testChar = currentTarget as TestCharacter;
                        if (testChar != null)
                        {
                            testChar.Move(currentDice.Value);
                        }
                    }
                    
                    // UI 업데이트
                    var diceUI = FindObjectOfType<DiceUI>();
                    if (diceUI != null)
                    {
                        diceUI.MarkDiceAsUsed(currentDice);
                    }
                    
                    // 드래그한 DiceElement를 원위치로
                    ReturnDiceElement();
                }
            }
            
            HidePanel();
        }
        
        /// <summary>
        /// 스킬 버튼 클릭
        /// </summary>
        private void OnSkillClicked()
        {
            if (currentDice == null) return;
            
            // DiceManager에 할당
            var diceManager = Core.DiceManager.Instance;
            if (diceManager != null)
            {
                bool success = diceManager.AssignDice(currentDice, currentTarget, ActionType.Skill);
                
                if (success)
                {
                    Debug.Log($"Skill action assigned with power: {currentDice.Value}");
                    
                    // Character 클래스 확인
                    var character = currentTarget as Character;
                    if (character != null)
                    {
                        character.UseSkill(currentDice.Value);
                    }
                    else
                    {
                        // 레거시 TestCharacter 지원
                        var testChar = currentTarget as TestCharacter;
                        if (testChar != null)
                        {
                            testChar.UseSkill(currentDice.Value);
                        }
                    }
                    
                    // UI 업데이트
                    var diceUI = FindObjectOfType<DiceUI>();
                    if (diceUI != null)
                    {
                        diceUI.MarkDiceAsUsed(currentDice);
                    }
                    
                    // 드래그한 DiceElement를 원위치로
                    ReturnDiceElement();
                }
            }
            
            HidePanel();
        }
        
        /// <summary>
        /// 취소 버튼 클릭
        /// </summary>
        private void OnCancelClicked()
        {
            Debug.Log("Action cancelled");
            
            // 드래그한 DiceElement를 원위치로
            ReturnDiceElement();
            
            HidePanel();
        }
        
        /// <summary>
        /// DiceElement를 원위치로 복귀
        /// </summary>
        private void ReturnDiceElement()
        {
            if (currentDice == null) return;
            
            // 모든 DiceElement 찾기
            var diceElements = FindObjectsOfType<DiceElement>();
            foreach (var element in diceElements)
            {
                if (element.Data == currentDice)
                {
                    element.ReturnToOriginalPosition();
                    break;
                }
            }
        }
    }
}
