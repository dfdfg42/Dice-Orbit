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
        [SerializeField] private UI.CharacterSelectionUI characterSelectionUI;
        [SerializeField] private UI.RewardUI rewardUI;
        [SerializeField] private GameObject combatUI;
        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "";

        private bool pendingStartGame = false;
        private int lastWaveCleared = 0;
        // 레벨업 타일을 밟은 캐릭터를 임시 보관합니다.
        private Character pendingLevelUpCharacter;
        
        // Properties
        public GameState CurrentState => currentState;
        
        // Events
        public event System.Action<GameState> OnStateChanged;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);
                Debug.Log($"[GameFlow] Awake - Instance set, DontDestroyOnLoad, scene={SceneManager.GetActiveScene().name}");
            }
            else
            {
                Debug.LogWarning("[GameFlow] Duplicate instance detected, destroying new instance");
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            Debug.Log($"[GameFlow] Start - gameplaySceneName='{gameplaySceneName}', scene={SceneManager.GetActiveScene().name}");
            CacheSceneReferences();
            ChangeState(GameState.MainMenu);
        }

        private void OnEnable()
        {
            Debug.Log("[GameFlow] OnEnable - subscribing to sceneLoaded");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            Debug.Log("[GameFlow] OnDisable - unsubscribing from sceneLoaded");
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        
        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            Debug.Log($"[GameFlow] ChangeState: {currentState} -> {newState}");
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
                case GameState.MainMenu:
                    if (mainMenuUI != null) mainMenuUI.Show();
                    if (combatUI != null) combatUI.SetActive(false);
                    if (characterSelectionUI != null) characterSelectionUI.Hide();
                    if (rewardUI != null) rewardUI.Hide();
                    break;
                    
                case GameState.Combat:
                    StartCombat();
                    break;
                    
                case GameState.Recruit:
                    Debug.Log("Enter Recruit State");
                    if (characterSelectionUI != null)
                    {
                        characterSelectionUI.Show();
                    }
                    break;

                case GameState.Reward:
                    if (characterSelectionUI != null) characterSelectionUI.Hide();
                    if (combatUI != null) combatUI.SetActive(false);
                    if (rewardUI != null) rewardUI.Show();
                    break;

                case GameState.LevelUp:
                    EnterLevelUpState();
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
                    break;
                    
                case GameState.Combat:
                    if (combatUI != null) combatUI.SetActive(false);
                    break;
            }
        }
        
        // === State Methods ===
        
        private void StartCombat()
        {
            Debug.Log("Starting combat...");
            
            if (combatUI != null)
            {
                combatUI.SetActive(true);
            }

            // Ensure first wave spawns if none started yet
            if (WaveManager.Instance != null && WaveManager.Instance.CurrentWave == 0)
            {
                Debug.Log("[GameFlow] Starting first wave...");
                WaveManager.Instance.StartFirstWave();
            }
            else if (WaveManager.Instance != null && !WaveManager.Instance.IsWaveActive)
            {
                 // Resume combat or start next? 
                 // Usually StartNextWave is called by 'OnRewardComplete'
                 // But if we just entered Combat state without active wave...
                 // It might be a reload or debug entry.
                 Debug.Log("[GameFlow] Entered Combat state but no wave active. Ensuring combat starts if monsters exist.");
                 if (CombatManager.Instance != null && CombatManager.Instance.ActiveMonsters.Count > 0)
                 {
                     CombatManager.Instance.StartCombat();
                 }
            }
            else if (WaveManager.Instance == null)
            {
                Debug.LogWarning("[GameFlow] WaveManager not found in scene. Monsters will not spawn.");
                 // Fallback for debug scenes without WaveManager
                if (CombatManager.Instance != null) CombatManager.Instance.StartCombat();
            }
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
            // If max wave reached?
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
            // After the initial recruit, go straight to combat (First Wave)
            // Or if in-between waves?
            if (WaveManager.Instance != null && WaveManager.Instance.CurrentWave == 0)
            {
                 // Start game -> Recruit -> Combat(Wave1)
                 ChangeState(GameState.Combat);
            }
            else
            {
                 // Wave Clear -> Recruit -> Reward -> Combat
                 // If we have Reward UI, maybe go to Reward?
                 // Current flow: Wave -> Reward -> Recruit -> Combat (Next Wave)
                 // or Wave -> Recruit -> Reward -> Combat?
                 // Let's assume Recruit -> Reward.
                 ChangeState(GameState.Reward);
            }
        }
        
        /// <summary>
        /// 전투 패배
        /// </summary>
        public void OnCombatDefeat()
        {
            ChangeState(GameState.GameOver);
        }

        /// <summary>
        /// 레벨업 트리거 (LevelUpAttribute Tile)
        /// </summary>
        public void TriggerLevelUp(Character character)
        {
            if (character == null || character.Stats == null) return;
            pendingLevelUpCharacter = character;
            Debug.Log($"[GameFlow] TriggerLevelUp called for {character.Stats.CharacterName} - State Change to LevelUp");
            ChangeState(GameState.LevelUp);
        }

        /// <summary>
        /// 메인메뉴에서 게임 시작
        /// </summary>
        public void StartGame()
        {
            Debug.Log("[GameFlow] StartGame called");
            if (!string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.name != gameplaySceneName)
                {
                    Debug.Log($"[GameFlow] Loading gameplay scene: {gameplaySceneName} (current: {activeScene.name})");
                    pendingStartGame = true;
                    SceneManager.LoadScene(gameplaySceneName);
                    return;
                }
            }

            StartGameFlow();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[GameFlow] Scene loaded: {scene.name}, pendingStartGame={pendingStartGame}");
            CacheSceneReferences();
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
            // Start with recruit selection (first character pick)
            Debug.Log("[GameFlow] StartGameFlow -> Recruit");
            ChangeState(GameState.Recruit);
        }

        private void CacheSceneReferences()
        {
            if (mainMenuUI == null)
            {
                mainMenuUI = Object.FindFirstObjectByType<UI.MainMenuUI>(FindObjectsInactive.Include);
            }

            if (characterSelectionUI == null)
            {
                characterSelectionUI = Object.FindFirstObjectByType<UI.CharacterSelectionUI>(FindObjectsInactive.Include);
            }

            if (rewardUI == null)
            {
                rewardUI = Object.FindFirstObjectByType<UI.RewardUI>(FindObjectsInactive.Include);
            }

            if (combatUI == null)
            {
                var combatCanvas = GameObject.Find("CombatUI");
                if (combatCanvas != null)
                {
                    combatUI = combatCanvas;
                }
            }

            Debug.Log($"[GameFlow] CacheSceneReferences - mainMenuUI={(mainMenuUI != null)}, characterSelectionUI={(characterSelectionUI != null)}, rewardUI={(rewardUI != null)}, combatUI={(combatUI != null)}");
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

        private void EnterLevelUpState()
        {
            if (pendingLevelUpCharacter == null || pendingLevelUpCharacter.Stats == null)
            {
                ChangeState(GameState.Combat);
                return;
            }

            // 레벨업 타일에서는 항상 액티브 1개 + 패시브 1개를 자동 강화합니다.
            pendingLevelUpCharacter.LevelUpCharacter();

            // 레벨업 상태는 일시 상태이므로 즉시 전투 흐름으로 복귀합니다.
            pendingLevelUpCharacter = null;
            ChangeState(GameState.Combat);
        }
    }
}
