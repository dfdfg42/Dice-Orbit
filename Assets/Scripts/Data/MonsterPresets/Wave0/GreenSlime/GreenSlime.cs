using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave0.GreenSlime
{
    /// <summary>[부식성 점액] 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 damage 피해. (RandomCharacter + Tiles + range 2)</summary>
    [System.Serializable]
    public class CorrosiveSlimeSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public CorrosiveSlimeSkill()
        {
            skillName = "부식성 점액";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
