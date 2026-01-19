using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 게임 플로우 관리자 (싱글톤)
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }
        
        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.MainMenu;
        
        [Header("References")]
        [SerializeField] private GameObject characterSelectionUI;
        [SerializeField] private GameObject combatUI;
        [SerializeField] private GameObject shopUI;
        
        // Properties
        public GameState CurrentState => currentState;
        
        // Events
        public event System.Action<GameState> OnStateChanged;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // 게임 시작 시 캐릭터 선택으로
            ChangeState(GameState.CharacterSelection);
        }
        
        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (currentState == newState) return;
            
            Debug.Log($"Game State: {currentState} -> {newState}");
            
            // 이전 상태 정리
            ExitState(currentState);
            
            // 새 상태로 전환
            currentState = newState;
            EnterState(newState);
            
            // 이벤트 발생
            OnStateChanged?.Invoke(newState);
        }
        
        /// <summary>
        /// 상태 진입
        /// </summary>
        private void EnterState(GameState state)
        {
            switch (state)
            {
                case GameState.CharacterSelection:
                    ShowCharacterSelection();
                    break;
                    
                case GameState.Combat:
                    StartCombat();
                    break;
                    
                case GameState.Shop:
                    ShowShop();
                    break;
                    
                case GameState.Victory:
                    ShowVictory();
                    break;
                    
                case GameState.GameOver:
                    ShowGameOver();
                    break;
            }
        }
        
        /// <summary>
        /// 상태 종료
        /// </summary>
        private void ExitState(GameState state)
        {
            switch (state)
            {
                case GameState.CharacterSelection:
                    HideCharacterSelection();
                    break;
                    
                case GameState.Combat:
                    break;
                    
                case GameState.Shop:
                    HideShop();
                    break;
            }
        }
        
        // === State Methods ===
        
        private void ShowCharacterSelection()
        {
            if (characterSelectionUI != null)
            {
                characterSelectionUI.SetActive(true);
            }
        }
        
        private void HideCharacterSelection()
        {
            if (characterSelectionUI != null)
            {
                characterSelectionUI.SetActive(false);
            }
        }
        
        private void StartCombat()
        {
            Debug.Log("Starting combat...");
            
            if (combatUI != null)
            {
                combatUI.SetActive(true);
            }
            
            // CombatManager 시작
            var combatManager = CombatManager.Instance;
            if (combatManager != null)
            {
                combatManager.StartCombat();
            }
            
            // TurnManager 플레이어 턴 시작
            var turnManager = TurnManager.Instance;
            if (turnManager != null)
            {
                turnManager.StartPlayerTurn();
            }
        }
        
        private void ShowShop()
        {
            if (shopUI != null)
            {
                shopUI.SetActive(true);
            }
        }
        
        private void HideShop()
        {
            if (shopUI != null)
            {
                shopUI.SetActive(false);
            }
        }
        
        private void ShowVictory()
        {
            Debug.Log("Victory!");
            // TODO: Victory UI
        }
        
        private void ShowGameOver()
        {
            Debug.Log("Game Over!");
            // TODO: Game Over UI
        }
        
        // === Public Methods ===
        
        /// <summary>
        /// 캐릭터 선택 완료
        /// </summary>
        public void OnCharacterSelected()
        {
            ChangeState(GameState.Combat);
        }
        
        /// <summary>
        /// 전투 승리
        /// </summary>
        public void OnCombatVictory()
        {
            ChangeState(GameState.Shop);
        }
        
        /// <summary>
        /// 전투 패배
        /// </summary>
        public void OnCombatDefeat()
        {
            ChangeState(GameState.GameOver);
        }
        
        /// <summary>
        /// 상점 종료
        /// </summary>
        public void OnShopExit()
        {
            ChangeState(GameState.Combat);
        }
    }
}
