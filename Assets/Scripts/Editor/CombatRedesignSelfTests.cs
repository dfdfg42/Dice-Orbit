using System.Linq;
using System.Reflection;
using DiceOrbit.Core;
using DiceOrbit.Core.Combo;
using DiceOrbit.Data.CharacterActives;
using DiceOrbit.Data.Passives;
using DiceOrbit.Systems.Effects;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 2026-08-28 콤보/상태 개편의 순수 로직 자가 테스트.
    /// 씬·플레이 모드 없이 도는 검증만 모았다 — 파이프라인 통합(방진 감쇄, 촉매 소모 등)은 플레이로 확인.
    /// 메뉴 [DiceOrbit → Run Combat Redesign Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// </summary>
    public static class CombatRedesignSelfTests
    {
        private static int _failures;

        [MenuItem("DiceOrbit/Run Combat Redesign Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;

            TestComboTransitions();
            TestComboIndependence();
            TestPoisonSequence();
            TestCircularDistance();
            TestActionScopeBookkeeping();
            TestStageTables();
            TestSelectionActiveSummaries();

            if (_failures == 0) Debug.Log("[SelfTest] 전체 PASS — 콤보/중독/구역 wrap/스코프/단계표");
            else Debug.LogError($"[SelfTest] 실패 {_failures}건 — 위 로그 확인");
            return _failures == 0;
        }

        private static void Check(bool condition, string label)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[SelfTest] FAIL: " + label);
        }

        private static void TestComboTransitions()
        {
            var t = new ComboTracker();
            Check(t.RegisterGateSuccess() == 0, "첫 게이트 성공은 1단계(0)");
            Check(t.ConfirmExecuted() == ComboOutcome.Advanced && t.Stage == 1, "1단계 후 전진");
            Check(t.RegisterGateSuccess() == 1, "둘째 게이트 성공은 2단계(1)");
            Check(t.ConfirmExecuted() == ComboOutcome.Advanced && t.Stage == 2, "2단계 후 전진");
            Check(t.RegisterGateSuccess() == 2, "셋째 게이트 성공은 3단계(2)");
            Check(t.ConfirmExecuted() == ComboOutcome.Finished && t.Stage == 0, "3단계 후 0으로 순환");

            t.ConfirmExecuted();
            t.Reset();
            Check(t.Stage == 0, "Reset은 0으로");
        }

        private static void TestComboIndependence()
        {
            var a = new ComboTracker();
            var b = new ComboTracker();
            a.ConfirmExecuted();
            a.ConfirmExecuted();
            Check(a.Stage == 2 && b.Stage == 0, "트래커는 서로 독립");
        }

        private static void TestPoisonSequence()
        {
            var p = new PoisonStatus(6);
            Check(p.Duration == -1, "중독은 지속시간 없음(-1)");

            string sequence = "";
            for (int i = 0; i < 6; i++) sequence += p.ConsumeTick() + (i < 5 ? "," : "");
            Check(sequence == "6,5,4,3,2,1", "중독 틱 수열 6,5,4,3,2,1 (실제: " + sequence + ")");
            Check(p.Value == 0, "6틱 후 잔여 0");
            Check(p.ConsumeTick() == 0, "0중첩 틱은 0");

            // 합산: 기존 4 + 추가 3 = 7 (StatusEffect.AddStack 경로)
            var q = new PoisonStatus(4);
            q.AddStack(3);
            Check(q.Value == 7, "중독 합산 4+3=7");

            // 2배 로직(죽음의 촉매): 현재치만큼 추가 = 정확히 2배
            var r = new PoisonStatus(5);
            r.AddStack(r.Value);
            Check(r.Value == 10, "중독 2배 5→10");
        }

        private static void TestCircularDistance()
        {
            Check(MageCircuit.CircularDistance(0, 3, 4) == 1, "n=4에서 0↔3 거리 1 (wrap)");
            Check(MageCircuit.CircularDistance(3, 0, 4) == 1, "wrap 대칭");
            Check(MageCircuit.CircularDistance(0, 2, 4) == 2, "맞은편 거리 2");
            Check(MageCircuit.CircularDistance(2, 2, 4) == 0, "자기 구역 거리 0");
        }

        private static void TestActionScopeBookkeeping()
        {
            AttackActionScope.ResetAll();
            Check(!AttackActionScope.IsActive, "초기 비활성");

            AttackActionScope.Begin(null);
            int first = AttackActionScope.CurrentActionId;
            Check(AttackActionScope.IsActive && first != 0, "Begin 후 활성");

            AttackActionScope.End();
            Check(!AttackActionScope.IsActive, "End 후 비활성");

            AttackActionScope.Begin(null);
            Check(AttackActionScope.CurrentActionId > first, "행동 ID 단조 증가");
            AttackActionScope.ResetAll();
            Check(!AttackActionScope.IsActive, "ResetAll 후 비활성");
        }

        private static void TestStageTables()
        {
            var warrior = new WarriorGreatswordActive();
            Check(Mathf.Approximately(warrior.GetStageMultiplier(0), 1.5f)
               && Mathf.Approximately(warrior.GetStageMultiplier(1), 1.1f)
               && Mathf.Approximately(warrior.GetStageMultiplier(2), 1.8f), "전사 배율 150/110/180%");
            Check(warrior.GetStageHits(0) == 1 && warrior.GetStageHits(2) == 1, "전사는 단타");

            var rogue = new RogueAmbushActive();
            Check(rogue.GetStageHits(0) == 2 && rogue.GetStageHits(1) == 3 && rogue.GetStageHits(2) == 5, "도적 타수 2/3/5");
            Check(Mathf.Approximately(rogue.GetStageMultiplier(0) * rogue.GetStageHits(0), 1.4f), "쌍비수 총 140%");
            Check(Mathf.Approximately(rogue.GetStageMultiplier(1) * rogue.GetStageHits(1), 1.8f), "맹독비수 총 180%");
            Check(Mathf.Approximately(rogue.GetStageMultiplier(2) * rogue.GetStageHits(2), 2.5f), "죽음의 촉매 총 250%");

            var mage = new MageEnergyBallActive();
            Check(Mathf.Approximately(mage.GetStageMultiplier(0), 1.4f)
               && Mathf.Approximately(mage.GetStageMultiplier(1), 1.1f)
               && Mathf.Approximately(mage.GetStageMultiplier(2), 1.8f), "마법사 배율 140/110/180%");

            var alch = new AlchemistThrowActive();
            Check(Mathf.Approximately(alch.GetStageMultiplier(0), 1.0f)
               && Mathf.Approximately(alch.GetStageMultiplier(1), 0.9f)
               && Mathf.Approximately(alch.GetStageMultiplier(2), 1.8f), "연금 배율 100/90/180%");
        }

        private static void TestSelectionActiveSummaries()
        {
            var go = new GameObject("CharacterSelectionSummarySelfTest");
            go.SetActive(false);
            var ui = go.AddComponent<UI.CharacterSelectionUI>();
            var buildSummary = typeof(UI.CharacterSelectionUI).GetMethod(
                "BuildActiveSummary",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Check(buildSummary != null, "선택창 액티브 요약 생성기를 찾을 수 있음");
            if (buildSummary == null)
            {
                UnityEngine.Object.DestroyImmediate(go);
                return;
            }

            var cases = new (string assetPath, string condition, string[] effects)[]
            {
                ("Assets/Scripts/Data/Character Preset/Warrior/Warrior.asset", "4 이상", new[] { "단일", "인접", "전장 전체", "방어도" }),
                ("Assets/Scripts/Data/Character Preset/Rogue/Rogue.asset", "3 이하", new[] { "2회", "3회", "5회", "중독", "2배" }),
                ("Assets/Scripts/Data/Character Preset/Mage/Mage.asset", "홀수", new[] { "단일", "2명", "마력 회로 전체", "감전" }),
                ("Assets/Scripts/Data/Character Preset/Alchemist/Alchemist.asset", "짝수", new[] { "단일", "인접", "전장 전체", "약화", "고양" }),
            };

            foreach (var testCase in cases)
            {
                var preset = AssetDatabase.LoadAssetAtPath<CharacterPreset>(testCase.assetPath);
                Check(preset != null, $"선택 설명 테스트 프리셋 로드: {testCase.assetPath}");
                if (preset == null) continue;

                var skill = preset.StartingActives.FirstOrDefault();
                Check(skill != null, $"{preset.CharacterName} 시작 액티브 존재");
                if (skill == null) continue;

                string text = buildSummary.Invoke(ui, new object[] { preset }) as string ?? string.Empty;
                int lineBreaks = text.Count(ch => ch == '\n');

                Check(lineBreaks == 0, $"{skill.GetType().Name} 선택 설명은 제목 없이 한 문장만 표시");
                Check(!text.Contains("<b>[") && !text.Contains("]</b>"), $"{skill.GetType().Name} 선택 설명에 액티브 제목 없음");
                Check(text.Contains(testCase.condition), $"{skill.GetType().Name} 선택 설명에 주사위 조건 포함");
                Check(testCase.effects.All(text.Contains), $"{skill.GetType().Name} 선택 설명에 단계별 핵심 효과 포함");
            }

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
