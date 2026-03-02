using DiceOrbit.Data;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Goblin
{
    /// <summary>
    /// 고블린 테스트 공격 스킬
    /// </summary>
    [System.Serializable]
    public class GoblinAttack : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 10;

        // 생성자에서 기본값 설정
        public GoblinAttack()
        {
            skillName = "test";
            description = "test";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
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
