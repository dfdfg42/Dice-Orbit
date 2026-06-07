using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 동상 디버프. 부착된 유닛이 받는 피해량을 일정 비율 증가시킨다. 중첩 불가.
    /// Value = 증가 퍼센트(예: 20 → +20%).
    /// </summary>
    public class FrostbiteDebuff : StatusEffect
    {
        public FrostbiteDebuff(int percent, int duration) : base(EffectType.Frostbite, percent, duration)
        {
            IsStackable = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            base.OnReact(trigger, context);
            if (Owner == null || context?.Action == null) return;

            // 내가 피해를 받는 쪽일 때 받는 피해량 증가
            if (trigger == CombatTrigger.OnCalculateOutput &&
                context.Action.Type == ActionType.Attack &&
                context.Target == Owner)
            {
                context.OutputValue *= 1f + (Value / 100f);
            }
        }
    }
}

namespace DiceOrbit.Data.MonsterPresets.Wave3.FrostTotem
{
    // ==========================================
    // 패턴 1 [서리꽃]
    // ==========================================
    /// <summary>
    /// 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해.
    /// 대상은 MonsterSkill: RandomCharacter + Tiles + count 1 + range 2.
    /// </summary>
    [System.Serializable]
    public class FrostFlower : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;

        public FrostFlower()
        {
            skillName = "서리꽃";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 패턴 2 [이슬점]
    // ==========================================
    /// <summary>
    /// 모든 아군 몬스터(자신 포함)의 피해량을 영구히 증가시킨다(중첩).
    /// 타겟 없는 팀 버프이므로 MonsterSkill: TargetType=Self, IntentType=Buff.
    /// </summary>
    [System.Serializable]
    public class DewPoint : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("아군 전체에 영구 부여할 공격력 증가량")]
        [SerializeField] private int damageBuff = 2;

        public DewPoint()
        {
            skillName = "이슬점";
            description = "아군 전체의 피해량 +2 영구 증가";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) return;

            foreach (var m in monsters)
            {
                if (m == null || !m.IsAlive || m.StatusEffects == null) continue;

                // 영구(-1) 공격력 버프를 중첩 가능하게 부여
                var buff = new BuffAttackStatus(damageBuff, -1) { IsStackable = true };
                m.StatusEffects.AddEffect(buff);
            }
            Debug.Log($"[이슬점] 모든 아군 피해량 +{damageBuff} (영구)");
        }
    }

    // ==========================================
    // 패시브 [동상]
    // ==========================================
    /// <summary>
    /// 턴 종료 시, 이번 턴에 다른 타일로 이동하지 않은 적에게 동상 디버프를 부여한다.
    /// 동상: 일정 턴 동안 받는 피해량 증가. 중첩 불가.
    /// </summary>
    [System.Serializable]
    public class FrostbitePassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("동상 피해 증가 퍼센트")]
        [SerializeField] private int damageIncreasePercent = 20;
        [Tooltip("동상 지속 턴")]
        [SerializeField] private int duration = 2;

        public FrostbitePassive()
        {
            passiveName = "동상";
            description = "턴 종료 시 이동하지 않은 적에게 동상(받는 피해 +20%, 2턴, 중첩 불가) 부여";
            priority = 10;
            isStackable = false;
        }

        public override string GetDynamicDescription()
            => $"턴 종료 시 이동하지 않은 적에게 받는 피해 +{damageIncreasePercent}% ({duration}턴)";

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context?.Action == null || owner == null) return;

            if (trigger == CombatTrigger.OnPostAction &&
                context.Action.Type == ActionType.OnEndTurn &&
                context.SourceUnit == owner)
            {
                ApplyFrostbiteToNonMovers();
            }
        }

        private void ApplyFrostbiteToNonMovers()
        {
            var alive = PartyManager.Instance?.GetAliveCharacters();
            if (alive == null) return;

            foreach (var c in alive)
            {
                if (c == null || !c.IsAlive || c.StatusEffects == null) continue;

                // 이번 턴에 이동하지 않은 적만
                int moved = (c.Stats as CharacterStats)?.MoveOnThisTurn ?? 0;
                if (moved > 0) continue;

                c.StatusEffects.AddEffect(new FrostbiteDebuff(damageIncreasePercent, duration));
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
