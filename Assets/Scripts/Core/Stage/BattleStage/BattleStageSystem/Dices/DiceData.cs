using UnityEngine;

namespace DiceOrbit.Data
{
    public enum DiceState
    {
        Available, // 사용 가능
        Reserved,  // 타겟 선택 등 행동 예약을 위해 임시 선점된 상태
        Used       // 사용 완료
    }

    /// <summary>
    /// 주사위 데이터 클래스
    /// </summary>
    [System.Serializable]
    public class DiceData
    {
        [SerializeField] private int id;
        [SerializeField] private int value; // 1~6
        [SerializeField] private DiceState state;

        // 할당된 캐릭터 (Phase 3에서 사용)
        private object assignedCharacter; // 일단 object로, 나중에 Character 타입으로 변경

        // 이 주사위가 나온 덱 슬롯 (효과 발동 + 호버 표시용). 직렬화하지 않음.
        [System.NonSerialized] private DieInstance source;

        // Properties
        public int ID => id;
        public int Value => value;
        public DiceState State { get => state; set => state = value; }
        public object AssignedCharacter => assignedCharacter;
        public DieInstance Source => source;
        
        /// <summary>
        /// 생성자
        /// </summary>
        public DiceData(int id, int value) : this(id, value, null) { }

        public DiceData(int id, int value, DieInstance source)
        {
            this.id = id;
            this.value = value;           // 면 값이 권위 — 1~6 하드 클램프 제거
            this.state = DiceState.Available;
            this.assignedCharacter = null;
            this.source = source;
        }

        /// <summary>
        /// 주사위를 캐릭터에 할당
        /// </summary>
        public void Assign(object character)
        {
            assignedCharacter = character;
            state = DiceState.Reserved;
        }

        /// <summary>
        /// 할당 해제
        /// </summary>
        public void Unassign()
        {
            assignedCharacter = null;
            state = DiceState.Available;
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
            value = newValue;             // 커스텀 면 값 허용 (구 Mathf.Clamp(1,6) 제거)
        }
    }
}
