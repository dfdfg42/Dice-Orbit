using System;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 타격 등급 — 한 번의 타격이 얼마나 세게 느껴져야 하는지 (타격감 리워크 2026-10-03).
    /// 등급이 플래시·히트스톱·밀림·흔들림·소리·숫자의 세기를 한꺼번에 정한다 (<see cref="HitFeelProfile"/>).
    /// </summary>
    public enum HitTier
    {
        Tick = 0,      // 직접 체력 손실 (중독 등)
        Blocked = 1,   // 공격은 들어갔지만 실제 피해 0 (방어도)
        Light = 2,
        Medium = 3,
        Heavy = 4,
        Kill = 5,      // 이 타격으로 쓰러짐
    }

    /// <summary>Light/Medium/Heavy를 가르는 기준. 세기 점수 = ½·보간(피해/최대체력) + ½·보간(피해 절대값).</summary>
    [Serializable]
    public class HitTierThresholds
    {
        [Tooltip("피해/최대체력 — 이 비율 이하는 0점, ratioHigh 이상은 만점")]
        public float ratioLow = 0.05f;
        public float ratioHigh = 0.35f;
        [Tooltip("피해 절대값 — 이 값 이하는 0점, damageHigh 이상은 만점")]
        public float damageLow = 4f;
        public float damageHigh = 30f;
        [Tooltip("세기 점수가 이 값 이상이면 Medium / Heavy")]
        public float mediumScore = 0.34f;
        public float heavyScore = 0.67f;
    }

    /// <summary>타격 등급 판정 — 순수 함수, 자가 테스트 대상.</summary>
    public static class HitTierClassifier
    {
        /// <summary>세기 점수 0~1. 비율과 절대값을 반반 섞는다 — 체력 적은 몬스터의 큰 비율 피해와 캐릭터의 큰 절대 피해가 둘 다 세게 읽히도록.</summary>
        public static float Score(int damage, int maxHp, HitTierThresholds t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (damage <= 0) return 0f;
            float ratio = maxHp > 0 ? (float)damage / maxHp : 1f;
            float ratioScore = Mathf.Clamp01(Mathf.InverseLerp(t.ratioLow, t.ratioHigh, ratio));
            float absScore = Mathf.Clamp01(Mathf.InverseLerp(t.damageLow, t.damageHigh, damage));
            return 0.5f * ratioScore + 0.5f * absScore;
        }

        /// <summary>
        /// 등급 판정. 우선순위: 처치 → 막힘(피해 0) → 직접 손실 → 세기 점수.
        /// 피해 0인 타격을 보고할지(막힘으로 보여 줄지)는 호출자가 정한다.
        /// </summary>
        public static HitTier Classify(int damage, int maxHp, bool killed, bool directLoss, HitTierThresholds t)
        {
            if (killed) return HitTier.Kill;
            if (damage <= 0) return HitTier.Blocked;
            if (directLoss) return HitTier.Tick;

            float score = Score(damage, maxHp, t);
            if (score >= t.heavyScore) return HitTier.Heavy;
            if (score >= t.mediumScore) return HitTier.Medium;
            return HitTier.Light;
        }
    }
}
