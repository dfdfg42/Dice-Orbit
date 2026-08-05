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
    public class VulnerableStatus : StatusEffect
    {
        public VulnerableStatus(int percent, int duration) : base(EffectType.Vulnerable, percent, duration)
        {
            IsStackable = false;
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (Owner == null) return;

            // 내가 피해를 받는 쪽일 때 받는 피해량 증가
            if (trigger == CombatTrigger.OnCalculateOutput && context.Target == Owner)
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
        [SerializeField] private int damageBuff = 3;

        public DewPoint()
        {
            skillName = "이슬점";
            description = "아군 전체의 피해량 +3 영구 증가";
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
    // 패시브 [서리 갑옷]
    // ==========================================
    /// <summary>
    /// [서리 갑옷] 다른 아군 몬스터의 공격이 적중하면, 모든 아군 몬스터에게 일시 방어도를 부여한다.
    /// (파이프라인이 방관 몬스터 패시브도 디스패치하므로 owner가 당사자가 아니어도 발화)
    /// </summary>
    [System.Serializable]
    public class FrostArmorPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("다른 아군 공격 적중 시 전 아군에 부여할 방어도")]
        [SerializeField] private int armorAmount = 10;

        public FrostArmorPassive()
        {
            passiveName = "서리 갑옷";
            description = "다른 아군의 공격이 적중하면 모든 아군에게 일시 방어도 부여";
            priority = 10;
            isStackable = false;
        }

        public override string GetDynamicDescription()
            => $"다른 아군 공격 적중 시 모든 아군 방어도 +{armorAmount}";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnHit || context.IsSimulation || !context.IsEffected) return;
            if (!(context.SourceUnit is Monster attacker) || attacker == owner) return;  // 다른 아군 몬스터만

            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) return;
            foreach (var m in monsters)
            {
                if (m == null || !m.IsAlive || m.Stats == null) continue;
                m.Stats.TempArmor += armorAmount;
            }
            Debug.Log($"[서리 갑옷] {attacker.name} 명중 → 전 아군 방어도 +{armorAmount}");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
