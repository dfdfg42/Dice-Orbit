using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 포션 효과 종류. 포션 = 1회성 순간 개입, 중심축은 주사위 조작 (스펙 §5).
    /// </summary>
    /// <summary>
    /// 포션 사용 시 대상 지정 방식
    /// </summary>
    public enum PotionTargetType
    {
        None,   // 타겟 지정 없음 (기본, 자동 발동 형태)
        Ally,   // 아군 캐릭터 지정
        Enemy,  // 적 몬스터 지정
        Any,    // 아군 및 적 모두 지정 가능
        Tile    // 보드 타일 지정 (예: 중화 포션)
    }

    public abstract class Potion : ScriptableObject
    {
        [Tooltip("세이브 식별자 — 자동으로 채워집니다. 직접 수정하지 마세요.")]
        [SerializeField] private string saveId;

        /// <summary>세이브가 이 에셋을 다시 찾는 키. 한 번 정해지면 바뀌지 않는다.</summary>
        public string SaveId => saveId;

        public string PotionName = "물약";
        [TextArea(2, 4)] public string Description = "물약 설명";
        public Sprite Icon;
        public PotionTargetType TargetType = PotionTargetType.None;
        [Min(1)] public int ShopPrice = 40;
        [Tooltip("전투 중에만 사용 가능")]
        public bool CombatOnly;

        /// <summary>
        /// 타겟을 받아 포션의 효과를 실행합니다.
        /// </summary>
        public abstract bool Use(Unit target = null);

        /// <summary>타일 대상 포션(TargetType.Tile)이 구현. 기본은 사용 불가.</summary>
        public virtual bool UseOnTile(Data.TileData tile) => false;

#if UNITY_EDITOR
        /// <summary>
        /// protected virtual이어야 한다. Unity는 가장 파생된 클래스의 OnValidate 하나만 호출하므로,
        /// private으로 두면 HealPotion이 자기 OnValidate를 추가하는 순간 saveId 채우기가 조용히 멈춘다.
        /// 서브클래스가 OnValidate를 override하면 반드시 base.OnValidate()를 호출할 것.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(saveId)) saveId = name;
        }
#endif
    }
}
