using DiceOrbit.Data;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Skeleton
{
    /// <summary>
    /// 고블린 테스트 공격 스킬
    /// </summary>
    [System.Serializable]
    public class SkeletonAttack : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int basicDamage = 30;

        // 생성자에서 기본값 설정
        public SkeletonAttack()
        {
            skillName = "타격";
            description = "상대에게 30 + 자신의 방어도만큼의 데미지를 줍니다";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            int damage = basicDamage + source.Stats.TempArmor;
            foreach (var target in targetUnits)
            {
                if (target == null || !target.IsAlive) continue;

                var context = new CombatContext(
                    source,
                    target,
                    new CombatAction(SkillName, ActionType.Attack, damage)
                );
                CombatPipeline.Instance?.Process(context);

                Debug.Log($"[{SkillName}] {source.name} attacks {target.name} for {damage} damage");
            }
        }
    }
}
