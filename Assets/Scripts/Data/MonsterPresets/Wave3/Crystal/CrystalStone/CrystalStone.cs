using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.CrystalStone
{
    /// <summary>[결정화] 살아있는 수정 핵의 수정 중첩 +stackAmount. (대상 없음, Execute가 직접 처리)</summary>
    [System.Serializable]
    public class CrystallizeSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int stackAmount = 1;

        public CrystallizeSkill()
        {
            skillName = "결정화";
            description = "수정 핵의 수정 중첩 증가";
        }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            CrystalSet.AddStack(CrystalSet.GetCore(), stackAmount);
        }
    }

    /// <summary>[수정 창] 무작위 대상 1명이 속한 타일 + 좌우 각각 ±2칸에 damage 피해. (RandomCharacter + Tiles + range 2)</summary>
    [System.Serializable]
    public class CrystalSpearSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public CrystalSpearSkill()
        {
            skillName = "수정 창";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 두 칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>수정석 조건부 AI: 수정 핵 생존 → [0]결정화 ONLY, 사망 → [1]수정 창 ONLY.</summary>
    [System.Serializable]
    public class CrystalStonePattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            int idx = CrystalSet.GetCore() != null ? 0 : 1;
            if (idx >= availableSkills.Count) idx = 0;
            return availableSkills[idx];
        }
    }
}
