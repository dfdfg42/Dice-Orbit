using UnityEngine;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 주사위 데이터 클래스
    /// </summary>
    [System.Serializable]
    public class DiceData
    {
        [SerializeField] private int id;
        [SerializeField] private int value; // 1~6
        [SerializeField] private bool isUsed;
        [SerializeField] private ActionType assignedAction;
        
        // 할당된 캐릭터 (Phase 3에서 사용)
        private object assignedCharacter; // 일단 object로, 나중에 Character 타입으로 변경
        
        // Properties
        public int ID => id;
        public int Value => value;
        public bool IsUsed => isUsed;
        public ActionType AssignedAction => assignedAction;
        public object AssignedCharacter => assignedCharacter;
        
        /// <summary>
        /// 생성자
        /// </summary>
        public DiceData(int id, int value)
        {
            this.id = id;
            this.value = Mathf.Clamp(value, 1, 6);
            this.isUsed = false;
            this.assignedAction = ActionType.None;
            this.assignedCharacter = null;
        }
        
        /// <summary>
        /// 주사위를 캐릭터에 할당
        /// </summary>
        public void Assign(object character, ActionType action)
        {
            assignedCharacter = character;
            assignedAction = action;
            isUsed = true;
        }
        
        /// <summary>
        /// 할당 해제
        /// </summary>
        public void Unassign()
        {
            assignedCharacter = null;
            assignedAction = ActionType.None;
            isUsed = false;
        }
        
        /// <summary>
        /// 주사위 초기화 (턴 종료 시)
        /// </summary>
        public void Reset()
        {
            Unassign();
        }
        
        /// <summary>
        /// 주사위 값 재설정
        /// </summary>
        public void SetValue(int newValue)
        {
            value = Mathf.Clamp(newValue, 1, 6);
        }
    }
}
