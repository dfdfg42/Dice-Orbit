using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.CrystalStone
{
    /// <summary>[결정 방패] 살아있는 수정 핵에게 일시 방어도 armor 부여. (targetType=Self로 배선, Execute는 대상 무시)</summary>
    [System.Serializable]
    public class CrystalShieldSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int armor = 10;

        public CrystalShieldSkill()
        {
            skillName = "결정 방패";
            description = "수정 핵에게 일시 방어도 부여";
        }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var core = CrystalSet.GetCore();
            if (core != null && core.Stats != null) core.Stats.TempArmor += armor;
        }
    }

    /// <summary>[수정 창] 무작위 대상 1명이 속한 타일 + 좌우 각각 ±2칸에 damage 피해.
    /// (RandomCharacter + Tiles + range 2로 배선; CorrosiveSlimeSkill과 동형)</summary>
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
}
