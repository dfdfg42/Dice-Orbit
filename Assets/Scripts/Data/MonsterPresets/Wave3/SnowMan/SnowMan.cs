using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 공격력 버프 (데미지 계산 시 추가)
    /// </summary>
    public class SlushSnow : StatusEffect
    {
        public SlushSnow(int value, int duration) : base(EffectType.SlushSnow, value, duration)
        {
            IsStackable = false;
        }


        public override void EffectApplied()
        {
            if (Owner.Stats is CharacterStats c)
            {
                c.MoveDebuff += Value;
            }
        }

        public override void EffectExpired()
        {
            if (Owner.Stats is CharacterStats c)
            {
                c.MoveDebuff -= Value;
            }
        }
    }
}

namespace DiceOrbit.Data.MonsterPresets.Wave3.SnowMan
{
    // ==========================================
    // 1. 눈사람 스킬 구현
    // ==========================================
    /// <summary>
    /// 눈사람이 사용할 스킬 틀입니다. SkillData를 상속받습니다.
    /// 구체적인 수치나 로직은 필요에 따라 채워넣으세요.
    /// </summary>
    [System.Serializable]
    public class ThrowSnow : SkillData
    {
        int damage = 30;
        public ThrowSnow()
        {
            skillName = "눈 던지기";
            description = $"무작위 대상 1명에게 {damage} 피해";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackUnits(source, targetUnits, damage);
        }
    }

    [System.Serializable]
    public class SnowStom : SkillData
    {
        int damage = 20;
        public SnowStom()
        {
            skillName = "눈보라";
            description = $"턴 시작 기준 무작위 대상 1명이 서있는 타일 + 좌우 2칸에 {damage} 피해";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }


    // ==========================================
    // 2. 눈사람 사망 효과 구현 (필요 시 주석 해제)
    // ==========================================
    /*
    [System.Serializable]
    public class SnowManDeath : DeathEffect
    {
        public SnowManDeath()
        {
            effectName = "SnowMan Death";
            description = "눈사람 사망 효과입니다.";
        }

        public override void Execute(Monster deadMonster)
        {
            // TODO: 사망 시 효과 구현
        }
    }
    */

    // ==========================================
    // 3. 눈사람 패시브 구현
    // ==========================================
    /// <summary>
    /// 눈사람의 고유 패시브 스킬 틀입니다. PassiveAbility를 상속받습니다.
    /// </summary>
    [System.Serializable]
    public class SnowManPassive : PassiveAbility
    {
        int moveDebuffValue = 2; // 이동력 감소 수치
        int duration = 1; // 디버프 지속 턴 수

        public SnowManPassive()
        {
            passiveName = "진창눈";
            description = "눈사람의 공격에 피격 시, 진창눈 디버프 부여";
            priority = 10; 
            isStackable = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 예외 방지
            if (context?.Action == null) return;
            if (trigger == CombatTrigger.OnHit &&
                context.Action.Type == ActionType.Attack && 
                context.SourceUnit == owner && 
                context.Target != null &&
                context.Target.IsAlive )
            {
                context.Target.StatusEffects.AddEffect(new DiceOrbit.Systems.Effects.SlushSnow(moveDebuffValue, duration + 1));
            }
        }

        public override bool AllowSamePassive(PassiveAbility incoming)
        {
            return false;
        }
    }
}
