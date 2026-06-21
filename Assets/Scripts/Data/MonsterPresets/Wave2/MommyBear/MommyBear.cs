using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave2.MommyBear
{
    // ==========================================
    // 패턴 1 [보호]
    // ==========================================
    /// <summary>
    /// 본인 및 아기곰에게 일시 방어도를 부여한다.
    /// 타겟 없는 팀 버프이므로 MonsterSkill: TargetType=Self, IntentType=Defend.
    /// </summary>
    [System.Serializable]
    public class ProtectSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int armorAmount = 5;

        public ProtectSkill()
        {
            skillName = "보호";
            description = "본인 및 아기곰에게 일시 방어도 부여";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (source?.Stats != null) source.Stats.TempArmor += armorAmount;

            var baby = BearHelper.FindMonsterByName("아기 곰");
            if (baby != null && baby.IsAlive && baby.Stats != null)
                baby.Stats.TempArmor += armorAmount;

            Debug.Log($"[보호] 본인 및 아기곰 방어도 +{armorAmount}");
        }
    }

    // ==========================================
    // 패턴 2 [곰은 사람을 찢어]
    // ==========================================
    /// <summary>
    /// 턴 시작 기준 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해.
    /// (대상/범위 선정은 MonsterSkill 설정: RandomCharacter + Tiles + range 2)
    /// </summary>
    [System.Serializable]
    public class MommyBearTear : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;

        public MommyBearTear()
        {
            skillName = "곰은 사람을 찢어";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 패시브 [꿀 묻은 털]
    // ==========================================
    /// <summary>
    /// 필드의 꿀 타일이 일정 개수 이상이면 엄마곰이 받는 피해량을 일정 비율 감소시킨다.
    /// </summary>
    [System.Serializable]
    public class HoneyFurPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("이 개수 이상 꿀 타일이 있으면 발동")]
        [SerializeField] private int honeyTileRequirement = 5;
        [Tooltip("받는 피해 감소 퍼센트")]
        [SerializeField] private int damageReductionPercent = 20;

        public HoneyFurPassive()
        {
            passiveName = "꿀 묻은 털";
            description = "꿀 타일이 5개 이상이면 받는 피해량 20% 감소";
            priority = 10;
            isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            BearPackTracker.EnsureWaveHook();
        }

        public override string GetDynamicDescription()
            => $"꿀 타일 {honeyTileRequirement}개 이상이면 받는 피해 -{damageReductionPercent}% (현재 꿀 {BearPackTracker.HoneyTileCount()}개)";

        public void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;

            // 엄마곰이 피해를 받는 쪽일 때, 꿀 타일이 충분하면 받는 피해 감소
            if (trigger == CombatTrigger.OnCalculateOutput &&
                context.Target == owner &&
                BearPackTracker.HoneyTileCount() >= honeyTileRequirement)
            {
                context.OutputValue *= 1f - (damageReductionPercent / 100f);
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    // ==========================================
    // 공용 헬퍼
    // ==========================================
    public static class BearHelper
    {
        public static Monster FindMonsterByName(string name)
        {
            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) return null;
            foreach (var m in monsters)
                if (m != null && m.IsAlive && m.Stats != null && m.Stats.MonsterName == name)
                    return m;
            return null;
        }
    }
}
