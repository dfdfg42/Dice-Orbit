using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.MonsterPresets.Wave1.Goblin;
using DiceOrbit.Data.Skills.Effects;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 타겟 타입
    /// </summary>
    public enum TargetType
    {
        Characters,     // 단일 타겟
        Tiles,          // 특정 범위 (주변 N칸)
        Self,           // 자기 자신 (버프, 회복 등)
        None            // 타겟 없음 (빈둥거림, 필드 전체 기믹 발동 등)
    }

    /// <summary>
    /// 타겟 선정 전략
    /// </summary>
    public enum TargetSelectionStrategy
    {
        RandomCharacter,  // 무작위 선택
        AllTargets,        // 모든 타겟
        RandomTiles,
        TilesWithAttribute, // 특정 속성이 있는 타일 중심
        Self,               // 자기 자신
        Custom              // 스킬에서 직접 타겟팅 구현
    }

    /// <summary>
    /// 공격 의도 타입
    /// </summary>
    public enum IntentType
    {
        Attack,      // 공격
        Defend,      // 방어 버프
        Buff,        // 공격력 버프
        Special,     // 특수 행동
        Multi        // 다중 공격 (Deprecated: Use Attack with TargetType.Area/All)
    }

    /// <summary>
    /// 몬스터 스킬 (직렬화 가능, 에디터에서 수치 조정 가능)
    /// </summary>
    [System.Serializable]
    public class MonsterSkill
    {
        [Header("Skill Data")]
        [SerializeReference] // 다형성 지원 (각 스킬 클래스 선택 가능)
        public SkillData skillData;

        [Header("Targeting")]
        [SerializeField] private TargetSelectionStrategy targetStrategy = TargetSelectionStrategy.RandomCharacter;
        [SerializeField] private TargetType targetType = TargetType.Characters;

        [Tooltip("TargetStrategy가 TilesWithAttribute일 때 탐색할 타일 속성")]
        [SerializeField] private DiceOrbit.Data.Tile.TileAttributeType targetTileAttribute = DiceOrbit.Data.Tile.TileAttributeType.None;

        [SerializeField] private int targetCount = 1; // 타겟 수
        [SerializeField] private int targetRange = 0; // 타겟으로부터 범위 (타일 기반 타겟팅 시 사용)
        [SerializeField] private IntentType intentType=IntentType.Attack;
        [Tooltip("표시할 스킬 아이콘")]
        [SerializeField] private Sprite intentIcon;

        public TargetSelectionStrategy TargetStrategy => targetStrategy;
        public TargetType TargetType => targetType;
        public DiceOrbit.Data.Tile.TileAttributeType TargetTileAttribute => targetTileAttribute;
        public int TargetCount => targetCount;
        public int TargetRange => targetRange;
        public IntentType IntentType => intentType;
        public Sprite IntentIcon => intentIcon;

        // 런타임에 생성된 AttackIntent (캐싱용)
        [System.NonSerialized]
        private AttackIntent cachedIntent;

        /// <summary>
        /// 스킬 대상 선정 및 AttackIntent 생성
        /// </summary>
        public AttackIntent GenerateIntent(Monster owner)
        {
            if (skillData == null)
            {
                Debug.LogWarning("[MonsterSkill] SkillData is null!");
                return null;
            }

            cachedIntent = new AttackIntent(this, owner);
            return cachedIntent;
        }
    }
}
