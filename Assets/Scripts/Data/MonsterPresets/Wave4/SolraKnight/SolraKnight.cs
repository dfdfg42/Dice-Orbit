using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave4.SolraKnight
{
    // ==========================================
    // 1. 솔라 나이트 스킬 구현
    // ==========================================
    /// <summary>
    /// 솔라 나이트가 사용할 스킬 틀입니다. SkillData를 상속받습니다.
    /// 구체적인 수치나 로직은 필요에 따라 채워넣으세요.
    /// </summary>
    [System.Serializable]
    public class SolraKnightSkill1 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 30;
        public SolraKnightSkill1()
        {
            skillName = "천공검";
            description = $"무작위 대상 1명에게 {damage} 피해";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackUnits(source, targetUnits, damage);
            // TODO: 솔라 나이트 스킬 효과 구현 (데미지나 방어막, 보호 등)
        }
    }

    [System.Serializable]
    public class SolraKnightSkill2 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 20;

        public SolraKnightSkill2()
        {
            skillName = "플레어";
            description = $"모든 홀수 타일에 {damage} 피해";
        }

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            return GameManager.Instance.GetOrbitManager().Tiles
                .Where(tile => tile.TileIndex % 2 == 1)
                .ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            // TODO: 솔라 나이트 스킬 효과 구현 (데미지나 방어막, 보호 등)
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 2. 솔라 나이트 사망 효과 구현 (필요 시 주석 해제)
    // ==========================================
    /*
    [System.Serializable]
    public class SolraKnightDeath : DeathEffect
    {
        public SolraKnightDeath()
        {
            effectName = "Solra Knight Death";
            description = "솔라 나이트 사망 효과입니다.";
        }

        public override void Execute(Monster deadMonster)
        {
            // TODO: 사망 시 효과 구현
        }
    }
    */

    // ==========================================
    // 3. 솔라 나이트 패시브 구현
    // ==========================================
    /// <summary>
    /// 솔라 나이트의 고유 패시브 스킬 틀입니다. PassiveAbility를 상속받습니다.
    /// </summary>
    [System.Serializable]
    public class SolraKnightPassive : PassiveAbility
    {
        [Header("Skill Settings")]
        [Tooltip("기사가 받는 피해 증가량")]
        [SerializeField] private float damageTakenMultiplier = 1.3f;
        [Tooltip("기사가 주는 피해 증가량")]
        [SerializeField] private float damageDealtMultiplier = 1.3f;
        public SolraKnightPassive()
        {
            passiveName = "양력";
            description = "적이 홀수타일에 있을 경우, 해당 적에게 입히는 피해량 30% 증가\n적이 짝수타일에서 공격할 경우, 입는 피해량 30% 증가";
            priority = 10; 
            isStackable = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 예외 방지
            if (context?.Action == null) return;

            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.Attack && 
                context.Target == owner)
            {
                if (context.SourceUnit is Character c)
                {
                    if (c.CurrentTile.TileIndex % 2 == 0) // 공격하는 적이 짝수 타일에 있을 때
                    {
                        Debug.Log("태양의 기사 패시브 발동(아프게 맞기)");
                        context.OutputValue = Mathf.RoundToInt(context.OutputValue * damageTakenMultiplier); // 입는 피해량 30% 증가
                    }
                }
            }

            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.Attack &&
                context.SourceUnit == owner)
            {
                if (context.Target is Character c)
                {
                    if (c.CurrentTile.TileIndex % 2 == 1) // 공격하는 적이 홀수 타일에 있을 때
                    {
                        Debug.Log("태양의 기사 패시브 발동(아프게 때리기)");
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
