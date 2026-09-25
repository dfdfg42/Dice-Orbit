using DiceOrbit.Visuals;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>ZonePlateRenderer 순수 헬퍼 자가 테스트 — 회전 매핑과 틴트. 메뉴 [DiceOrbit → Run ZonePlate Self-Tests] 또는 RunAll().</summary>
    public static class ZonePlateSelfTests
    {
        private static int _failures;

        [MenuItem("DiceOrbit/Run ZonePlate Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;
            TestRotationAtZeroLaysSpriteFlatFacingPlusZ();
            TestRotationAt90TurnsCounterClockwise();
            TestOwnerTintKeepsHueSoftensSaturation();
            if (_failures == 0) Debug.Log("[SelfTest] 전체 PASS — ZonePlate");
            else Debug.LogError($"[SelfTest] 실패 {_failures}건 — ZonePlate");
            return _failures == 0;
        }

        private static void Check(bool condition, string label)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[SelfTest] FAIL: " + label);
        }

        private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-6f;

        private static void TestRotationAtZeroLaysSpriteFlatFacingPlusZ()
        {
            var q = ZonePlateRenderer.PlateRotation(0f);
            Check(Near(q * Vector3.right, Vector3.right), "startDeg 0: 스프라이트 +x → 월드 +x");
            Check(Near(q * Vector3.up, Vector3.forward), "startDeg 0: 스프라이트 +y(위) → 월드 +z (눕힘)");
        }

        private static void TestRotationAt90TurnsCounterClockwise()
        {
            var q = ZonePlateRenderer.PlateRotation(90f);
            Check(Near(q * Vector3.right, Vector3.forward), "startDeg 90: 스프라이트 +x → 월드 +z (반시계 90°, 구역 각도 체계)");
            Check(Near(q * Vector3.up, Vector3.left), "startDeg 90: 스프라이트 +y → 월드 −x");
        }

        private static void TestOwnerTintKeepsHueSoftensSaturation()
        {
            var c = ZonePlateRenderer.OwnerTint(Color.red, 0.32f, 0.85f);
            Check(Mathf.Approximately(c.r, 1f) && Mathf.Abs(c.g - 0.68f) < 0.01f && Mathf.Abs(c.b - 0.68f) < 0.01f, $"OwnerTint(red): (1, 0.68, 0.68) 근처, 실제 {c}");
            Check(Mathf.Approximately(c.a, 0.85f), "OwnerTint: 알파 유지");
        }
    }
}
