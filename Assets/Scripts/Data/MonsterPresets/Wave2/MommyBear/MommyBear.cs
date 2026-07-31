using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave2.MommyBear
{
    /// <summary>
    /// [보호 본능] 아기곰을 가장 최근에 공격한 적에게 피해. 단 그 적이 이번 턴 꿀 cancelHoneySteps개 이상 밟았으면 취소.
    /// 최근 공격자 없음/사망 시 no-op. (BearPackTracker.LastBabyAttacker / GetHoneySteps 사용)
    /// </summary>
    [System.Serializable]
    public class ProtectiveInstinctSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("대상이 이번 턴 이 개수 이상 꿀을 밟으면 취소")]
        [SerializeField] private int cancelHoneySteps = 2;

        public ProtectiveInstinctSkill()
        {
            skillName = "보호 본능";
            description = "아기곰을 가장 최근에 공격한 적에게 피해 (그 적이 이번 턴 꿀 2개 이상 밟으면 취소)";
        }

        public override int GetPreviewDamage() => damage;

        public override List<Unit> GetCustomTargets(MonsterSkill skill, Monster owner)
        {
            var attacker = BearPackTracker.LastBabyAttacker;
            return (attacker != null && attacker.IsAlive) ? new List<Unit> { attacker } : new List<Unit>();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var attacker = BearPackTracker.LastBabyAttacker;
            if (attacker == null || !attacker.IsAlive)
            {
                Debug.Log("[보호 본능] 최근 아기곰 공격자 없음 — no-op");
                return;
            }

            int turn = CombatManager.Instance != null ? CombatManager.Instance.TurnCount : 0;
            if (BearPackTracker.GetHoneySteps(attacker, turn) >= cancelHoneySteps)
            {
                Debug.Log($"[보호 본능] 취소 — 대상이 이번 턴 꿀 {cancelHoneySteps}개 이상 밟음");
                return;
            }

            AttackUnits(source, new List<Unit> { attacker }, damage);
        }
    }

    /// <summary>[곰은 사람을 찢어] 무작위 대상 1명이 속한 타일 + 좌우 각각 3칸에 피해. (RandomCharacter + Tiles + range 3)</summary>
    [System.Serializable]
    public class MommyBearTear : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;

        public MommyBearTear()
        {
            skillName = "곰은 사람을 찢어";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 3칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// [꿀 묻은 발톱] 엄마곰 공격이 명중할 때, 피격 대상 주변 ±honeyRadius칸에 꿀 타일이 있으면
    /// 그 대상에게 쇠약(다음 턴 가하는 피해 -weakenPercent%, weakenDuration턴)을 부여한다.
    /// </summary>
    [System.Serializable]
    public class HoneyFurPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("피격 대상 주변 ±칸 (꿀 탐색)")]
        [SerializeField] private int honeyRadius = 2;
        [Tooltip("쇠약(다음 턴 가하는 피해 감소) 퍼센트")]
        [SerializeField] private int weakenPercent = 20;
        [Tooltip("쇠약 지속 턴 (다음 턴 커버)")]
        [SerializeField] private int weakenDuration = 2;

        public HoneyFurPassive()
        {
            passiveName = "꿀 묻은 발톱";
            description = "공격 범위에 꿀 타일이 있으면, 피격된 적은 다음 턴 피해량 20% 감소";
            priority = 10;
            isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            BearPackTracker.EnsureWaveHook();
        }

        public override string GetDynamicDescription()
            => $"공격 대상 주변 ±{honeyRadius}칸에 꿀이 있으면 피격 적 다음 턴 피해 -{weakenPercent}%";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnHit) return;      // 실제 명중 시
            if (context.IsSimulation) return;
            if (context.SourceUnit != owner) return;
            if (!(context.Target is Character victim) || victim.CurrentTile == null) return;

            if (BearPackTracker.HoneyTilesNear(victim.CurrentTile.TileIndex, honeyRadius) > 0)
            {
                victim.StatusEffects?.AddEffect(new WeakStatus(weakenPercent, weakenDuration));
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>공용 헬퍼</summary>
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
