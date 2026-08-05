using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave0.BlueSlime
{
    /// <summary>[점액 분사] 설치된 점액 타일 + 좌우 각각 1칸에 damage 피해. (TilesWithAttribute=Slime + range 1)</summary>
    [System.Serializable]
    public class SlimeSpraySkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public SlimeSpraySkill()
        {
            skillName = "점액 분사";
            description = "설치된 점액 타일 + 좌우 각각 한 칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
