using System.Collections.Generic;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Core.Forecast
{
    /// <summary>예고 카드의 한 줄.</summary>
    public readonly struct ForecastLine
    {
        public readonly string Text;
        public readonly ForecastTone Tone;
        /// <summary>제목 줄인가 (굵고 크게).</summary>
        public readonly bool Title;

        public ForecastLine(string text, ForecastTone tone, bool title = false)
        {
            Text = text;
            Tone = tone;
            Title = title;
        }
    }

    /// <summary>
    /// 행동 예고의 문구 (순수 — ActionForecastSelfTests). 카드 줄과 몬스터 위 꼬리표가 같은 결과에서 나온다.
    /// </summary>
    public static class ActionForecastText
    {
        /// <summary>카드 전체 — 공격 종류 → 대상 → 콤보 → 경로 → 턴 종료 → 위험 순.</summary>
        public static void BuildCardLines(ActionForecast forecast, List<ForecastLine> lines)
        {
            lines.Clear();
            if (forecast == null) return;

            lines.Add(new ForecastLine(AttackTitle(forecast), TitleTone(forecast.Kind), title: true));
            if (forecast.Kind == ForecastAttackKind.NoTarget)
                lines.Add(new ForecastLine("사거리에 몬스터가 없습니다", ForecastTone.Bad));

            foreach (var target in forecast.Targets)
                lines.Add(new ForecastLine(TargetLine(target), target.Lethal ? ForecastTone.Good : ForecastTone.Neutral));

            string combo = ComboLine(forecast);
            if (!string.IsNullOrEmpty(combo))
                lines.Add(new ForecastLine(combo, ComboTone(forecast.ComboChange)));

            foreach (var note in forecast.PathNotes)
                lines.Add(new ForecastLine("경로: " + NoteText(note), note.Tone));
            foreach (var note in forecast.EndTurnNotes)
                lines.Add(new ForecastLine("턴 종료: " + NoteText(note), note.Tone));
            foreach (var threat in forecast.Threats)
                lines.Add(new ForecastLine(ThreatLine(threat), threat.IsAttack ? ForecastTone.Bad : ForecastTone.Neutral));
        }

        public static string AttackTitle(ActionForecast forecast)
        {
            switch (forecast.Kind)
            {
                case ForecastAttackKind.Combo: return $"{forecast.ComboStage + 1}단계 [{forecast.AttackName}]";
                case ForecastAttackKind.Basic: return "기본 공격";
                case ForecastAttackKind.NoTarget: return "공격 없음";
                default: return "이동만 (이번 턴엔 이미 공격함)";
            }
        }

        private static ForecastTone TitleTone(ForecastAttackKind kind)
            => kind == ForecastAttackKind.NoTarget ? ForecastTone.Bad : ForecastTone.Neutral;

        /// <summary>대상 한 줄 — "초록 슬라임  -18 (9×2) · 방어도 -4 · 2 남음".</summary>
        public static string TargetLine(ForecastTargetResult target)
        {
            var parts = new List<string>();

            if (target.HpLoss > 0)
                parts.Add(target.Hits > 1 ? $"-{target.HpLoss} ({target.DamagePerHit}×{target.Hits})" : $"-{target.HpLoss}");
            else if (target.ArmorLoss == 0)
                parts.Add("피해 없음");

            if (target.ArmorLoss > 0) parts.Add($"방어도 -{target.ArmorLoss}");
            if (target.Lethal) parts.Add("처치");
            else if (target.HpLoss > 0) parts.Add($"{target.HpAfter} 남음");
            if (target.DodgeChance > 0f) parts.Add($"회피 {target.DodgeChance:0.#}%");

            return $"{target.Name}  {string.Join(" · ", parts)}";
        }

        /// <summary>몬스터 위에 띄우는 짧은 꼬리표 — "-18", "-20 처치", "방어도 -5", "0".</summary>
        public static string TargetTag(ForecastTargetResult target)
        {
            if (target.Lethal) return $"-{target.HpLoss} 처치";
            if (target.HpLoss > 0) return $"-{target.HpLoss}";
            if (target.ArmorLoss > 0) return $"방어도 -{target.ArmorLoss}";
            return "0";
        }

        public static string ComboLine(ActionForecast forecast)
        {
            string gate = forecast.GateCondition;
            switch (forecast.ComboChange)
            {
                case ForecastComboChange.Advance:
                    return $"콤보 이어짐 → 다음은 {forecast.ComboStage + 2}단계";
                case ForecastComboChange.Finish:
                    return "콤보 완성 → 다음은 1단계부터";
                case ForecastComboChange.BrokenByDice:
                    return string.IsNullOrEmpty(gate) ? "콤보 끊김" : $"콤보 끊김 — {gate} 필요";
                case ForecastComboChange.BrokenByNoTarget:
                    return "콤보 초기화 — 때릴 대상이 없음";
            }

            // 끊길 콤보가 없는 기본공격 — 어떤 주사위면 강화 공격이 나가는지 알려 준다
            bool basicWithoutCombo = forecast.Kind == ForecastAttackKind.Basic
                || (forecast.Kind == ForecastAttackKind.NoTarget && forecast.ComboStage < 0);
            if (basicWithoutCombo && !string.IsNullOrEmpty(gate)) return $"강화 공격 조건: {gate}";
            return string.Empty;
        }

        private static ForecastTone ComboTone(ForecastComboChange change)
        {
            switch (change)
            {
                case ForecastComboChange.Advance:
                case ForecastComboChange.Finish:
                    return ForecastTone.Good;
                case ForecastComboChange.BrokenByDice:
                case ForecastComboChange.BrokenByNoTarget:
                    return ForecastTone.Bad;
                default:
                    return ForecastTone.Neutral;
            }
        }

        public static string NoteText(ForecastNote note)
            => note.Count > 1 ? $"{note.Text} ×{note.Count}" : note.Text;

        public static string ThreatLine(ForecastThreat threat)
        {
            string head = threat.IsAttack ? "위험" : "예고";
            return string.IsNullOrEmpty(threat.SkillName)
                ? $"{head}: {threat.MonsterName}의 행동 범위"
                : $"{head}: {threat.MonsterName} [{threat.SkillName}] 범위";
        }
    }
}
