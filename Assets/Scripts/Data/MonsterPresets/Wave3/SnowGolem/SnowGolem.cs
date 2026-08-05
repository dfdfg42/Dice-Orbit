using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.MonsterPresets.Wave3.SnowMan;

namespace DiceOrbit.Data.MonsterPresets.Wave3.SnowGolem
{
    /// <summary>[눈 방패] 자신을 제외한 무작위 아군 몬스터에게 일시 방어도 armor 부여.</summary>
    [System.Serializable]
    public class SnowShieldSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int armor = 5;

        public SnowShieldSkill()
        {
            skillName = "눈 방패";
            description = "자신을 제외한 무작위 아군에게 일시 방어도 부여";
        }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var others = SnowSet.OtherAliveMonsters(source as Monster).ToList();
            if (others.Count == 0) return;
            var target = others[Random.Range(0, others.Count)];
            if (target != null && target.Stats != null) target.Stats.TempArmor += armor;
        }
    }

    /// <summary>[눈 주먹] 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해. (RandomCharacter + Tiles + range 2)</summary>
    [System.Serializable]
    public class SnowFistSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;

        public SnowFistSkill()
        {
            skillName = "눈 주먹";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>눈골렘 조건부 AI: 눈방패[0] ONLY, 최초 아군 사망 시 눈주먹[1] ONLY.</summary>
    [System.Serializable]
    public class SnowGolemPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            int idx = SnowSet.HasAnyAllyDied() ? 1 : 0;
            if (idx >= availableSkills.Count) idx = 0;
            return availableSkills[idx];
        }
    }
}
