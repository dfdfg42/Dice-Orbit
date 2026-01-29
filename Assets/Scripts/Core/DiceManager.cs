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
        [SerializeField] private Data.Dice.DiceConfig defaultConfig;
        private Data.Dice.DiceConfig currentConfig;

        [Header("References")]
        [SerializeField] private UI.DiceUI diceUI;
        
        // Runtime data
        private List<DiceData> currentDice = new List<DiceData>();
        private int diceIdCounter = 0;
        
        // Events
        public System.Action<List<DiceData>> OnDiceRolled;
        public System.Action<DiceData> OnDiceUsed;
        public System.Action OnAllDiceUsed;
        
        // Properties
        public List<DiceData> CurrentDice => currentDice;
        public List<DiceData> AvailableDice => currentDice.Where(d => !d.IsUsed).ToList();
        public int AvailableDiceCount => AvailableDice.Count;
        
        private static DiceManager instance;
        public static DiceManager Instance => instance;
        
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                currentConfig = defaultConfig;
                
                // create default config if null
                if (currentConfig == null)
                {
                    currentConfig = ScriptableObject.CreateInstance<Data.Dice.DiceConfig>();
                    currentConfig.MinValue = 1;
                    currentConfig.MaxValue = 6;
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        public void EquipConfig(Data.Dice.DiceConfig config)
        {
            if (config != null)
            {
                currentConfig = config;
                Debug.Log($"[DiceManager] Equipped new dice config: {config.ConfigName}");
            }
        }

        /// <summary>
        /// 주사위 굴리기
        /// </summary>
        public void RollDice()
        {
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
                int value = currentConfig.GetRandomValue();
                DiceData dice = new DiceData(diceIdCounter++, value);
                currentDice.Add(dice);
            }
            
            Debug.Log($"Rolled {count} dice using {currentConfig.ConfigName}: {string.Join(", ", currentDice.Select(d => d.Value))}");
            
            // UI 업데이트
            if (diceUI != null)
            {
                diceUI.DisplayDice(currentDice);
            }
            
            // 이벤트 발생
            OnDiceRolled?.Invoke(currentDice);
        }

        public void AddExtraDice(int count, int min, int max)
        {
            for (int i = 0; i < count; i++)
            {
                int value = Random.Range(min, max + 1);
                DiceData dice = new DiceData(diceIdCounter++, value);
                currentDice.Add(dice);
            }
            Debug.Log($"Added {count} extra dice!");
            
            if (diceUI != null) diceUI.DisplayDice(currentDice);
            OnDiceRolled?.Invoke(currentDice); // Update listeners
        }
        
        /// <summary>
        /// 주사위를 캐릭터에 할당
        /// </summary>
        public bool AssignDice(DiceData dice, object character, ActionType action)
        {
            if (dice == null || dice.IsUsed)
            {
                Debug.LogWarning("Cannot assign dice: null or already used");
                return false;
            }
            
            if (action == ActionType.None)
            {
                Debug.LogWarning("Cannot assign dice: action type is None");
                return false;
            }
            
            // 할당
            dice.Assign(character, action);
            
            Debug.Log($"Dice {dice.ID} (value: {dice.Value}) assigned to character with action: {action}");
            
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
        /// DiceUI 참조 설정
        /// </summary>
        public void SetDiceUI(UI.DiceUI ui)
        {
            diceUI = ui;
        }
    }
}
