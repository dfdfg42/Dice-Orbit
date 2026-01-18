using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 턴 관리자
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }
        
        [Header("UI References")]
        [SerializeField] private Button rollDiceButton;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private TextMeshProUGUI turnCountText;
        
        [Header("State")]
        [SerializeField] private int turnCount = 0;
        private bool playerTurnActive = true;
        
        // Properties
        public int TurnCount => turnCount;
        public bool PlayerTurnActive => playerTurnActive;
        
        private void Awake()
        {
            // 싱글톤
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            // 버튼 이벤트 연결
            if (rollDiceButton != null)
            {
                rollDiceButton.onClick.AddListener(OnRollDiceClicked);
            }
            
            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
                endTurnButton.interactable = false; // 처음엔 비활성
            }
        }
        
        private void Start()
        {
            UpdateUI();
            StartPlayerTurn();
        }
        
        /// <summary>
        /// 플레이어 턴 시작
        /// </summary>
        public void StartPlayerTurn()
        {
            playerTurnActive = true;
            turnCount++;
            
            Debug.Log($"=== Turn {turnCount} - Player Turn ===");
            
            // 자동으로 주사위 굴리기
            var diceManager = DiceManager.Instance;
            if (diceManager != null)
            {
                diceManager.RollDice();
                Debug.Log("Dice automatically rolled!");
            }
            
            // End Turn 버튼 활성화
            if (endTurnButton != null)
            {
                endTurnButton.interactable = true;
            }
            
            UpdateUI();
        }
        
        /// <summary>
        /// Roll Dice 버튼 클릭
        /// </summary>
        private void OnRollDiceClicked()
        {
            var diceManager = DiceManager.Instance;
            if (diceManager != null)
            {
                diceManager.RollDice();
                
                // 주사위 굴린 후 Roll Dice 비활성화
                if (rollDiceButton != null)
                {
                    rollDiceButton.interactable = false;
                }
                
                // End Turn 버튼 활성화
                if (endTurnButton != null)
                {
                    endTurnButton.interactable = true;
                }
            }
        }
        
        /// <summary>
        /// 턴 종료 버튼 클릭
        /// </summary>
        private void OnEndTurnClicked()
        {
            EndPlayerTurn();
        }
        
        /// <summary>
        /// 플레이어 턴 종료
        /// </summary>
        public void EndPlayerTurn()
        {
            if (!playerTurnActive) return;
            
            playerTurnActive = false;
            
            Debug.Log("=== Player Turn End ===");
            
            // End Turn 버튼 비활성화
            if (endTurnButton != null)
            {
                endTurnButton.interactable = false;
            }
            
            // 몬스터 턴 실행
            ExecuteMonsterTurn();
        }
        
        /// <summary>
        /// 몬스터 턴 실행
        /// </summary>
        private void ExecuteMonsterTurn()
        {
            var combatManager = CombatManager.Instance;
            if (combatManager != null && combatManager.InCombat)
            {
                combatManager.ExecuteMonsterTurn();
            }
            
            // 다음 플레이어 턴 시작
            Invoke(nameof(StartPlayerTurn), 1f); // 1초 후
        }
        
        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (turnCountText != null)
            {
                turnCountText.text = $"Turn: {turnCount}";
            }
        }
    }
}
