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
        [Header("Skill Selection")]
        [SerializeField] private GameObject skillSelectPanel;
        [SerializeField] private Transform skillButtonContainer;
        [SerializeField] private GameObject skillSelectButtonPrefab; // Prefab with Button & Text components

        // Current state
        private DiceData currentDice;
        private object currentTarget;
        private bool waitingForDice = false; // 주사위 대기 상태
        OrbitManager orbitManager;
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
            if (skillSelectPanel != null) skillSelectPanel.SetActive(false);
        }

        private void Start()
        {
            orbitManager = FindAnyObjectByType<OrbitManager>();
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
            
            if (skillSelectPanel != null)
                skillSelectPanel.SetActive(false);
            
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
                        orbitManager.Move(character, currentDice.Value);
                    }
                    
                    // UI 업데이트
                    var diceUI = Object.FindAnyObjectByType<DiceUI>();
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
        /// 스킬 버튼 클릭 -> 스킬 선택창 표시
        /// </summary>
        private void OnSkillClicked()
        {
            if (currentDice == null) return;
            
            var character = currentTarget as Character;
            if (character != null)
            {
                PopulateSkillList(character);
                if (skillSelectPanel != null) skillSelectPanel.SetActive(true);
            }
            else
            {
                // Fallback for TestCharacter (legacy, has no runtime skills)
                // Just execute default
                 Debug.LogWarning("ActionPanel: Target is not Character, executing default.");
                 ExecuteSkill(0); // Assuming legacy handling inside
            }
        }

        private void PopulateSkillList(Character character)
        {
            if (skillButtonContainer == null || skillSelectButtonPrefab == null) return;

            // Clear old
            foreach (Transform child in skillButtonContainer) Destroy(child.gameObject);

            var skills = character.Stats.RuntimeActiveSkills;
            for (int i = 0; i < skills.Count; i++)
            {
                int index = i;
                var skill = skills[i];
                var go = Instantiate(skillSelectButtonPrefab, skillButtonContainer);
                
                // Setup Text
                var txt = go.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = $"{skill.BaseSkill.SkillName} (Lv.{skill.CurrentLevel})";
                
                // Add icon if possible
                var img = go.GetComponentsInChildren<Image>();
                // Assume second image is icon if button has background
                if(img.Length > 1 && skill.BaseSkill.Icon != null) img[1].sprite = skill.BaseSkill.Icon;

                // Setup Button
                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => OnSpecificSkillClicked(index));
                }
            }
            
            // Add Cancel Button inside panel? Or user clicks elsewhere.
        }

        private void OnSpecificSkillClicked(int index)
        {
            ExecuteSkill(index);
        }

        private void ExecuteSkill(int index)
        {
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
                        character.UseSkillByIndex(index, currentDice.Value);
                    }
                    
                    // UI 업데이트
                    var diceUI = FindFirstObjectByType<DiceUI>();
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
            var diceElements = Object.FindObjectsByType<DiceElement>(FindObjectsSortMode.None);
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

