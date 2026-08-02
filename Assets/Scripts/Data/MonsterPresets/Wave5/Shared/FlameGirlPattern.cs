using System.Linq;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave5.Shared
{
    /// <summary>
    /// 불꽃 소녀 AI. availableSkills 순서 = [0 발화, 1 화염구, 2 대화재].
    /// 불꽃 타일 10개↑ → 대화재(2), 아니면 발화/화염구 랜덤(0 또는 1).
    /// </summary>
    [System.Serializable]
    public class FlameGirlPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;

            var orbit = GameManager.Instance?.GetOrbitManager();
            int n = orbit == null ? 0 : orbit.Tiles.Count(t => t != null && t.HasAttribute(TileAttributeType.Flame));

            if (n >= 10 && availableSkills.Count >= 3) return availableSkills[2]; // 대화재
            return availableSkills[Random.Range(0, Mathf.Min(2, availableSkills.Count))]; // 발화/화염구
        }
    }
}
