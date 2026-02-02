using UnityEngine;
using UnityEngine.SceneManagement;

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
        [SerializeField] private UI.MainMenuUI mainMenuUI;
        [SerializeField] private UI.RecruitUI recruitUI; // New Refactor 2.0
        [SerializeField] private UI.RewardUI rewardUI;
        [SerializeField] private GameObject combatUI;
        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "";

        private bool pendingStartGame = false;
        private int lastWaveCleared = 0;
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
            ChangeState(GameState.MainMenu);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
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

                case GameState.MainMenu:
                    if (mainMenuUI != null) mainMenuUI.Show();
                    if (combatUI != null) combatUI.SetActive(false);
                    if (recruitUI != null) recruitUI.Hide();
                    if (rewardUI != null) rewardUI.Hide();
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
                    break;

                case GameState.Reward:
                    if (recruitUI != null) recruitUI.Hide();
                    if (combatUI != null) combatUI.SetActive(false);
                    if (rewardUI != null) rewardUI.Show();
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
                case GameState.MainMenu:
                    if (mainMenuUI != null) mainMenuUI.Hide();
                    break;
                case GameState.CharacterSelection:
                    // HideCharacterSelection(); // Obsolete
                    break;
                    
                case GameState.Combat:
                    if (combatUI != null) combatUI.SetActive(false);
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
            Debug.Log($"[GameFlow] Wave {wave} Cleared. Proceeding to Recruit.");
            lastWaveCleared = wave;
            ChangeState(GameState.Recruit);
        }

        private void OnWaveStarted(int wave)
        {
            Debug.Log($"[GameFlow] Wave {wave} Started. Combat Beginning.");
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.StartCombat();
            }
        }

        public void OnRewardComplete()
        {
            if (WaveManager.Instance != null && lastWaveCleared >= WaveManager.Instance.MaxWave)
            {
                ChangeState(GameState.Victory);
                return;
            }

            ChangeState(GameState.Combat);
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.StartNextWave();
            }
        }

        public void OnRecruitComplete()
        {
            ChangeState(GameState.Reward);
        }
        
        /// <summary>
        /// 전투 패배
        /// </summary>
        public void OnCombatDefeat()
        {
            ChangeState(GameState.GameOver);
        }

        /// <summary>
        /// 메인메뉴에서 게임 시작
        /// </summary>
        public void StartGame()
        {
            if (!string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.name != gameplaySceneName)
                {
                    pendingStartGame = true;
                    SceneManager.LoadScene(gameplaySceneName);
                    return;
                }
            }

            StartGameFlow();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (pendingStartGame && (string.IsNullOrWhiteSpace(gameplaySceneName) || scene.name == gameplaySceneName))
            {
                pendingStartGame = false;
                StartGameFlow();
            }
            
            // Subscribe to WaveManager events
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnWaveStart -= OnWaveStarted;
                WaveManager.Instance.OnWaveStart += OnWaveStarted;
                
                WaveManager.Instance.OnWaveClear -= OnWaveCleared;
                WaveManager.Instance.OnWaveClear += OnWaveCleared;
            }
        }

        private void StartGameFlow()
        {
            ChangeState(GameState.Combat);
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.StartFirstWave();
            }
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
