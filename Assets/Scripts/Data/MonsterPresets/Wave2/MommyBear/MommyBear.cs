using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using static Unity.VisualScripting.Member;

namespace DiceOrbit.Data.MonsterPresets.Wave2.MommyBear
{
    // ==========================================
    // 1. 엄마곰 스킬 구현
    // ==========================================
    /// <summary>
    /// 엄마곰이 사용할 스킬 틀입니다. SkillData를 상속받습니다.
    /// 구체적인 수치나 로직은 필요에 따라 채워넣으세요.
    /// </summary>
    [System.Serializable]
    public class MommyBearAttack1 : SkillData
    {
        int damage = 15;
        public MommyBearAttack1()
        {
            skillName = "휘둘러치기";
            description = $"무작위 대상 1명이 서있는 타일 + 좌우 2칸에 {damage} 피해";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    [System.Serializable]
    public class MommyBearAttack2 : SkillData
    {
        int damage = 20;
        public MommyBearAttack2()
        {
            skillName = "곰은 사람을 찢어";
            description = $"무작위 대상 1명에게 {damage} 피해";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackUnits(source, targetUnits, damage);
        }
    }
    // ==========================================
    // 2. 엄마곰 사망 효과 구현 (필요 시 주석 해제)
    // ==========================================
    /*
    [System.Serializable]
    public class MommyBearDeath : DeathEffect
    {
        public MommyBearDeath()
        {
            effectName = "Mommy Bear Death";
            description = "엄마곰 사망 효과입니다.";
        }

        public override void Execute(Monster deadMonster)
        {
            // TODO: 사망 시 효과 구현
        }
    }
    */

    // ==========================================
    // 3. 엄마곰 패시브 구현
    // ==========================================
    /// <summary>
    /// 엄마곰의 고유 패시브 스킬 틀입니다. PassiveAbility를 상속받습니다.
    /// </summary>
    [System.Serializable]
    public class MommyBearPassive : PassiveAbility
    {
        int healAmount = 10;
        public MommyBearPassive()
        {
            passiveName = "엄마 곰도 꿀을 좋아해";
            description = $"꿀 디버프를 가진 적을 공격할 경우, 체력을 {healAmount} 회복";
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
                context.Target.StatusEffects.HasEffect(EffectType.Honey))
            {
                // 패시브 발동
                Debug.Log("엄마곰 패시브 발동");
                var action = new CombatAction(passiveName, ActionType.Heal, healAmount);
                var attackContext = new CombatContext(owner, owner, action);

                CombatPipeline.Instance?.Process(attackContext);
            }
        }

        public override bool AllowSamePassive(PassiveAbility incoming)
        {
            return false;
        }
    }
}
