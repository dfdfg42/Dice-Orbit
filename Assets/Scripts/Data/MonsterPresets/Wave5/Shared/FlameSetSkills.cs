using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave5.Shared
{
    /// <summary>
    /// 타일 피해 공용 스킬 — 대상 타일(MonsterSkill 타깃팅으로 결정)에 damage 피해.
    /// 불똥별/화염 폭발(RandomCharacter+Tiles+range2), 불의 저주(RandomTiles+count6) 등에 재사용.
    /// </summary>
    [System.Serializable]
    public class FlameTileDamageSkill : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 표시 이름")]
        [SerializeField] private string skillLabel = "화염";
        [SerializeField] private int damage = 30;

        public override string SkillName => string.IsNullOrEmpty(skillLabel) ? "화염" : skillLabel;
        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// 불꽃 설치 공용 스킬 — 대상 타일(MonsterSkill 타깃팅)에 불꽃 타일 설치(피해 없음). [발화].
    /// </summary>
    [System.Serializable]
    public class PlaceFireSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private string skillLabel = "발화";
        [SerializeField] private int fireDamage = 35;

        public override string SkillName => string.IsNullOrEmpty(skillLabel) ? "발화" : skillLabel;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (targetTiles == null) return;
            foreach (var tile in targetTiles)
            {
                if (tile == null || tile.HasAttribute(TileAttributeType.Flame)) continue;
                tile.AddAttribute(new FireTile(fireDamage));
            }
        }
    }
}
