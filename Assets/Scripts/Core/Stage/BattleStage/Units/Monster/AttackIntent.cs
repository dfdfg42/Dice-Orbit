using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 몬스터 공격 의도 데이터
    /// </summary>
    [System.Serializable]
    public class AttackIntent
    {
        public IntentType Type;
        public TargetType TargetType = TargetType.Characters;
        public int AreaRadius = 0;   // 0: 단일, 1: 좌우 1칸 (총 3칸), etc.

        public int Damage;           // 예정 데미지
        public int HitCount = 1;     // 공격 횟수
        public string Description;

        // 선정된 타겟들 (Character 리스트)
        private List<Core.Character> selectedTargets = new List<Core.Character>();

        // 타겟 타일들 (몬스터 스킬이 타일 기반일 경우)
        private List<TileData> targetedTiles;

        /// <summary>
        /// 선정된 타겟 캐릭터들
        /// </summary>
        public List<Core.Character> Targets
        {
            get => selectedTargets;
            set => selectedTargets = value ?? new List<Core.Character>();
        }

        /// <summary>
        /// 타겟 타일들
        /// </summary>
        public List<TileData> TargetTiles
        {
            get => targetedTiles;
            set => targetedTiles = value;
        }

        public AttackIntent(IntentType type, int damage = 0, string desc = "")
        {
            Type = type;
            Damage = damage;
            Description = desc;
        }

        /// <summary>
        /// 생성자 오버로드 (타겟 포함)
        /// </summary>
        public AttackIntent(IntentType type, TargetType targetType, int damage, List<Core.Character> targets, string desc = "")
        {
            Type = type;
            TargetType = targetType;
            Damage = damage;
            Description = desc;
            selectedTargets = targets ?? new List<Core.Character>();
        }

        /// <summary>
        /// 타겟 새로고침 (죽은 캐릭터 제거)
        /// </summary>
        public void RefreshTargets()
        {
            if (selectedTargets == null) return;
            selectedTargets = selectedTargets.Where(t => t != null && t.IsAlive).ToList();
        }

        /// <summary>
        /// 의도 설명
        /// </summary>
        public override string ToString()
        {
            switch (Type)
            {
                case IntentType.Attack:
                    string targetInfo = "Temp";
                    return $"Attack {targetInfo} ({Damage} dmg)";
                case IntentType.Multi:
                    return $"Multi Attack x{HitCount} ({Damage} each)";
                case IntentType.Defend:
                    return "Defend (+Defense)";
                case IntentType.Buff:
                    return "Power Up (+Attack)";
                case IntentType.Special:
                    return Description;
                default:
                    return "Unknown";
            }
        }
    }
}
