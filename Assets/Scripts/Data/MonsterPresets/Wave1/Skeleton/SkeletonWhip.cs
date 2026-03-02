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
    public class SkeletonWhip : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int basicDamage = 20;

        // 생성자에서 기본값 설정
        public SkeletonWhip()
        {
            skillName = "휩쓸기";
            description = "무작위 대상 1명이 서있는 타일 + 좌우 2칸에 20 피해";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            int damage = basicDamage;
            foreach (var tile in targetTiles)
            {
                if (tile == null) continue;
                var targetsOnTile = GameManager.Instance?.GetOrbitManager().GetCharactersOnTile(tile);
                foreach (var target in targetsOnTile)
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
}
