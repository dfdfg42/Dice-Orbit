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
        [SerializeField] private UI.RecruitUI recruitUI; // New Refactor 2.0
        [SerializeField] private GameObject combatUI;
        // [SerializeField] private GameObject shopUI;    // Removed Refactor 2.0
        // [SerializeField] private UI.LevelUpUI levelUpUI; // Removed Refactor 2.0
        
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
            // Refactor 2.0: Start with Recruit
            ChangeState(GameState.Recruit);
        }
        
        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            ExitState(currentState);
            currentState = newState;
            EnterState(currentState);
            OnStateChanged?.Invoke(currentState);
        }


        /// <summary>
        /// 상태 진입
        /// </summary>
        private void EnterState(GameState state)
        {
            switch (state)
            {
                case GameState.CharacterSelection: // Obsolete
                    // ShowCharacterSelection();
                    break;
                    
                case GameState.Combat:
                    StartCombat();
                    break;
                    
                case GameState.Recruit:
                    Debug.Log("Enter Recruit State");
                    if(recruitUI != null)
                    {
                        // Ensure options are ready
                        if(Systems.Recruit.RecruitManager.Instance != null)
                            Systems.Recruit.RecruitManager.Instance.GenerateOptions();
                            
                        recruitUI.Show();
                    }
                    else
                    {
                        var legacySelection = Object.FindFirstObjectByType<UI.CharacterSelectionUI>(FindObjectsInactive.Include);
                        if (legacySelection != null)
                        {
                            legacySelection.Show();
                        }
                        else
                        {
                            Debug.LogWarning("[GameFlow] RecruitUI not found in scene.");
                        }
                    }
                    break;

                case GameState.Reward:

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
                    // HideCharacterSelection(); // Obsolete
                    break;
                    
                case GameState.Combat:
                    break;
                    
                // case GameState.Shop:
                //     HideShop();
                //     break;

                // case GameState.LevelUp:
                //    break;
            }
        }
        
        // === State Methods ===
        
        /* Obsolete
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
        */
        
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
        
        // Removed Shop/LevelUp display methods
        
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
            // Not used directly, dependent on WaveManager
        }

        public void OnWaveCleared(int wave)
        {
            Debug.Log($"[GameFlow] Wave {wave} Cleared. Proceding to Reward.");
            ChangeState(GameState.Reward);
        }

        public void OnRewardComplete()
        {
            ChangeState(GameState.Recruit);
        }

        public void OnRecruitComplete()
        {
            ChangeState(GameState.Combat);
            // Trigger Next Wave
            if(WaveManager.Instance != null)
                WaveManager.Instance.StartNextWave();
        }
        
        /// <summary>
        /// 전투 패배
        /// </summary>
        public void OnCombatDefeat()
        {
            ChangeState(GameState.GameOver);
        }

        private void ShowVictory()
        {
            Debug.Log("[GameFlow] Victory Screen Shown");
            // TODO: Implement UI
        }

        private void ShowGameOver()
        {
            Debug.Log("[GameFlow] Game Over Screen Shown");
            // TODO: Implement UI
        }
    }
}
