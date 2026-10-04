using System;
using DiceOrbit.Visuals;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 타격감 순수 로직·프로필 에셋 자가 테스트 (타격감 리워크 2026-10-03). 씬·플레이 모드 불필요.
    /// 메뉴 [DiceOrbit → Run HitFeel Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// </summary>
    public static class HitFeelSelfTests
    {
        private static int _failures;

        [MenuItem("DiceOrbit/Run HitFeel Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;

            TestKillBlockedTickPrecedence();
            TestScoreTiersMatchSpecExamples();
            TestScoreIsMonotonicInDamage();
            TestDefaultsCoverEveryTierAndEscalate();
            TestResetKeepsWiredSounds();
            TestGetThrowsWhenTierMissing();
            TestTraumaMath();
            TestDeathSliceGeometry();
            TestProfileAssetIsComplete();

            if (_failures == 0) Debug.Log("[HitFeelSelfTests] 전부 통과");
            else Debug.LogError($"[HitFeelSelfTests] 실패 {_failures}건");
            return _failures == 0;
        }

        // ── 등급 판정 ─────────────────────────────────────────

        private static void TestKillBlockedTickPrecedence()
        {
            var t = new HitTierThresholds();
            Check(HitTierClassifier.Classify(1, 100, killed: true, directLoss: false, t) == HitTier.Kill, "처치는 피해가 작아도 Kill");
            Check(HitTierClassifier.Classify(3, 100, killed: true, directLoss: true, t) == HitTier.Kill, "직접 손실로 쓰러져도 Kill");
            Check(HitTierClassifier.Classify(0, 100, killed: false, directLoss: false, t) == HitTier.Blocked, "피해 0 = 막힘");
            Check(HitTierClassifier.Classify(40, 100, killed: false, directLoss: true, t) == HitTier.Tick, "직접 손실은 커도 Tick");
        }

        private static void TestScoreTiersMatchSpecExamples()
        {
            var t = new HitTierThresholds();
            Check(Tier(2, 20, t) == HitTier.Light, "체력 20에 2 = Light (비율만 크다고 세지 않다)");
            Check(Tier(6, 20, t) == HitTier.Medium, "체력 20 슬라임이 6 = Medium");
            Check(Tier(12, 100, t) == HitTier.Light, "체력 100 캐릭터가 12 = Light");
            Check(Tier(25, 100, t) == HitTier.Heavy, "체력 100 캐릭터가 25 = Heavy");
            Check(Tier(18, 60, t) == HitTier.Heavy, "체력 60에 18(콤보 3단) = Heavy");
        }

        private static void TestScoreIsMonotonicInDamage()
        {
            var t = new HitTierThresholds();
            float prev = -1f;
            for (int d = 0; d <= 60; d++)
            {
                float score = HitTierClassifier.Score(d, 80, t);
                Check(score >= prev, $"세기 점수는 피해가 커질수록 줄지 않는다 (d={d})");
                Check(score >= 0f && score <= 1f, $"세기 점수는 0~1 (d={d})");
                prev = score;
            }
            Check(HitTierClassifier.Score(0, 80, t) == 0f, "피해 0 = 0점");
            Check(HitTierClassifier.Score(10, 0, t) > 0f, "최대 체력 0이어도 예외 없이 점수를 낸다");
        }

        // ── 프로필 ────────────────────────────────────────────

        private static void TestDefaultsCoverEveryTierAndEscalate()
        {
            var p = ScriptableObject.CreateInstance<HitFeelProfile>();
            p.ResetTiersToDefaults();
            foreach (HitTier tier in Enum.GetValues(typeof(HitTier)))
                Check(p.TryGet(tier, out _), $"기본값에 {tier} 항목이 있다");

            HitTier[] rising = { HitTier.Light, HitTier.Medium, HitTier.Heavy, HitTier.Kill };
            for (int i = 1; i < rising.Length; i++)
            {
                var a = p.Get(rising[i - 1]); var b = p.Get(rising[i]);
                Check(b.hitStop > a.hitStop, $"{rising[i]} 히트스톱 > {rising[i - 1]}");
                Check(b.trauma > a.trauma, $"{rising[i]} 흔들림 > {rising[i - 1]}");
                Check(b.recoilDistance > a.recoilDistance, $"{rising[i]} 넉백 > {rising[i - 1]}");
                Check(b.popupScale >= a.popupScale, $"{rising[i]} 숫자 크기 ≥ {rising[i - 1]}");
            }
            Check(p.Get(HitTier.Tick).hitStop == 0f && p.Get(HitTier.Tick).trauma == 0f, "틱(중독 등)은 화면을 멈추거나 흔들지 않는다");
            Check(p.Get(HitTier.Kill).hitStop <= 0.2f, "히트스톱은 가장 길어도 0.2초 이하 (템포 보호)");
            UnityEngine.Object.DestroyImmediate(p);
        }

        private static void TestResetKeepsWiredSounds()
        {
            var p = ScriptableObject.CreateInstance<HitFeelProfile>();
            p.ResetTiersToDefaults();
            var clip = AudioClip.Create("test", 64, 1, 44100, false);
            p.Get(HitTier.Heavy).sfx = clip;
            p.Get(HitTier.Heavy).sfxVolume = 0.4f;
            p.Get(HitTier.Heavy).hitStop = 9f;

            p.ResetTiersToDefaults();
            Check(p.Get(HitTier.Heavy).sfx == clip, "기본값 재설정이 배선된 소리를 지우지 않는다");
            Check(Mathf.Approximately(p.Get(HitTier.Heavy).sfxVolume, 0.4f), "기본값 재설정이 소리 볼륨을 지우지 않는다");
            Check(p.Get(HitTier.Heavy).hitStop < 1f, "기본값 재설정이 수치는 되돌린다");
            UnityEngine.Object.DestroyImmediate(clip);
            UnityEngine.Object.DestroyImmediate(p);
        }

        private static void TestGetThrowsWhenTierMissing()
        {
            var p = ScriptableObject.CreateInstance<HitFeelProfile>();
            ExpectThrows<InvalidOperationException>(() => p.Get(HitTier.Light), "항목 없는 등급 Get은 예외 (조용한 기본값 없음)");
            UnityEngine.Object.DestroyImmediate(p);
        }

        // ── 처치 연출 (베어 가르기) ───────────────────────────

        private static void TestDeathSliceGeometry()
        {
            foreach (float angle in new[] { 25f, -25f, 38f, -18f, 155f, -160f })
            {
                DeathSliceEffect.CutFrame(angle, out var direction, out var normal);
                Check(Mathf.Abs(Vector2.Dot(direction, normal)) < 1e-5f, $"법선은 베는 선에 수직 ({angle}도)");
                Check(Mathf.Approximately(direction.magnitude, 1f) && Mathf.Approximately(normal.magnitude, 1f), $"방향·법선은 단위 벡터 ({angle}도)");
                Check(normal.y > 0f, $"법선은 언제나 위 조각을 가리킨다 ({angle}도)");

                Vector2 slide = DeathSliceEffect.SlideDirection(direction);
                Check(slide.y <= 0f, $"위 조각은 베인 선을 따라 내려간다 ({angle}도)");
                Check(Mathf.Approximately(Mathf.Abs(Vector2.Dot(slide, direction)), 1f), $"미끄러지는 방향은 베는 선 위 ({angle}도)");
            }

            var feel = new DeathSliceFeel();
            Check(feel.angleRange.x > 0f && feel.angleRange.y < 60f, "베는 각도는 비스듬하다 (수평도 수직도 아니다)");
            Check(feel.slashSweep + feel.slashFade <= 0.5f, "참격은 0.5초 안에 사라진다");
            Check(feel.holdDuration + feel.splitDuration <= 0.8f, "갈라짐은 0.8초 안에 끝난다 (템포 보호)");
        }

        // ── 카메라 트라우마 ───────────────────────────────────

        private static void TestTraumaMath()
        {
            Check(Mathf.Approximately(CameraShaker.Decay(0.6f, 2f, 0.1f), 0.4f), "트라우마 선형 감쇠");
            Check(CameraShaker.Decay(0.1f, 2f, 1f) == 0f, "트라우마는 0 아래로 내려가지 않는다");
            Check(Mathf.Approximately(CameraShaker.TraumaForAmplitude(0.55f, 0.55f), 1f), "최대폭 흔들림 = 트라우마 1");
            float half = CameraShaker.TraumaForAmplitude(0.1375f, 0.55f);
            Check(Mathf.Approximately(half * half * 0.55f, 0.1375f), "환산한 트라우마² × 최대폭 = 요청한 폭");
            Check(CameraShaker.TraumaForAmplitude(5f, 0.55f) == 1f, "트라우마는 1을 넘지 않는다");
            Check(CameraShaker.TraumaForAmplitude(0.1f, 0f) == 0f, "최대폭 0이면 0");
        }

        // ── 실제 에셋 ─────────────────────────────────────────

        private static void TestProfileAssetIsComplete()
        {
            HitFeelProfile p;
            try { p = HitFeelProfile.LoadOrThrow(HitFeelProfile.ResourcePath); }
            catch (InvalidOperationException e) { _failures++; Debug.LogError("[HitFeelSelfTests] FAIL: 프로필 에셋 없음 — " + e.Message); return; }

            foreach (HitTier tier in Enum.GetValues(typeof(HitTier)))
            {
                if (!p.TryGet(tier, out var feel)) { Check(false, $"에셋에 {tier} 항목이 있다"); continue; }
                Check(feel.sfx != null, $"에셋 {tier} 타격음이 배선됐다");
            }
            Check(p.characterLaunch.sfx != null, "에셋 발사 소리가 배선됐다");
            Check(p.monsterStrike.sfx != null, "에셋 몬스터 내리찍기 소리가 배선됐다");
            Check(p.flashMaterial != null && p.flashMaterial.shader != null && p.flashMaterial.shader.name == "DiceOrbit/SpriteSolidFlash",
                "에셋 플래시 재질이 SpriteSolidFlash 셰이더를 쓴다");
            var slice = p.deathSlice.sliceMaterial;
            Check(slice != null && slice.shader != null && slice.shader.name == "DiceOrbit/SpriteSlice",
                "에셋 처치 조각 재질이 SpriteSlice 셰이더를 쓴다");
            Check(p.deathSlice.slashSprite != null, "에셋 참격 스프라이트가 배선됐다");
        }

        // ── 도우미 ────────────────────────────────────────────

        private static HitTier Tier(int damage, int maxHp, HitTierThresholds t)
            => HitTierClassifier.Classify(damage, maxHp, killed: false, directLoss: false, t);

        private static void Check(bool condition, string what)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[HitFeelSelfTests] FAIL: " + what);
        }

        private static void ExpectThrows<T>(Action action, string what) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            catch (Exception e) { _failures++; Debug.LogError($"[HitFeelSelfTests] FAIL: {what} — 다른 예외 {e.GetType().Name}"); return; }
            _failures++;
            Debug.LogError("[HitFeelSelfTests] FAIL: " + what + " — 예외가 나지 않음");
        }
    }
}
