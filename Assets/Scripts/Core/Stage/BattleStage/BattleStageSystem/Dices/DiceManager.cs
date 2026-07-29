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
        // 주사위 개수/값은 이제 DiceDeckManager의 덱이 결정한다. min/max는 재굴림 폴백용.
        [SerializeField] private int minDiceValue = 1;
        [SerializeField] private int maxDiceValue = 6;
        
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
            // 덱은 DiceDeckManager가 소유·시드한다 (파티 기반 개수 동기화 제거).
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
        
        /// <summary>소유 덱을 굴린다 — 각 DieInstance의 면에서 랜덤 1개.</summary>
        public void RollDice()
        {
            currentDice.Clear();

            var deck = DiceDeckManager.EnsureInstance()?.Deck;
            if (deck == null || deck.Count == 0)
            {
                Debug.LogWarning("[DiceManager] 덱이 비어 있습니다 — 굴릴 주사위 없음. DiceDeckManager/standardDie 확인.");
            }
            else
            {
                foreach (var inst in deck)
                    currentDice.Add(new DiceData(diceIdCounter++, inst.RollFace(), inst));
            }

            // 일기예보 바이어스 (기존 유지)
            if (_forecastBiasTurnsLeft > 0 && _forecastBiasPercent > 0)
            {
                foreach (var die in currentDice)
                    if (die.Value > 3 && Random.value < _forecastBiasPercent / 100f)
                        die.SetValue(Random.Range(1, 4));
                _forecastBiasTurnsLeft--;
            }

            if (diceUI != null) diceUI.DisplayDice(currentDice);
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
                die.SetValue(die.Source != null ? die.Source.RollFace() : Random.Range(minDiceValue, maxDiceValue + 1));

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
        
        /// <summary>주사위 사용 확정 — 상태를 Used로, 부착 효과를 발동, 시각 제거.</summary>
        public void MarkUsed(DiceData dice, Character user)
        {
            if (dice == null) return;
            dice.State = DiceState.Used;

            var effect = dice.Source?.Effect;
            if (effect != null)
            {
                string summary = effect.Apply(new DieUseContext { User = user, RolledValue = dice.Value });
                Debug.Log($"[DiceManager] 주사위 효과 발동: {summary}");
            }

            diceUI?.MarkDiceAsUsed(dice);   // 시각 제거(상태는 위에서 세팅)
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

    }
}
