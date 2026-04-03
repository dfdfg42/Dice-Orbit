using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave4.LunaKnight
{
    // ==========================================
    // 1. 루나 나이트 스킬 구현
    // ==========================================
    [System.Serializable]
    public class LunaKnightSkill1 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;

        public LunaKnightSkill1()
        {
            skillName = "그믐달";
            description = $"무작위 대상 1명에게 {damage} 피해";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackUnits(source, targetUnits, damage);
        }
    }

    [System.Serializable]
    public class LunaKnightSkill2 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 20;

        public LunaKnightSkill2()
        {
            skillName = "월광";
            description = $"모든 짝수 타일에 {damage} 피해";
        }

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            return GameManager.Instance.GetOrbitManager().Tiles
                .Where(tile => tile.TileIndex % 2 == 0) // 짝수 타일
                .ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 2. 루나 나이트 패시브 구현
    // ==========================================
    [System.Serializable]
    public class LunaKnightPassive : PassiveAbility
    {
        [Header("Skill Settings")]
        [Tooltip("기사가 받는 피해 증가량")]
        [SerializeField] private float damageTakenMultiplier = 1.3f;
        [Tooltip("기사가 주는 피해 증가량")]
        [SerializeField] private float damageDealtMultiplier = 1.3f;

        public LunaKnightPassive()
        {
            passiveName = "음력";
            description = "적이 짝수타일에 있을 경우, 해당 적에게 입히는 피해량 30% 증가\n적이 홀수타일에서 공격할 경우, 입는 피해량 30% 증가";
            priority = 10; 
            isStackable = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context?.Action == null) return;

            // 방어 (입는 피해량 처리)
            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.Attack && 
                context.Target == owner)
            {
                if (context.SourceUnit is Character c)
                {
                    if (c.CurrentTile.TileIndex % 2 == 1) // 공격하는 적이 홀수 타일에 있을 때
                    {
                        context.OutputValue = Mathf.RoundToInt(context.OutputValue * damageTakenMultiplier); // 입는 피해량 30% 증가
                    }
                }
            }

            // 공격 (입히는 피해량 처리)
            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.Attack &&
                context.SourceUnit == owner)
            {
                if (context.Target is Character c)
                {
                    if (c.CurrentTile.TileIndex % 2 == 0) // 공격받는 적이 짝수 타일에 있을 때
                    {
                        context.OutputValue = Mathf.RoundToInt(context.OutputValue * damageDealtMultiplier); // 입히는 피해량 30% 증가
                    }
                }
            }
        }

        public override bool AllowSamePassive(PassiveAbility incoming)
        {
            return false;
        }
    }
}
