using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// Character/Monster/TileData → 패널용 구조화 데이터 빌더 (단일 출처).
    /// 기존 Character/Monster에 복붙돼 있던 상태이상·패시브 빌드 로직을 통합한다.
    /// 서식 제로: 여기서 색/태그/접두어를 붙이지 않는다.
    /// </summary>
    public static class UnitInfoBuilder
    {
        public static UnitInfoData Build(Character ch)
        {
            var s = ch.Stats;
            return new UnitInfoData(
                name: s != null && !string.IsNullOrWhiteSpace(s.CharacterName) ? s.CharacterName : ch.name,
                currentHp: s?.CurrentHP ?? 0,
                maxHp: s?.MaxHP ?? 0,
                armor: s?.TempArmor ?? 0,
                flavorText: s?.SourcePreset != null ? (s.SourcePreset.Description ?? string.Empty).Trim() : string.Empty,
                actives: BuildActives(s),
                passives: BuildPassives(ch.Passives),
                statuses: BuildStatuses(ch.StatusEffects),
                currentTile: ch.CurrentTile != null ? Build(ch.CurrentTile) : (TileInfoData?)null);
        }

        public static UnitInfoData Build(Monster m)
        {
            var s = m.Stats;
            return new UnitInfoData(
                name: s != null && !string.IsNullOrWhiteSpace(s.MonsterName) ? s.MonsterName : m.name,
                currentHp: s?.CurrentHP ?? 0,
                maxHp: s?.MaxHP ?? 0,
                armor: s?.TempArmor ?? 0,
                flavorText: string.Empty,
                actives: System.Array.Empty<SkillInfoData>(),
                passives: BuildPassives(m.Passives),
                statuses: BuildStatuses(m.StatusEffects),
                currentTile: null);   // 몬스터는 타일 추적 없음 (Character.CurrentTile만 존재)
        }

        public static TileInfoData Build(TileData tile)
        {
            var list = new List<TileAttributeInfo>();
            foreach (var attr in tile.GetAttributes())
            {
                if (attr == null) continue;
                list.Add(new TileAttributeInfo(attr.Type, attr.Value, attr.Duration));
            }
            return new TileInfoData(tile.TileIndex, tile.Type, list);
        }

        private static IReadOnlyList<SkillInfoData> BuildActives(CharacterStats stats)
        {
            var result = new List<SkillInfoData>();
            var slots = stats?.ActiveAbilities;
            if (slots == null) return result;

            foreach (var slot in slots)
            {
                var skill = slot?.RuntimeInstance ?? slot?.BaseSkill;
                if (skill == null) continue;
                result.Add(new SkillInfoData(
                    skill.SkillName,
                    skill.requirement?.GetDescription() ?? string.Empty,
                    skill.GetDynamicDescription() ?? string.Empty,
                    slot.CurrentLevel));
            }
            return result;
        }

        private static IReadOnlyList<PassiveInfoData> BuildPassives(Systems.Passives.PassiveManager passives)
        {
            var result = new List<PassiveInfoData>();
            if (passives?.ActivePassives == null) return result;

            foreach (var p in passives.ActivePassives)
            {
                if (p == null) continue;
                result.Add(new PassiveInfoData(
                    string.IsNullOrWhiteSpace(p.PassiveName) ? "Unknown Passive" : p.PassiveName,
                    p.CurrentLevel,
                    p.GetDynamicDescription() ?? string.Empty,
                    (p.Description ?? string.Empty).Trim()));
            }
            return result;
        }

        private static IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData> BuildStatuses(
            Systems.Effects.StatusEffectManager statusEffects)
        {
            var result = new List<TooltipKeywordFormatter.StatusDisplayData>();
            var effects = statusEffects?.GetActiveEffects();
            if (effects == null) return result;

            foreach (var e in effects)
            {
                if (e == null) continue;
                result.Add(TooltipKeywordFormatter.BuildStatusDisplayData(e.Type.ToString(), e.Value, e.Duration));
            }
            return result;
        }
    }
}
