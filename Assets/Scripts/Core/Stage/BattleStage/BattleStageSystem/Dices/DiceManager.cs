using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 주사위 시스템 관리자
    /// </summary>
    public class DiceManager : MonoBehaviour
    {
        [Header("Dice Settings")]
        [SerializeField] private int diceCountPerTurn = 4;
        [SerializeField] private int minDiceValue = 1;
        [SerializeField] private int maxDiceValue = 6;
        [SerializeField] private bool usePartyBasedDiceCount = true;
        [SerializeField] private int dicePerCharacter = 2;
        
        [Header("References")]
        [SerializeField] private UI.DiceUI diceUI;
        
        // Runtime data
        private List<DiceData> currentDice = new List<DiceData>();
        private int diceIdCounter = 0;

        // Forecast bias (일기예보 스킬)
        private int _forecastBiasPercent = 0;
        private int _forecastBiasTurnsLeft = 0;
        
        // Events
        public System.Action<List<DiceData>> OnDiceRolled;
        public System.Action<DiceData> OnDiceUsed;
        public System.Action OnAllDiceUsed;
        
        // Properties
        public List<DiceData> CurrentDice => currentDice;
        public List<DiceData> AvailableDice => currentDice.Where(d => d.State == DiceState.Available).ToList();
        public int AvailableDiceCount => AvailableDice.Count;
        
        private static DiceManager instance;
        public static DiceManager Instance => instance;
        
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
            // 시작 시점에 파티 인원 기준으로 주사위 개수를 동기화합니다.
            RefreshDiceCountFromParty();

            var partyManager = PartyManager.Instance;
            if (partyManager != null)
            {
                partyManager.OnPartyChanged += HandlePartyChanged;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            var partyManager = PartyManager.Instance;
            if (partyManager != null)
            {
                partyManager.OnPartyChanged -= HandlePartyChanged;
            }
        }
        
        /// <summary>
        /// 주사위 굴리기
        /// </summary>
        public void RollDice()
        {
            // 매 턴 주사위 굴림 직전에 파티 인원 기반 개수를 보정합니다.
            RefreshDiceCountFromParty();
            RollDice(diceCountPerTurn);
        }
        
        /// <summary>
        /// 주사위 굴리기 (개수 지정)
        /// </summary>
        public void RollDice(int count)
        {
            // 기존 주사위 초기화
            currentDice.Clear();
            
            // 새로운 주사위 생성
            for (int i = 0; i < count; i++)
            {
                int value = Random.Range(minDiceValue, maxDiceValue + 1);
                DiceData dice = new DiceData(diceIdCounter++, value);
                currentDice.Add(dice);
            }
            
            // 일기예보 바이어스 적용
            if (_forecastBiasTurnsLeft > 0 && _forecastBiasPercent > 0)
            {
                foreach (var die in currentDice)
                {
                    if (die.Value > 3 && Random.value < _forecastBiasPercent / 100f)
                        die.SetValue(Random.Range(1, 4));
                }
                _forecastBiasTurnsLeft--;
                Debug.Log($"[ForecastBias] 적용됨 ({_forecastBiasPercent}%). 남은 턴: {_forecastBiasTurnsLeft}");
            }

            Debug.Log($"Rolled {count} dice: {string.Join(", ", currentDice.Select(d => d.Value))}");

            // UI 업데이트
            if (diceUI != null)
            {
                diceUI.DisplayDice(currentDice);
            }
            
            // 이벤트 발생
            OnDiceRolled?.Invoke(currentDice);
        }
        
        /// <summary>
        /// 남은(미사용) 주사위 전부 재굴림 — 재굴림 물약용. 배정/사용된 주사위는 건드리지 않는다.
        /// </summary>
        public void RerollAvailableDice()
        {
            var available = AvailableDice;
            if (available.Count == 0) return;

            foreach (var die in available)
                die.SetValue(Random.Range(minDiceValue, maxDiceValue + 1));

            Debug.Log($"[DiceManager] 재굴림: {string.Join(", ", available.Select(d => d.Value))}");
            if (diceUI != null) diceUI.DisplayDice(currentDice);
        }

        /// <summary>
        /// 주사위를 캐릭터에 할당
        /// </summary>
        public bool AssignDice(DiceData dice, object character)
        {
            if (dice == null || dice.State != DiceState.Available)
            {
                Debug.LogWarning($"Cannot assign dice: null or not available");
                return false;
            }

            // 할당
            dice.Assign(character);

            Debug.Log($"Dice {dice.ID} (value: {dice.Value}) assigned to character");
            
            // 이벤트 발생
            OnDiceUsed?.Invoke(dice);
            
            // 모든 주사위가 사용되었는지 확인
            if (AvailableDiceCount == 0)
            {
                OnAllDiceUsed?.Invoke();
            }
            
            return true;
        }
        
        /// <summary>
        /// 주사위 할당 해제
        /// </summary>
        public void UnassignDice(DiceData dice)
        {
            if (dice != null)
            {
                dice.Unassign();
                Debug.Log($"Dice {dice.ID} unassigned");
            }
        }
        
        /// <summary>
        /// 모든 주사위 초기화 (턴 종료 시)
        /// </summary>
        public void ResetDice()
        {
            foreach (var dice in currentDice)
            {
                dice.Reset();
            }
            
            currentDice.Clear();
            diceIdCounter = 0;
            
            Debug.Log("All dice reset");
            
            // UI 초기화
            if (diceUI != null)
            {
                diceUI.ClearDice();
            }
        }
        
        /// <summary>
        /// 특정 ID의 주사위 가져오기
        /// </summary>
        public DiceData GetDice(int id)
        {
            return currentDice.FirstOrDefault(d => d.ID == id);
        }
        
        /// <summary>
        /// 일기예보 바이어스 적용 (다음 N턴 동안 낮은 눈금 확률 +percent%)
        /// </summary>
        public void ApplyForecastBias(int percent, int turns)
        {
            _forecastBiasPercent = percent;
            _forecastBiasTurnsLeft = turns;
            Debug.Log($"[ForecastBias] 설정: {percent}%, {turns}턴");
        }

        /// <summary>
        /// DiceUI 참조 설정
        /// </summary>
        public void SetDiceUI(UI.DiceUI ui)
        {
            diceUI = ui;
        }

        private void HandlePartyChanged(int partySize)
        {
            RefreshDiceCountFromParty();
            Debug.Log($"[DiceManager] 파티 변경 감지: 인원 {partySize}, 턴당 주사위 {diceCountPerTurn}");
        }

        private void RefreshDiceCountFromParty()
        {
            if (!usePartyBasedDiceCount) return;

            var partyManager = PartyManager.Instance;
            if (partyManager == null)
            {
                return;
            }

            int partySize = partyManager.PartySize;
            int resolvedDiceCount = Mathf.Max(0, partySize * Mathf.Max(1, dicePerCharacter));
            diceCountPerTurn = resolvedDiceCount;
        }
    }
}
