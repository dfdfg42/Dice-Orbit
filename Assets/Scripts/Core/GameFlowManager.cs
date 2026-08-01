using UnityEngine;
using UnityEngine.SceneManagement;
using DiceOrbit.Core.Run;
using DiceOrbit.Core.Run.Save;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 게임 플로우 관리자 (싱글톤) — 노드맵 런 구조 (2026-07-07 개편).
    ///
    /// 흐름:
    ///   MainMenu → Recruit(2명 채울 때까지 반복) → RunManager.StartRun → Map
    ///   Map(노드 선택) → [전투류] Combat → 클리어 → Reward → (노드 1·2였으면 Recruit) → Map
    ///                  → [휴식] 즉시 회복 → Map 유지
    ///                  → [상점/이벤트] v1 스텁 → Map 유지
    ///                  → [보스] Combat → 클리어 → Victory
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.MainMenu;

        [Header("파티")]
        [Tooltip("런 시작 시 뽑는 인원 (스펙 §3: 2명 시작)")]
        [SerializeField] private int startingPartySize = 2;
        [Tooltip("자동 모집 포함 최대 인원")]
        [SerializeField] private int maxPartySize = 4;
        [Tooltip("이 수만큼의 전투 클리어까지는 보상 후 자동 모집 (스펙 §3: 노드 1·2)")]
        [SerializeField] private int autoRecruitBattles = 2;

        [Header("휴식")]
        [Range(0f, 1f)]
        [SerializeField] private float restHealRatio = 0.3f;

        [Header("References")]
        [SerializeField] private UI.MainMenuUI mainMenuUI;
        [SerializeField] private UI.CharacterSelectionUI characterSelectionUI;
        [SerializeField] private UI.RewardUI rewardUI;
        [SerializeField] private GameObject combatUI;
        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "";

        private bool pendingStartGame = false;
        private bool pendingContinue = false;
        private bool pendingRestart = false;

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
            UI.RunHudUI.EnsureInstance();
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

            Debug.Log($"[GameFlow] ChangeState: {currentState} -> {newState}");
            ExitState(currentState);
            currentState = newState;
            EnterState(currentState);
            OnStateChanged?.Invoke(currentState);
        }

        private void EnterState(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    if (mainMenuUI != null) mainMenuUI.Show();
                    if (combatUI != null) combatUI.SetActive(false);
                    if (characterSelectionUI != null) characterSelectionUI.Hide();
                    if (rewardUI != null) rewardUI.Hide();
                    UI.NodeMapUI.Instance?.Hide();
                    break;

                case GameState.Map:
                    if (combatUI != null) combatUI.SetActive(false);
                    UI.NodeMapUI.EnsureInstance();
                    UI.NodeMapUI.Instance?.Show();
                    RunSaveService.SaveCurrent();   // 노드 단위 자동 저장 (이어하기)
                    break;

                case GameState.Shop:
                    UI.ShopUI.EnsureInstance();
                    UI.ShopUI.Instance?.Show();
                    break;

                case GameState.Event:
                    UI.EventUI.EnsureInstance();
                    UI.EventUI.Instance?.Show();
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

                case GameState.Victory:
                    RunSaveService.Delete();   // 런 종료 — 이어하기 소멸 (로그라이크)
                    ShowVictory();
                    break;

                case GameState.GameOver:
                    RunSaveService.Delete();
                    ShowGameOver();
                    break;
            }
        }

        private void ExitState(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    if (mainMenuUI != null) mainMenuUI.Hide();
                    break;

                case GameState.Map:
                    UI.NodeMapUI.Instance?.Hide();
                    break;

                case GameState.Shop:
                    UI.ShopUI.Instance?.Hide();
                    break;

                case GameState.Event:
                    UI.EventUI.Instance?.Hide();
                    break;

                case GameState.Recruit:
                    if (characterSelectionUI != null) characterSelectionUI.Hide();
                    break;

                case GameState.Reward:
                    if (rewardUI != null) rewardUI.Hide();
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

            var run = RunManager.Instance;
            if (run != null && run.RunActive && run.CurrentNode != null && run.CurrentNode.IsCombat)
            {
                // 노드맵 흐름: 노드가 배정받은 몹 세트로 전투 시작 (번호는 층+1 — 표시용)
                if (CombatManager.Instance != null && !CombatManager.Instance.InCombat)
                {
                    if (run.CurrentNode.Encounter == null)
                    {
                        Debug.LogError("[GameFlow] 이 노드에 몹 세트가 없습니다 — ActDefinition의 티어 풀을 확인하세요.");
                        ChangeState(GameState.Map);
                        return;
                    }
                    CombatManager.Instance.StartEncounter(run.CurrentNode.Encounter, run.CurrentNode.Floor + 1);
                }
                return;
            }

            // 노드맵 없는 디버그 씬 폴백: 이미 배치된 몬스터로 전투만 시작
            Debug.LogWarning("[GameFlow] RunManager 없이 Combat 진입 — 배치된 몬스터로 전투 시작 (디버그 폴백).");
            if (CombatManager.Instance != null && CombatManager.Instance.ActiveMonsters.Count > 0)
            {
                CombatManager.Instance.StartCombat();
            }
        }

        // === 노드맵 진입점 (NodeMapUI가 호출) ===

        /// <summary>맵에서 노드 선택. 노드 타입에 따라 상태 전환/즉시 처리.</summary>
        public void OnNodeSelected(int nodeId)
        {
            var run = RunManager.Instance;
            if (run == null || !run.RunActive) return;

            var node = run.MoveToNode(nodeId);
            if (node == null) return;

            switch (node.Type)
            {
                case MapNodeType.Battle:
                case MapNodeType.Elite:
                case MapNodeType.Boss:
                    ChangeState(GameState.Combat);
                    break;

                case MapNodeType.Rest:
                    ApplyRest();
                    UI.NodeMapUI.Instance?.Rebuild();   // Map 상태 유지, 다음 선택지 갱신
                    break;

                case MapNodeType.Shop:
                    ChangeState(GameState.Shop);
                    break;

                case MapNodeType.Event:
                    ChangeState(GameState.Event);
                    break;
            }
        }

        /// <summary>휴식 노드: 파티 전원 비율 회복 (+유물 보너스).</summary>
        private void ApplyRest()
        {
            float ratio = restHealRatio + (ArtifactManager.Instance?.RestHealBonus01 ?? 0f);
            foreach (var character in Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                if (character == null || character.Stats == null || !character.IsAlive) continue;
                var stats = character.Stats;
                int heal = Mathf.RoundToInt(stats.MaxHP * ratio);
                stats.CurrentHP = Mathf.Min(stats.MaxHP, stats.CurrentHP + heal);
                Debug.Log($"[GameFlow] 휴식 — {stats.CharacterName} +{heal} HP ({stats.CurrentHP}/{stats.MaxHP})");
            }
        }

        // === 전투 결과 ===

        public void OnEncounterCleared()
        {
            Debug.Log("[GameFlow] Encounter Cleared.");

            // 승리 확정 → 리타이어한 파티원 점감 부활 (스펙 §4: 전투 종료 후 부활)
            PartyManager.Instance?.ReviveRetiredMembers();

            var run = RunManager.Instance;
            if (run != null && run.RunActive && run.CurrentNode != null)
            {
                run.OnBattleCleared();

                // 보스 클리어 = 막 종료 (v1: 단일 막 → 승리)
                if (run.CurrentNode.Type == MapNodeType.Boss)
                {
                    ChangeState(GameState.Victory);
                    return;
                }

                // (엘리트 유물 드랍은 보상 화면의 수령 행으로 — RewardUI.BuildRewardRows)
            }

            ChangeState(GameState.Reward);
        }

        public void OnRewardComplete()
        {
            var run = RunManager.Instance;

            // 노드 1·2 클리어 직후에는 자동 모집 (파티가 최대 인원 미만일 때만)
            if (run != null && run.RunActive &&
                run.BattlesCleared <= autoRecruitBattles &&
                CountParty() < maxPartySize)
            {
                ChangeState(GameState.Recruit);
                return;
            }

            ChangeState(GameState.Map);
        }

        public void OnRecruitComplete()
        {
            var run = RunManager.Instance;

            // 런 시작 전(맵 없음): 시작 인원을 채울 때까지 모집 반복 → 다 차면 런 시작
            if (run == null || !run.RunActive)
            {
                if (CountParty() < startingPartySize && characterSelectionUI != null)
                {
                    Debug.Log($"[GameFlow] 시작 인원 {CountParty()}/{startingPartySize} — 모집 계속");
                    characterSelectionUI.Show();   // Recruit 상태 유지한 채 재표시
                    return;
                }

                if (run != null && run.StartRun())
                {
                    ChangeState(GameState.Map);
                    return;
                }

                // RunManager/ActDefinition 없는 씬 폴백: 그냥 전투로
                Debug.LogWarning("[GameFlow] RunManager 없음 — 전투로 직행 (디버그 폴백).");
                ChangeState(GameState.Combat);
                return;
            }

            // 런 중간 모집(노드 1·2 이후) → 맵으로 복귀
            ChangeState(GameState.Map);
        }

        public void OnCombatDefeat()
        {
            ChangeState(GameState.GameOver);
        }

        /// <summary>상점에서 [떠나기] — 맵으로 복귀 (ShopUI가 호출).</summary>
        public void OnShopComplete()
        {
            ChangeState(GameState.Map);
        }

        /// <summary>이벤트 종료 — 맵으로 복귀 (EventUI가 호출).</summary>
        public void OnEventComplete()
        {
            ChangeState(GameState.Map);
        }

        private int CountParty()
        {
            int count = 0;
            foreach (var c in Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
                if (c != null && c.IsAlive) count++;
            return count;
        }

        // === 게임 시작/재시작 ===

        public void StartGame()
        {
            Debug.Log("[GameFlow] StartGame called");
            RunSaveService.Delete();   // 새 게임 = 기존 이어하기 폐기

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

        /// <summary>이어하기 (메인메뉴) — 세이브가 있을 때만. 씬 로드 후 복원 흐름 진입.</summary>
        public void ContinueGame()
        {
            if (!RunSaveService.HasSave())
            {
                Debug.LogWarning("[GameFlow] 이어할 세이브가 없습니다.");
                return;
            }

            Debug.Log("[GameFlow] ContinueGame called");
            if (!string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.name != gameplaySceneName)
                {
                    pendingContinue = true;
                    SceneManager.LoadScene(gameplaySceneName);
                    return;
                }
            }

            ContinueGameFlow();
        }

        /// <summary>세이브 복원 — 참가자 전원 검증 통과 시에만 적용된다.</summary>
        private void ContinueGameFlow()
        {
            var report = RunSaveService.RestoreCurrent();
            if (!report.Success)
            {
                Debug.LogWarning($"[GameFlow] 세이브 복원 실패 — 새 게임으로 시작합니다.\n{report}");
                RunSaveService.Delete();
                StartGameFlow();
                return;
            }

            if (report.Warnings.Count > 0)
                Debug.LogWarning($"[GameFlow] 복원 경고\n{report}");

            ChangeState(GameState.Map);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[GameFlow] Scene loaded: {scene.name}, pendingStartGame={pendingStartGame}");
            CacheSceneReferences();
            UI.RunHudUI.EnsureInstance();   // 런 상단 HUD (골드/유물/포션) — 상태에 따라 스스로 표시/숨김
            if (pendingStartGame && (string.IsNullOrWhiteSpace(gameplaySceneName) || scene.name == gameplaySceneName))
            {
                pendingStartGame = false;
                StartGameFlow();
            }

            if (pendingContinue && (string.IsNullOrWhiteSpace(gameplaySceneName) || scene.name == gameplaySceneName))
            {
                pendingContinue = false;
                ContinueGameFlow();
            }

            // 저장된 환경설정(볼륨/전체화면) 적용 — 씬의 AudioManager가 새로 뜬 뒤에
            UI.SettingsUI.ApplySavedSettings();

            // (전투 결과 통지는 CombatManager가 OnEncounterCleared/OnCombatDefeat를 직접 호출 — 구독 불필요)

            // 재시작 후 재진입: 영입 화면부터 다시 시작
            if (pendingRestart)
            {
                pendingRestart = false;
                StartGameFlow();
            }
        }

        private void StartGameFlow()
        {
            // 시작 모집 (startingPartySize명 채울 때까지 OnRecruitComplete가 반복 표시)
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
            UI.GameResultUI.ShowVictory();
        }

        private void ShowGameOver()
        {
            Debug.Log("[GameFlow] Game Over Screen Shown");
            UI.GameResultUI.ShowGameOver();
        }

        /// <summary>
        /// 게임 재시작: 공유 정적 상태를 초기화하고 현재 씬을 다시 로드한 뒤 영입 화면으로 진입.
        /// </summary>
        public void RestartGame()
        {
            Debug.Log("[GameFlow] RestartGame");

            // 공유 정적 상태 초기화
            DiceOrbit.Data.MonsterPresets.Wave2.BearPackTracker.Reset();

            // 런/플로우 상태 초기화
            RunManager.Instance?.EndRun();
            pendingRestart = true;

            UI.GameResultUI.Instance?.Hide();

            // 현재 씬 재로드 (씬 종속 매니저/오브젝트는 모두 새로 초기화됨)
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
