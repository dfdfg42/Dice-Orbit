using DiceOrbit.Core.Pipeline;
using DiceOrbit.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 전투 관리자 (싱글톤)
    /// 전투 실행, 턴 관리, UI 제어 통합
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("Combat State")]
        [SerializeField] private bool inCombat = false;
        [SerializeField] private List<Monster> activeMonsters = new List<Monster>();

        [Header("Turn Management")]
        [SerializeField] private int turnCount = 0;
        private bool playerTurnActive = false;

        [Header("UI References")]
        [SerializeField] private Button rollDiceButton;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private TextMeshProUGUI turnCountText;

        // Events
        public System.Action OnCombatStart;
        public System.Action OnCombatEnd;
        public System.Action<Monster> OnMonsterDeath;
        public event System.Action OnMonsterTurnStart; // Legacy event support or internal use

        // Properties
        public bool InCombat => inCombat;
        public List<Monster> ActiveMonsters => activeMonsters;
        public int TurnCount => turnCount;
        public bool PlayerTurnActive => playerTurnActive;

        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("Multiple CombatManagers detected! Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            // 버튼 자동 연결 (Scene에 할당 안 된 경우 대비)
            EnsureButtons();

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
            EnsureButtons();
            UpdateUI();
        }

        private void OnDestroy()
        {
            // Cleanup listeners if needed
            if (rollDiceButton != null) rollDiceButton.onClick.RemoveListener(OnRollDiceClicked);
            if (endTurnButton != null) endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        }

        private void EnsureButtons()
        {
            if (rollDiceButton == null)
            {
                rollDiceButton = FindButtonByName("Roll Dice Button", "RollDiceButton", "Roll Dice");
            }

            if (endTurnButton == null)
            {
                endTurnButton = FindButtonByName("End Turn Button", "EndTurnButton", "End Turn");
            }
        }

        private Button FindButtonByName(params string[] names)
        {
            foreach (var name in names)
            {
                var obj = GameObject.Find(name);
                if (obj != null)
                {
                    var button = obj.GetComponent<Button>();
                    if (button != null) return button;
                }
            }

            var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                if (text == null) continue;

                foreach (var name in names)
                {
                    if (!string.IsNullOrEmpty(text.text) && text.text.Contains(name))
                    {
                        return button;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 전투 시작
        /// </summary>
        public void StartCombat()
        {
            if (inCombat) return;

            inCombat = true;
            turnCount = 0;
            Debug.Log($"Combat started! {activeMonsters.Count} monster(s)");

            OnCombatStart?.Invoke();

            // Start the turn loop directly
            // 0.5초 딜레이 후 시작 (연출을 위해)
            Invoke(nameof(StartPlayerTurn), 0.5f);
        }

        /// <summary>
        /// 전투 종료
        /// </summary>
        public void EndCombat(bool victory)
        {
            if (!inCombat) return;

            inCombat = false;
            HideMonsterIntents(); // Clean up visuals

            if (victory)
            {
                Debug.Log("Victory! All monsters defeated!");
            }
            else
            {
                Debug.Log("Defeat! Party wiped out!");
            }

            OnCombatEnd?.Invoke();

            // Wave 진행 처리
            if (victory)
            {
                if (WaveManager.Instance != null)
                {
                    WaveManager.Instance.CheckWaveClear();
                }
            }
            else
            {
                if (GameFlowManager.Instance != null)
                {
                    GameFlowManager.Instance.OnCombatDefeat();
                }
            }
        }

        // ===========================================
        // Turn Management Logic (Merged from TurnManager)
        // ===========================================

        /// <summary>
        /// 플레이어 턴 시작
        /// </summary>
        public void StartPlayerTurn()
        {
            if (!inCombat) return;

            playerTurnActive = true;
            turnCount++;

            Debug.Log($"=== Turn {turnCount} - Player Turn ===");

            // 캐릭터 턴 시작 처리 (패시브/상태효과)
            var partyManager = PartyManager.Instance;
            if (partyManager != null)
            {
                partyManager.ResetTeamFirstAction();
                foreach (var character in partyManager.Party)
                {
                    if (character != null && character.IsAlive)
                    {
                        character.OnStartTurn();
                    }
                }
            }

            // 주사위 자동 굴리기
            var diceManager = DiceManager.Instance;
            if (diceManager != null)
            {
                diceManager.RollDice();
            }

            // UI Update
            if (rollDiceButton != null) rollDiceButton.interactable = false; // Auto rolled
            if (endTurnButton != null) endTurnButton.interactable = true;

            // 몬스터 공격 의도 미리보기 표시
            ShowMonsterIntents();

            UpdateUI();
        }

        /// <summary>
        /// 플레이어 턴 종료
        /// </summary>
        public void EndPlayerTurn()
        {
            if (!playerTurnActive) return;

            playerTurnActive = false;

            Debug.Log("=== Player Turn End ===");

            // UI Lock
            if (endTurnButton != null) endTurnButton.interactable = false;

            // 공격 의도 미리보기 숨기기 (몬스터 턴 시작 전)
            HideMonsterIntents();

            // 몬스터 턴 실행
            ExecuteMonsterTurn();
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

                if (rollDiceButton != null) rollDiceButton.interactable = false;
                if (endTurnButton != null) endTurnButton.interactable = true;
            }
        }

        /// <summary>
        /// 턴 종료 버튼 클릭
        /// </summary>
        private void OnEndTurnClicked()
        {
            EndPlayerTurn();
        }

        private void UpdateUI()
        {
            if (turnCountText != null)
            {
                turnCountText.text = $"Turn: {turnCount}";
            }
        }

        // ===========================================
        // Monster Logic
        // ===========================================

        /// <summary>
        /// 몬스터 턴 실행
        /// </summary>
        public void ExecuteMonsterTurn()
        {
            if (!inCombat) return;

            Debug.Log("=== Monster Turn Start ===");
            OnMonsterTurnStart?.Invoke();

            var sortedMonster = activeMonsters.OrderByDescending(m => m.Stats.Speed).ToList();
            // 실제 행동
            foreach (var monster in sortedMonster)
            {
                if (monster != null && monster.IsAlive)
                {
                    monster.ExecuteIntent();
                }
            }

            // 파티 전멸 체크
            var partyManager = PartyManager.Instance;
            if (partyManager != null && partyManager.IsPartyWiped())
            {
                EndCombat(false);
                return;
            }

            // End Monster Turn after delay
            StartCoroutine(EndMonsterTurnRoutine());
        }

        private System.Collections.IEnumerator EndMonsterTurnRoutine()
        {
            yield return new WaitForSeconds(1.0f); // Default monster turn duration

            Debug.Log("=== Monster Turn End ===");

            if (inCombat)
            {
                TileTurnEnd(); // Loop back to player
            }

            if (inCombat)
            {
                StartPlayerTurn(); // Loop back to player
            }
        }

        private void TileTurnEnd()
        {
            // 타일 턴 시작 처리 (패시브/상태효과)
            CombatContext context = new CombatContext(null, null, new CombatAction("Turn End", ActionType.None, 0));
            CombatPipeline.Instance.Process(context);
        }

        /// <summary>
        /// 몬스터들의 공격 의도 미리보기 표시
        /// </summary>
        private void ShowMonsterIntents()
        {
            // AttackIndicator가 등록된 모든 Intent를 시각화
            UI.MonsterAttackIntentManager.Instance?.Show();
            Debug.Log("[CombatManager] Showing monster attack previews");
        }

        /// <summary>
        /// 몬스터들의 공격 의도 미리보기 숨기기
        /// </summary>
        private void HideMonsterIntents()
        {
            UI.MonsterAttackIntentManager.Instance?.Hide();
            Debug.Log("[CombatManager] Hiding monster attack previews");
        }

        // ===========================================
        // Management Methods
        // ===========================================

        /// <summary>
        /// 몬스터 등록 (Wave 스폰 시 사용)
        /// </summary>
        public void RegisterMonster(Monster monster)
        {
            if (monster == null) return;
            if (!activeMonsters.Contains(monster))
            {
                activeMonsters.Add(monster);
            }
        }

        /// <summary>
        /// 현재 몬스터 목록 초기화
        /// </summary>
        public void ClearMonsters()
        {
            activeMonsters.Clear();
        }

        /// <summary>
        /// 몬스터 격파 처리
        /// </summary>
        public void OnMonsterDefeated(Monster monster)
        {
            activeMonsters.Remove(monster);
            OnMonsterDeath?.Invoke(monster);

            // 모든 몬스터 격파 확인
            if (activeMonsters.All(m => !m.IsAlive))
            {
                EndCombat(true);
            }
        }

        /// <summary>
        /// 캐릭터가 몬스터 공격
        /// </summary>
        /// <summary>
        /// 캐릭터가 몬스터 공격 (Pipeline 사용)
        /// </summary>
        public void AttackMonster(Monster target, int damage, bool ignoreDefense = false)
        {
            if (target == null || !target.IsAlive) return;

            // System/Direct Attack via Pipeline
            var action = new Pipeline.CombatAction("Direct Attack", Pipeline.ActionType.Attack, damage);
            var context = new Pipeline.CombatContext(null, target, action); // Source is null (System)

            if (Pipeline.CombatPipeline.Instance != null)
            {
                Pipeline.CombatPipeline.Instance.Process(context);
            }
        }

        /// <summary>
        /// 모든 몬스터 공격 (범위 공격, Pipeline 사용)
        /// </summary>
        public void AttackAllMonsters(int damage, bool ignoreDefense = false)
        {
            // 리스트 복사하여 순회 중 변경 대비
            var targets = new List<Monster>(activeMonsters);

            foreach (var monster in targets)
            {
                if (monster.IsAlive)
                {
                    var action = new Pipeline.CombatAction("Global Attack", Pipeline.ActionType.Attack, damage);
                    var context = new Pipeline.CombatContext(null, monster, action);

                    if (Pipeline.CombatPipeline.Instance != null)
                    {
                        Pipeline.CombatPipeline.Instance.Process(context);
                    }
                }
            }
        }

        /// <summary>
        /// 생존한 몬스터 목록
        /// </summary>
        public List<Monster> GetAliveMonsters()
        {
            return activeMonsters.Where(m => m.IsAlive).ToList();
        }
    }
}

