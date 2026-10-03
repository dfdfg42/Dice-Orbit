using System.Collections;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.UI;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 타격감 단일 창구 (타격감 리워크 2026-10-03). 한 번의 타격을 여섯 겹으로 동시에 터뜨린다:
    /// 임팩트 VFX · 흰 실루엣 플래시 · 히트스톱 · 밀림(넉백+찌그러짐) · 카메라 트라우마 · 타격음. (숫자 팝업은 Unit.TakeDamage가 같은 등급으로 띄운다.)
    /// 세기는 전부 <see cref="HitFeelProfile"/>의 등급 표에서 온다 — 여기에는 수치가 없다.
    ///
    /// 호출 지점: CombatPipeline.ApplyAction(피격), AutoAttackSystem·ComboActiveSkill·CharacterActiveTemplate(발사), CombatManager(몬스터 움츠림·내리찍기).
    /// </summary>
    public static class HitDirector
    {
        private static bool _warnedNoAudio;

        /// <summary>
        /// 공격이 대상에 적용된 직후. damage = 실제로 깎인 체력(0이면 막힘으로 연출), killed = 이 타격으로 쓰러졌는가.
        /// 반환값은 판정된 등급.
        /// </summary>
        public static HitTier ReportHit(Unit source, Unit target, int damage, bool killed, bool directLoss, string vfxCue)
        {
            if (target == null) throw new System.ArgumentNullException(nameof(target));

            var profile = HitFeelProfile.Current;
            int maxHp = target.Stats != null ? target.Stats.MaxHP : 0;
            var tier = HitTierClassifier.Classify(damage, maxHp, killed, directLoss, profile.thresholds);
            var feel = profile.Get(tier);
            Vector3 direction = HitDirection(source, target);

            // ① 임팩트 VFX (막힘은 숫자 대신 "막음" 글자가 뜬다)
            VfxService.PlayImpact(string.IsNullOrEmpty(vfxCue) ? VfxTags.Impact : vfxCue, target, feel.impactScale);
            if (tier == HitTier.Blocked)
                FloatingLabelPopup.CreateBlocked(target.transform.position + Vector3.up * 1.6f);

            // ② 대상 반응 — 플래시 + 밀림
            UnitHitReactor.Get(target).PlayHit(feel, direction);

            // ③ 멈칫 ④ 흔들림 ⑤ 소리
            ImpactFeedback.HitStop(feel.hitStop);
            if (CameraShaker.Instance != null) CameraShaker.Instance.AddTrauma(feel.trauma);
            PlaySfx(feel.sfx, feel.sfxVolume, target.transform.position);

            return tier;
        }

        /// <summary>캐릭터가 공격을 쏘는 순간 — 대상 쪽 반동 + 휘두르는 소리.</summary>
        public static void ReportAttackLaunched(Unit attacker, Vector3 targetWorldPosition)
        {
            if (attacker == null) return;
            var launch = HitFeelProfile.Current.characterLaunch;
            Vector3 dir = Flat(targetWorldPosition - attacker.transform.position);
            UnitHitReactor.Get(attacker).PlayLunge(dir, launch.lungeDistance, launch.lungeDuration);
            PlaySfx(launch.sfx, launch.sfxVolume, attacker.transform.position);
        }

        /// <summary>몬스터 공격 직전 — 움츠린다. 끝나면 호출자가 <see cref="MonsterStrike"/>와 실제 공격을 실행한다.</summary>
        public static IEnumerator MonsterWindup(Monster monster, Vector3 aimWorldPosition)
        {
            if (monster == null) yield break;
            var strike = HitFeelProfile.Current.monsterStrike;
            var reactor = UnitHitReactor.Get(monster);
            Vector3 dir = Flat(aimWorldPosition - monster.transform.position);

            float t = 0f;
            while (t < strike.windupDuration)
            {
                if (monster == null) yield break;
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / strike.windupDuration);
                reactor.SetWindup(1f - (1f - k) * (1f - k), dir, strike.windupPullback, strike.windupSquash);
                yield return null;
            }
        }

        /// <summary>몬스터가 내리찍는 순간 — 움츠림을 풀고 대상 쪽으로 튀어 나간다 + 내리찍는 소리. 피해는 호출자가 이 직후 적용한다.</summary>
        public static void MonsterStrike(Monster monster, Vector3 aimWorldPosition)
        {
            if (monster == null) return;
            var strike = HitFeelProfile.Current.monsterStrike;
            var reactor = UnitHitReactor.Get(monster);
            Vector3 dir = Flat(aimWorldPosition - monster.transform.position);
            reactor.SetWindup(0f, dir, 0f, Vector2.one);
            reactor.PlayLunge(dir, strike.strikeDistance, strike.strikeDuration);
            PlaySfx(strike.sfx, strike.sfxVolume, monster.transform.position);
        }

        /// <summary>몬스터 처치 — 커지며 사라진다 (파괴 지연 시간 안에서).</summary>
        public static void MonsterDeathPop(Monster monster, float duration)
        {
            if (monster == null) return;
            UnitHitReactor.Get(monster).PlayDeathPop(duration, HitFeelProfile.Current.deathPopScale);
        }

        // ── 내부 ──────────────────────────────────────────────

        /// <summary>공격자→대상 방향(바닥 평면). 공격자가 없거나 같은 자리면 카메라에서 멀어지는 쪽.</summary>
        private static Vector3 HitDirection(Unit source, Unit target)
        {
            if (source != null && source != target)
            {
                Vector3 d = Flat(target.transform.position - source.transform.position);
                if (d != Vector3.zero) return d;
            }
            var cam = Camera.main;
            return cam != null ? Flat(cam.transform.forward) : Vector3.forward;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }

        private static void PlaySfx(AudioClip clip, float volume, Vector3 at)
        {
            if (clip == null || volume <= 0f) return;
            var audio = AudioManager.Instance;
            if (audio == null)
            {
                if (!_warnedNoAudio)
                {
                    _warnedNoAudio = true;
                    Debug.LogWarning("[HitDirector] AudioManager가 없어 타격음을 재생하지 않는다 — 씬에 AudioManager 프리팹이 있어야 한다.");
                }
                return;
            }

            float jitter = HitFeelProfile.Current.sfxPitchJitter;
            float pitch = 1f + Random.Range(-jitter, jitter);
            audio.PlaySFXAtPoint(clip, at, volume, pitch, 0f);   // 풀 소스 — 소스마다 피치가 따로라 겹쳐도 서로 안 흔든다. spatialBlend 0 = 2D
        }
    }
}
