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
    }
}
