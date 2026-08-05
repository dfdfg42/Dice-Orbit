using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave2.MommyBear
{
    /// <summary>
    /// [보호 본능] 아기곰을 가장 최근에 공격한 적에게 피해. 단 그 적이 이번 턴 꿀 cancelHoneySteps개 이상 밟았으면 취소.
    /// 반응형: 사용 시 LastBabyAttacker를 소비(다음엔 [찢어]).
    /// </summary>
    [System.Serializable]
    public class ProtectiveInstinctSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;
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
            BearPackTracker.SetLastBabyAttacker(null);   // 반응형: 사용 시 소비

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

    /// <summary>[곰은 사람을 찢어] 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해. (RandomCharacter + Tiles + range 2)</summary>
    [System.Serializable]
    public class MommyBearTear : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

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

    /// <summary>엄마곰 조건부 AI: [곰은 사람을 찢어][1] ONLY, 단 아기곰이 최근 공격받았으면(LastBabyAttacker 존재) [보호 본능][0].</summary>
    [System.Serializable]
    public class MommyBearPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            var attacker = BearPackTracker.LastBabyAttacker;
            int idx = (attacker != null && attacker.IsAlive) ? 0 : 1;
            if (idx >= availableSkills.Count) idx = 0;
            return availableSkills[idx];
        }
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
