using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Forecast;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 행동 예고 순수 로직 자가 테스트 (행동 예고 2026-10-04). 씬·플레이 모드 불필요.
    /// 메뉴 [DiceOrbit → Run ActionForecast Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// 실제 전투 수치와의 대조(예고 = 실행 결과)는 Play 검증에서 한다.
    /// </summary>
    public static class ActionForecastSelfTests
    {
        private static int _failures;

        [MenuItem("DiceOrbit/Run ActionForecast Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;

            TestResolveDamageMatchesTakeDamage();
            TestApplyHitAccumulates();
            TestApplyHitThroughArmor();
            TestApplyHitRespectsInvulnerable();
            TestTargetLines();
            TestTitlesAndComboLines();
            TestCardOrderAndNotes();
            TestTileForecastNotes();
            TestTileAttributeForecasts();
            TestSimulationScopeRestores();
            TestHpBarForecastRange();

            if (_failures == 0) Debug.Log("[ActionForecastSelfTests] 전부 통과");
            else Debug.LogError($"[ActionForecastSelfTests] 실패 {_failures}건");
            return _failures == 0;
        }

        // ── 피해 적용 ─────────────────────────────────────────

        private static void TestResolveDamageMatchesTakeDamage()
        {
            // 순수 함수와 실제 TakeDamage가 같은 결과를 내는지 — 방어도 유무·초과 피해·무적 조합
            int[][] cases =
            {
                new[] { 10, 0, 30, 0 },    // 피해, 방어도, 체력, 무적
                new[] { 10, 4, 30, 0 },
                new[] { 3, 8, 30, 0 },
                new[] { 50, 5, 20, 0 },
                new[] { 50, 0, 20, 1 },
                new[] { 0, 5, 20, 0 },
            };
            foreach (var c in cases)
            {
                var stats = new UnitStats { MaxHP = 100, CurrentHP = c[2], TempArmor = c[1], Invulnerable = c[3] == 1 };
                UnitStats.ResolveDamage(c[0], c[1], c[2], c[3] == 1, out int absorbed, out int hpDamage);
                int returned = stats.TakeDamage(c[0]);

                string label = $"피해 {c[0]} / 방어도 {c[1]} / 체력 {c[2]} / 무적 {c[3]}";
                Check(returned == hpDamage, $"{label}: TakeDamage 반환값 = 예고의 체력 피해");
                Check(stats.TempArmor == c[1] - absorbed, $"{label}: 남은 방어도가 같다");
                Check(stats.CurrentHP == Mathf.Max(0, c[2] - hpDamage), $"{label}: 남은 체력이 같다");
            }
        }

        private static void TestApplyHitAccumulates()
        {
            var r = new ForecastTargetResult { Name = "슬라임", HpBefore = 20, HpAfter = 20 };
            ActionForecaster.ApplyHit(r, 7);
            ActionForecaster.ApplyHit(r, 7);
            Check(r.Hits == 2 && r.DamagePerHit == 7, "다단 타격 횟수와 1타 피해");
            Check(r.HpLoss == 14 && r.HpAfter == 6 && !r.Lethal, "두 번 맞으면 14 손실, 6 남음");

            ActionForecaster.ApplyHit(r, 7);
            Check(r.HpAfter == 0 && r.Lethal && r.HpLoss == 20, "초과 피해는 체력 0에서 멈춘다 (손실 = 남아 있던 체력)");
        }

        private static void TestApplyHitThroughArmor()
        {
            var r = new ForecastTargetResult { HpBefore = 20, HpAfter = 20, ArmorBefore = 10, ArmorAfter = 10 };
            ActionForecaster.ApplyHit(r, 6);
            Check(r.ArmorLoss == 6 && r.HpLoss == 0, "방어도가 전부 흡수");
            ActionForecaster.ApplyHit(r, 6);
            Check(r.ArmorLoss == 10 && r.HpLoss == 2, "남은 방어도 4를 뚫고 2가 체력에");
        }

        private static void TestApplyHitRespectsInvulnerable()
        {
            var r = new ForecastTargetResult { HpBefore = 5, HpAfter = 5, Invulnerable = true };
            ActionForecaster.ApplyHit(r, 99);
            Check(r.HpAfter == 1 && !r.Lethal, "무적 대상은 체력 1에서 멈춘다 — 처치로 예고하지 않는다");
        }

        // ── 문구 ──────────────────────────────────────────────

        private static void TestTargetLines()
        {
            var single = new ForecastTargetResult { Name = "초록 슬라임", HpBefore = 20, HpAfter = 2, Hits = 1, DamagePerHit = 18 };
            Check(ActionForecastText.TargetLine(single) == "초록 슬라임  -18 · 2 남음", "단타 줄");
            Check(ActionForecastText.TargetTag(single) == "-18", "단타 꼬리표");

            var multi = new ForecastTargetResult { Name = "고블린", HpBefore = 30, HpAfter = 12, Hits = 2, DamagePerHit = 9 };
            Check(ActionForecastText.TargetLine(multi) == "고블린  -18 (9×2) · 12 남음", "다단 줄");

            var kill = new ForecastTargetResult { Name = "슬라임", HpBefore = 10, HpAfter = 0, Hits = 1, DamagePerHit = 14 };
            Check(ActionForecastText.TargetLine(kill) == "슬라임  -10 · 처치", "처치 줄 — 손실은 남아 있던 체력만큼");
            Check(ActionForecastText.TargetTag(kill) == "-10 처치", "처치 꼬리표");

            var armored = new ForecastTargetResult { Name = "해골", HpBefore = 20, HpAfter = 18, ArmorBefore = 6, ArmorAfter = 0, Hits = 1, DamagePerHit = 8 };
            Check(ActionForecastText.TargetLine(armored) == "해골  -2 · 방어도 -6 · 18 남음", "방어도를 뚫는 줄");

            var blocked = new ForecastTargetResult { Name = "해골", HpBefore = 20, HpAfter = 20, ArmorBefore = 9, ArmorAfter = 4, Hits = 1, DamagePerHit = 5 };
            Check(ActionForecastText.TargetLine(blocked) == "해골  방어도 -5", "방어도에 전부 막히는 줄");
            Check(ActionForecastText.TargetTag(blocked) == "방어도 -5", "막힘 꼬리표");

            var nothing = new ForecastTargetResult { Name = "허수아비", HpBefore = 20, HpAfter = 20, Hits = 1, DamagePerHit = 0 };
            Check(ActionForecastText.TargetLine(nothing) == "허수아비  피해 없음", "피해 0 줄");
            Check(ActionForecastText.TargetTag(nothing) == "0", "피해 0 꼬리표");

            var dodgy = new ForecastTargetResult { Name = "박쥐", HpBefore = 20, HpAfter = 15, Hits = 1, DamagePerHit = 5, DodgeChance = 25f };
            Check(ActionForecastText.TargetLine(dodgy) == "박쥐  -5 · 15 남음 · 회피 25%", "회피는 수치에 넣지 않고 알린다");
        }

        private static void TestTitlesAndComboLines()
        {
            var combo = new ActionForecast { Kind = ForecastAttackKind.Combo, ComboStage = 1, AttackName = "지진파", ComboChange = ForecastComboChange.Advance, GateCondition = "주사위 눈 4 이상" };
            Check(ActionForecastText.AttackTitle(combo) == "2단계 [지진파]", "강화 공격 제목");
            Check(ActionForecastText.ComboLine(combo) == "콤보 이어짐 → 다음은 3단계", "콤보 전진");

            var finish = new ActionForecast { Kind = ForecastAttackKind.Combo, ComboStage = 2, AttackName = "대지 분쇄", ComboChange = ForecastComboChange.Finish };
            Check(ActionForecastText.ComboLine(finish) == "콤보 완성 → 다음은 1단계부터", "콤보 완성");

            var broken = new ActionForecast { Kind = ForecastAttackKind.Basic, ComboChange = ForecastComboChange.BrokenByDice, GateCondition = "주사위 눈 4 이상" };
            Check(ActionForecastText.AttackTitle(broken) == "기본 공격", "기본 공격 제목");
            Check(ActionForecastText.ComboLine(broken) == "콤보 끊김 — 주사위 눈 4 이상 필요", "콤보 끊김 — 조건을 알려 준다");

            var plain = new ActionForecast { Kind = ForecastAttackKind.Basic, GateCondition = "홀수 눈" };
            Check(ActionForecastText.ComboLine(plain) == "강화 공격 조건: 홀수 눈", "끊길 콤보가 없으면 조건만 안내");

            var noGate = new ActionForecast { Kind = ForecastAttackKind.Basic };
            Check(ActionForecastText.ComboLine(noGate) == string.Empty, "강화 공격이 없는 캐릭터는 콤보 줄이 없다");

            var noTarget = new ActionForecast { Kind = ForecastAttackKind.NoTarget, ComboStage = 1, ComboChange = ForecastComboChange.BrokenByNoTarget, GateCondition = "짝수 눈" };
            Check(ActionForecastText.AttackTitle(noTarget) == "공격 없음", "표적 없음 제목");
            Check(ActionForecastText.ComboLine(noTarget) == "콤보 초기화 — 때릴 대상이 없음", "표적 없어 콤보 초기화");

            var gateOkNoTargetNoCombo = new ActionForecast { Kind = ForecastAttackKind.NoTarget, ComboStage = 0, GateCondition = "짝수 눈" };
            Check(ActionForecastText.ComboLine(gateOkNoTargetNoCombo) == string.Empty, "조건은 맞았고 끊길 콤보도 없으면 콤보 줄 없음");

            var done = new ActionForecast { Kind = ForecastAttackKind.AlreadyAttacked };
            Check(ActionForecastText.AttackTitle(done) == "이동만 (이번 턴엔 이미 공격함)", "이미 공격함 제목");
        }

        private static void TestCardOrderAndNotes()
        {
            var forecast = new ActionForecast { Kind = ForecastAttackKind.Combo, ComboStage = 0, AttackName = "균열 베기", ComboChange = ForecastComboChange.Advance };
            forecast.Targets.Add(new ForecastTargetResult { Name = "슬라임", HpBefore = 10, HpAfter = 0, Hits = 1, DamagePerHit = 12 });
            forecast.PathNotes.Add(new ForecastNote("지뢰 피해 5", ForecastTone.Bad, 2));
            forecast.EndTurnNotes.Add(new ForecastNote("속박: 다음 턴 이동 불가", ForecastTone.Bad));
            forecast.Threats.Add(new ForecastThreat { MonsterName = "초록 슬라임", SkillName = "박치기", IsAttack = true });
            forecast.Threats.Add(new ForecastThreat { MonsterName = "점액 슬라임", SkillName = "점액", IsAttack = false });

            var lines = new List<ForecastLine>();
            ActionForecastText.BuildCardLines(forecast, lines);

            Check(lines.Count == 7, $"줄 수 7 (제목·대상·콤보·경로·턴 종료·위험·예고) — 실제 {lines.Count}");
            if (lines.Count != 7) return;
            Check(lines[0].Title && lines[0].Text == "1단계 [균열 베기]", "첫 줄은 제목");
            Check(lines[1].Text == "슬라임  -10 · 처치" && lines[1].Tone == ForecastTone.Good, "처치 줄은 좋은 소식");
            Check(lines[2].Tone == ForecastTone.Good, "콤보 전진은 좋은 소식");
            Check(lines[3].Text == "경로: 지뢰 피해 5 ×2" && lines[3].Tone == ForecastTone.Bad, "경로 노트는 횟수를 붙인다");
            Check(lines[4].Text == "턴 종료: 속박: 다음 턴 이동 불가", "턴 종료 노트");
            Check(lines[5].Text == "위험: 초록 슬라임 [박치기] 범위" && lines[5].Tone == ForecastTone.Bad, "공격 범위는 위험");
            Check(lines[6].Text == "예고: 점액 슬라임 [점액] 범위" && lines[6].Tone == ForecastTone.Neutral, "공격이 아닌 행동은 예고");

            var empty = new ActionForecast { Kind = ForecastAttackKind.NoTarget };
            ActionForecastText.BuildCardLines(empty, lines);
            Check(lines.Count == 2 && lines[1].Tone == ForecastTone.Bad, "표적 없음은 제목 + 이유 한 줄");
        }

        // ── 타일 예고 ─────────────────────────────────────────

        private static void TestTileForecastNotes()
        {
            var f = new TileForecast();
            f.Note("지뢰 피해 5", ForecastTone.Bad);
            f.Note("지뢰 피해 5", ForecastTone.Bad);
            f.Note("체력 +3", ForecastTone.Good);
            f.Note("", ForecastTone.Good);
            Check(f.Notes.Count == 2 && f.Notes[0].Count == 2 && f.Notes[1].Count == 1, "같은 문구는 횟수로 합친다, 빈 문구는 버린다");

            f.GrantStatus(EffectType.Catalyst, 25);
            f.GrantStatus(EffectType.Catalyst, 25);
            Check(f.GrantedStatuses.Count == 1 && f.HasPendingStatus(EffectType.Catalyst), "같은 상태는 한 번만 얻는다");
            Check(!f.HasPendingStatus(EffectType.Shock), "얻지 않은 상태는 없다고 답한다");

            f.MarkConsumed();
            Check(f.Consumed, "소모 표시");
            f.BeginAttribute();
            Check(!f.Consumed, "다음 속성을 묻기 전에 소모 표시가 풀린다");
        }

        private static void TestTileAttributeForecasts()
        {
            // 지뢰: 지나가면 터지고 사라진다 → 턴 종료 예고에서는 빠져야 한다
            var mine = new RandMineTile(TileAttributeType.RandMine, 5, -1);
            var traverse = new TileForecast();
            traverse.BeginAttribute();
            mine.ForecastTraverse(null, traverse);
            Check(traverse.Consumed && traverse.Notes.Count == 1 && traverse.Notes[0].Text == "지뢰 피해 5" && traverse.Notes[0].Tone == ForecastTone.Bad,
                "지뢰 통과 예고 + 소모");

            // 불꽃: 한 턴에 하나만 끈다 — 같은 경로의 두 번째 불꽃은 그대로 남는다
            var fire1 = new FireTile(20);
            var fire2 = new FireTile(20);
            var path = new TileForecast();
            path.BeginAttribute(); fire1.ForecastTraverse(null, path);
            bool firstConsumed = path.Consumed;
            path.BeginAttribute(); fire2.ForecastTraverse(null, path);
            Check(firstConsumed && !path.Consumed, "불꽃은 첫 번째만 꺼진다");
            Check(path.Notes.Count == 1 && path.Notes[0].Count == 1, "불꽃 끄기 노트는 한 번");

            var endTurn = new TileForecast();
            fire2.ForecastEndTurn(null, endTurn);
            Check(endTurn.Notes.Count == 1 && endTurn.Notes[0].Text == "불꽃 피해 20", "남은 불꽃 위에서 턴을 마치면 피해");

            // 단단함: 통과·턴 종료 둘 다
            var sturdy = new SturdyTile(10);
            var a = new TileForecast(); sturdy.ForecastTraverse(null, a);
            var b = new TileForecast(); sturdy.ForecastEndTurn(null, b);
            Check(a.Notes.Count == 1 && b.Notes.Count == 1 && a.Notes[0].Tone == ForecastTone.Good && !a.Consumed, "단단함은 통과·턴 종료 모두 방어도, 사라지지 않는다");

            // 예고 훅이 없는 속성(예리함)은 아무것도 적지 않는다 — 효과는 피해 수치에 이미 들어간다
            var sharp = new SharpTile(10);
            var c = new TileForecast(); sharp.ForecastTraverse(null, c); sharp.ForecastEndTurn(null, c);
            Check(c.Notes.Count == 0, "예리함은 노트가 없다");
        }

        // ── 스코프 ────────────────────────────────────────────

        private static void TestSimulationScopeRestores()
        {
            Systems.Effects.AttackActionScope.ResetAll();
            Check(!Systems.Effects.AttackActionScope.IsActive, "처음엔 스코프 없음");

            using (Systems.Effects.AttackActionScope.Simulate(null, new Systems.Effects.AttackActionInfo { DiceValue = 4, ComboStage = 1, IsAutoAttack = true }))
            {
                Check(Systems.Effects.AttackActionScope.IsActive, "시뮬레이션 중에는 스코프가 열려 있다");
                Check(Systems.Effects.AttackActionScope.CurrentInfo.DiceValue == 4 && Systems.Effects.AttackActionScope.CurrentInfo.ComboStage == 1, "시뮬레이션 정보가 보인다");
            }
            Check(!Systems.Effects.AttackActionScope.IsActive, "끝나면 스코프 없음으로 돌아간다");

            // 진행 중인 실제 행동 안에서 시뮬레이션해도 실제 행동이 그대로 남는다
            Systems.Effects.AttackActionScope.Begin(null, new Systems.Effects.AttackActionInfo { DiceValue = 6, ComboStage = 2 });
            int realId = Systems.Effects.AttackActionScope.CurrentActionId;
            using (Systems.Effects.AttackActionScope.Simulate(null, new Systems.Effects.AttackActionInfo { DiceValue = 1 }))
                Check(Systems.Effects.AttackActionScope.CurrentInfo.DiceValue == 1, "안쪽은 시뮬레이션 값");
            Check(Systems.Effects.AttackActionScope.CurrentActionId == realId && Systems.Effects.AttackActionScope.CurrentInfo.DiceValue == 6,
                "바깥 실제 행동의 ID·정보가 복원된다");
            Systems.Effects.AttackActionScope.End();
        }

        // ── 체력바의 깎일 구간 ────────────────────────────────

        private static void TestHpBarForecastRange()
        {
            UI.MonsterUI.ForecastRange(20, 20, 5, out float from, out float to);
            Check(Mathf.Approximately(from, 0.75f) && Mathf.Approximately(to, 1f), "체력 20/20에서 5 → 오른쪽 끝 25%");

            UI.MonsterUI.ForecastRange(10, 20, 4, out from, out to);
            Check(Mathf.Approximately(from, 0.3f) && Mathf.Approximately(to, 0.5f), "체력 10/20에서 4 → 30%~50%");

            UI.MonsterUI.ForecastRange(6, 20, 99, out from, out to);
            Check(from == 0f && Mathf.Approximately(to, 0.3f), "처치(초과 피해)는 남은 체력 전부");

            UI.MonsterUI.ForecastRange(10, 20, 0, out from, out to);
            Check(from == 0f && to == 0f, "피해 0이면 구간 없음");

            UI.MonsterUI.ForecastRange(0, 20, 5, out from, out to);
            Check(from == 0f && to == 0f, "이미 쓰러진 대상은 구간 없음");

            UI.MonsterUI.ForecastRange(5, 0, 3, out from, out to);
            Check(from == 0f && to == 0f, "최대 체력 0이어도 예외 없이 구간 없음");
        }

        // ── 도우미 ────────────────────────────────────────────

        private static void Check(bool condition, string what)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[ActionForecastSelfTests] FAIL: " + what);
        }
    }
}
