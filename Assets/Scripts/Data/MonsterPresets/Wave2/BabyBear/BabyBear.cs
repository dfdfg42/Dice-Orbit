using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave2.BabyBear
{
    // ==========================================
    // 패턴 1 [꿀 묻은 발]
    // ==========================================
    /// <summary>
    /// 무작위 타일(MonsterSkill 설정: RandomTiles + count 4)에 꿀 타일을 설치한다.
    /// 통과 시 소량 회복하고, 한 턴에 3개 이상 밟으면 이동 불가가 된다.
    /// </summary>
    [System.Serializable]
    public class HoneyPawSkill : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("꿀 타일 통과 시 회복량")]
        [SerializeField] private int healOnStep = 2;
        [Tooltip("이 개수 이상 밟으면 이동 불가")]
        [SerializeField] private int bindThreshold = 3;
        [Tooltip("이동 불가 지속 턴 (해당 턴 + 다음 턴)")]
        [SerializeField] private int bindDuration = 2;

        public HoneyPawSkill()
        {
            skillName = "꿀 묻은 발";
            description = "무작위 타일 4개에 꿀 타일 설치. 통과 시 회복, 한 턴에 3개 이상 밟으면 이동 불가(발동 후 삭제)";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (targetTiles == null) return;

            foreach (var tile in targetTiles)
            {
                if (tile == null) continue;
                if (tile.HasAttribute(TileAttributeType.Honey)) continue;
                tile.AddAttribute(new HoneyPawTile(healOnStep, bindThreshold, bindDuration));
            }
        }
    }

    // ==========================================
    // 패턴 2 [돌진]
    // ==========================================
    /// <summary>
    /// 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해.
    /// (대상/범위 선정은 MonsterSkill 설정: RandomCharacter + Tiles + range 2)
    /// </summary>
    [System.Serializable]
    public class BabyBearCharge : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;

        public BabyBearCharge()
        {
            skillName = "돌진";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 패시브 [아기 곰은 꿀을 좋아해]
    // ==========================================
    /// <summary>
    /// 플레이어가 먹은 꿀의 양만큼 아기곰의 피해량이 1씩 영구 증가.
    /// </summary>
    [System.Serializable]
    public class HoneyLoverPassive : PassiveAbility
    {
        public HoneyLoverPassive()
        {
            passiveName = "아기 곰은 꿀을 좋아해";
            description = "플레이어가 먹은 꿀의 양만큼 피해량이 1씩 영구 증가";
            priority = 10;
            isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            BearPackTracker.EnsureWaveHook();
        }

        public override string GetDynamicDescription()
            => $"먹은 꿀 1개당 피해 +1 (현재 +{BearPackTracker.HoneyEaten})";

        public void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;

            // 아기곰 공격 → 먹은 꿀 양만큼 피해 증가
            if (trigger == CombatTrigger.OnCalculateOutput &&
                context.SourceUnit == owner)
            {
                context.OutputValue += BearPackTracker.HoneyEaten;
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
