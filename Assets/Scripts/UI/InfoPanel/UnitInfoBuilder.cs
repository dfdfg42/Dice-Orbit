using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
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
                activesLabel: "액티브",
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
                activesLabel: "다음 행동",
                actives: BuildMonsterIntent(m),
                passives: BuildPassives(m.Passives),
                statuses: BuildStatuses(m.StatusEffects),
                currentTile: null);   // 몬스터는 타일 추적 없음 (Character.CurrentTile만 존재)
        }

        /// <summary>
        /// 몬스터의 다음 행동(인텐트)을 액티브 섹션용 항목으로 빌드.
        /// 스킬명 + 대상 + 설명 + 예상 피해(파이프라인 시뮬레이션 — 패시브/모디파이어 반영).
        /// </summary>
        private static IReadOnlyList<SkillInfoData> BuildMonsterIntent(Monster m)
        {
            var result = new List<SkillInfoData>();
            var data = m.NextSkill?.skillData;
            if (data == null) return result;

            // 대상 이름들 (메타 자리에 표시)
            string targetsText = string.Empty;
            var targets = m.CurrentIntent?.Targets;
            if (targets != null && targets.Count > 0)
            {
                var names = new List<string>();
                foreach (var t in targets)
                {
                    if (t == null) continue;
                    names.Add(t.Stats is CharacterStats cs ? cs.CharacterName
                            : t.Stats is MonsterStats ms ? ms.MonsterName
                            : t.name);
                }
                if (names.Count > 0) targetsText = "대상: " + string.Join(", ", names);
            }

            // 설명 + 예상 피해 (구 몬스터 툴팁의 시뮬레이션 로직 계승)
            string desc = (data.Description ?? string.Empty).Trim();
            int previewBase = data.GetPreviewDamage();
            if (previewBase > 0)
            {
                int shown = previewBase;
                var repTarget = ResolvePreviewTarget(m);
                if (repTarget != null && CombatPipeline.Instance != null)
                {
                    var simCtx = new AttackContext(m, repTarget, data.SkillName, previewBase);
                    shown = CombatPipeline.Instance.SimulateCalculation(simCtx);
                }
                desc = string.IsNullOrEmpty(desc) ? $"예상 피해: {shown}" : $"{desc}\n예상 피해: {shown}";
            }

            result.Add(new SkillInfoData(data.SkillName, targetsText, desc, 0));
            return result;
        }

        /// <summary>인텐트 예상 피해 시뮬레이션에 쓸 대표 대상. 의도 대상이 있으면 그 대상, 없으면 생존 파티원.</summary>
        private static Unit ResolvePreviewTarget(Monster m)
        {
            var intent = m.CurrentIntent;
            if (intent != null)
            {
                if (intent.Targets != null)
                {
                    foreach (var t in intent.Targets)
                        if (t != null && t.IsAlive) return t;
                }

                if (intent.TargetTiles != null)
                {
                    foreach (var tile in intent.TargetTiles)
                    {
                        var chars = tile != null ? tile.GetCharactersOnTile() : null;
                        if (chars == null) continue;
                        foreach (var c in chars)
                            if (c != null && c.IsAlive) return c;
                    }
                }
            }

            var alive = PartyManager.Instance?.GetAliveCharacters();
            return (alive != null && alive.Count > 0) ? alive[0] : null;
        }

        public static TileInfoData Build(TileData tile)
        {
            var list = new List<TileAttributeInfo>();
            foreach (var attr in tile.GetAttributes())
            {
                if (attr == null) continue;
                list.Add(new TileAttributeInfo(
                    attr.Type, attr.Value, attr.Duration,
                    attr.GetDisplayName(),      // 한국어 이름 (서브클래스가 제공)
                    attr.GetDescription()));    // 효과 설명 (Bone/Reagent/Honey 등이 오버라이드)
            }
            return new TileInfoData(tile.TileIndex, tile.Type, list);
        }

        // ── 커서 요약 툴팁 (한눈 요약 — 상세는 정보 패널 몫) ─────────────

        /// <summary>캐릭터 호버 요약: 이름+HP 한 줄, 상태이상/스택 요약 한 줄 (없으면 생략).</summary>
        public static string BuildHoverSummary(Character ch)
        {
            var s = ch.Stats;
            if (s == null) return ch.name;

            string name = !string.IsNullOrWhiteSpace(s.CharacterName) ? s.CharacterName : ch.name;
            string line1 = $"{name}  HP {s.CurrentHP}/{s.MaxHP}" + (s.TempArmor > 0 ? $"  방어도 {s.TempArmor}" : "");

            var statuses = BuildStatuses(ch.StatusEffects);
            if (statuses.Count == 0) return line1;

            var parts = new List<string>(statuses.Count);
            foreach (var st in statuses)
            {
                string part = st.Name;
                if (!string.IsNullOrEmpty(st.StackText)) part += $" {st.StackText}";
                if (!string.IsNullOrEmpty(st.DurationText)) part += $" {st.DurationText}";
                parts.Add(part);
            }
            return $"{line1}\n{string.Join(" · ", parts)}";
        }

        /// <summary>몬스터 호버 요약: 이름+HP 한 줄, 다음 행동 → 대상 (예상 피해) 한 줄 (인텐트 없으면 생략).</summary>
        public static string BuildHoverSummary(Monster m)
        {
            var s = m.Stats;
            string name = s != null && !string.IsNullOrWhiteSpace(s.MonsterName) ? s.MonsterName : m.name;
            string line1 = $"{name}  HP {s?.CurrentHP ?? 0}/{s?.MaxHP ?? 0}";

            var data = m.NextSkill?.skillData;
            if (data == null) return line1;

            string line2 = data.SkillName;

            // 대상 이름들
            var targets = m.CurrentIntent?.Targets;
            if (targets != null && targets.Count > 0)
            {
                var names = new List<string>();
                foreach (var t in targets)
                {
                    if (t == null) continue;
                    names.Add(t.Stats is CharacterStats cs ? cs.CharacterName
                            : t.Stats is MonsterStats ms ? ms.MonsterName
                            : t.name);
                }
                if (names.Count > 0) line2 += $" → {string.Join(", ", names)}";
            }

            // 예상 피해 (파이프라인 시뮬레이션 — 패시브/모디파이어 반영)
            int previewBase = data.GetPreviewDamage();
            if (previewBase > 0)
            {
                int shown = previewBase;
                var repTarget = ResolvePreviewTarget(m);
                if (repTarget != null && CombatPipeline.Instance != null)
                {
                    var simCtx = new AttackContext(m, repTarget, data.SkillName, previewBase);
                    shown = CombatPipeline.Instance.SimulateCalculation(simCtx);
                }
                line2 += $" (예상 {shown})";
            }

            // 스킬 설명 (은은한 회색으로 한 줄)
            string desc = (data.Description ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(desc))
                return $"{line1}\n{line2}\n<color=#B3B3B3>{desc}</color>";

            return $"{line1}\n{line2}";
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
                    skill.FormatDiceCondition(),   // 한국어 문구 ("주사위 4 이상" 등) — GetDescription()은 영문 디버그용
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
