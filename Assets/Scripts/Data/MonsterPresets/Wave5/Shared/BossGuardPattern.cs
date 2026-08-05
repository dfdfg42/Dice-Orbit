using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave5.Shared
{
    /// <summary>
    /// 보스 호위 패턴 (불꽃 요정/오르골 공용). 불꽃 소녀 생존 시 index 0(패턴1),
    /// 사망 시 index 1(패턴2)만 사용한다. KnightGuardPattern 미러.
    /// seedFireDamage=true면 소유자에 이번-턴 받은-피해 추적 상태(FireDamageTakenStatus)를 최초 1회 부여(불짚이기 취소 판정용).
    /// </summary>
    [System.Serializable]
    public class BossGuardPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        [Tooltip("체크 시 소유자에 받은-피해 추적 상태 부여(불짚이기 취소 판정용)")]
        [SerializeField] private bool seedFireDamage = false;

        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;

            if (seedFireDamage && owner != null && owner.StatusEffects != null
                && !owner.StatusEffects.HasEffect(EffectType.FireDamageTaken))
                owner.StatusEffects.AddEffect(new FireDamageTakenStatus());

            int idx = FlameSet.FindBoss() != null ? 0 : 1;
            if (idx >= availableSkills.Count) idx = 0;
            return availableSkills[idx];
        }
    }
}
