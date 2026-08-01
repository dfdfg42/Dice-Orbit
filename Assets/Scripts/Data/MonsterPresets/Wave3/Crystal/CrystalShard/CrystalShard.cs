using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.CrystalShard
{
    /// <summary>[수정 비] 무작위 타일 count개에 있는 적에게 damage 피해. (RandomTiles + Tiles + count 6으로 배선)</summary>
    [System.Serializable]
    public class CrystalRainSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public CrystalRainSkill()
        {
            skillName = "수정 비";
            description = "무작위 타일 6개에 있는 적에게 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>[결정 재생] 살아있는 수정 핵의 체력 healAmount 회복. (targetType=None으로 배선, Execute는 대상 무시)</summary>
    [System.Serializable]
    public class CrystalRegenSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int healAmount = 10;

        public CrystalRegenSkill()
        {
            skillName = "결정 재생";
            description = "수정 핵의 체력 회복";
        }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var core = CrystalSet.GetCore();
            if (core != null && core.Stats != null) core.Stats.Heal(healAmount);
        }
    }
}
