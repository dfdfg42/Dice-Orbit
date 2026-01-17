using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 턴 관리 시스템
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DiceManager diceManager;
        
        [Header("Turn Settings")]
        [SerializeField] private int currentTurn = 0;
        
        // 턴 페이즈
        public enum TurnPhase
        {
            Idle,           // 대기
            Roll,           // 주사위 굴리기
            PlayerAction,   // 플레이어 행동 선택
            Execution,      // 액션 실행
            MonsterTurn     // 몬스터 턴
        }
        
        private TurnPhase currentPhase = TurnPhase.Idle;
        
        // Events
        public System.Action<int> OnTurnStart;
        public System.Action<int> OnTurnEnd;
        public System.Action<TurnPhase> OnPhaseChanged;
        
        // Properties
        public int CurrentTurn => currentTurn;
        public TurnPhase CurrentPhase => currentPhase;
        
        private static TurnManager instance;
        public static TurnManager Instance => instance;
        
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // DiceManager 자동 찾기
            if (diceManager == null)
            {
                diceManager = FindObjectOfType<DiceManager>();
            }
            
            // 이벤트 구독
            if (diceManager != null)
            {
                diceManager.OnAllDiceUsed += OnAllDiceUsedHandler;
            }
        }
        
        /// <summary>
        /// 턴 시작
        /// </summary>
        public void StartTurn()
        {
            currentTurn++;
            Debug.Log($"===== Turn {currentTurn} Start =====");
            
            // 페이즈를 Roll로 변경
            ChangePhase(TurnPhase.Roll);
            
            // 주사위 굴리기
            if (diceManager != null)
            {
                diceManager.RollDice();
            }
            
            // 플레이어 행동 페이즈로 전환
            ChangePhase(TurnPhase.PlayerAction);
            
            // 이벤트 발생
            OnTurnStart?.Invoke(currentTurn);
        }
        
        /// <summary>
        /// 턴 종료
        /// </summary>
        public void EndTurn()
        {
            Debug.Log($"===== Turn {currentTurn} End =====");
            
            // 페이즈를 Execution으로 변경
            ChangePhase(TurnPhase.Execution);
            
            // TODO: Phase 3에서 캐릭터 행동 실행
            ExecutePlayerActions();
            
            // 몬스터 턴
            ChangePhase(TurnPhase.MonsterTurn);
            ExecuteMonsterActions();
            
            // 주사위 초기화
            if (diceManager != null)
            {
                diceManager.ResetDice();
            }
            
            // 이벤트 발생
            OnTurnEnd?.Invoke(currentTurn);
            
            // Idle 상태로 복귀
            ChangePhase(TurnPhase.Idle);
        }
        
        /// <summary>
        /// 페이즈 변경
        /// </summary>
        private void ChangePhase(TurnPhase newPhase)
        {
            if (currentPhase != newPhase)
            {
                currentPhase = newPhase;
                Debug.Log($"Phase changed to: {newPhase}");
                OnPhaseChanged?.Invoke(newPhase);
            }
        }
        
        /// <summary>
        /// 플레이어 행동 실행
        /// </summary>
        private void ExecutePlayerActions()
        {
            Debug.Log("Executing player actions...");
            
            if (diceManager == null) return;
            
            // 할당된 주사위들 처리
            foreach (var dice in diceManager.CurrentDice)
            {
                if (dice.IsUsed)
                {
                    Debug.Log($"Execute: Dice {dice.ID} -> Action {dice.AssignedAction}, Value {dice.Value}");
                    
                    // TestCharacter 실행
                    var testChar = dice.AssignedCharacter as TestCharacter;
                    if (testChar != null)
                    {
                        if (dice.AssignedAction == Data.ActionType.Move)
                        {
                            testChar.Move(dice.Value);
                        }
                        else if (dice.AssignedAction == Data.ActionType.Skill)
                        {
                            testChar.UseSkill(dice.Value);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 몬스터 행동 실행
        /// </summary>
        private void ExecuteMonsterActions()
        {
            Debug.Log("Executing monster actions...");
            // TODO: Phase 4에서 몬스터 AI 구현
        }
        
        /// <summary>
        /// 모든 주사위가 사용되었을 때 핸들러
        /// </summary>
        private void OnAllDiceUsedHandler()
        {
            Debug.Log("All dice used! Ready to end turn.");
            // 자동으로 턴 종료하거나, UI 버튼 활성화 등
        }
        
        /// <summary>
        /// 수동으로 턴 시작 버튼용
        /// </summary>
        public void OnStartTurnButtonClicked()
        {
            if (currentPhase == TurnPhase.Idle)
            {
                StartTurn();
            }
        }
        
        /// <summary>
        /// 수동으로 턴 종료 버튼용
        /// </summary>
        public void OnEndTurnButtonClicked()
        {
            if (currentPhase == TurnPhase.PlayerAction)
            {
                EndTurn();
            }
        }
        
        private void OnDestroy()
        {
            // 이벤트 구독 해제
            if (diceManager != null)
            {
                diceManager.OnAllDiceUsed -= OnAllDiceUsedHandler;
            }
        }
    }
}
