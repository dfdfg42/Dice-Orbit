using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>한 등급의 타격 느낌 — 여섯 겹(플래시·히트스톱·밀림·흔들림·소리·숫자)의 세기.</summary>
    [Serializable]
    public class HitTierFeel
    {
        public HitTier tier;

        [Header("멈칫 · 흔들림")]
        [Tooltip("히트스톱(초, realtime). 같은 프레임에 겹치면 긴 쪽 하나만")]
        public float hitStop;
        [Tooltip("카메라 트라우마 가산 (0~1, 누적)")]
        public float trauma;

        [Header("대상 반응")]
        [Tooltip("흰 실루엣 플래시 지속(초)")]
        public float flashDuration = 0.08f;
        public Color flashColor = Color.white;
        [Tooltip("공격 방향으로 밀리는 거리(월드 유닛)")]
        public float recoilDistance;
        [Tooltip("밀렸다 돌아오는 시간(초)")]
        public float recoilDuration = 0.22f;
        [Tooltip("맞는 순간 스케일 배수 (x, y) — 1,1 = 변형 없음")]
        public Vector2 squash = Vector2.one;
        [Tooltip("맞는 순간 기울기(도)")]
        public float tiltDegrees;

        [Header("임팩트 VFX · 숫자")]
        public float impactScale = 1f;
        public float popupScale = 1f;
        public Color popupColor = Color.red;

        [Header("소리")]
        public AudioClip sfx;
        [Range(0f, 1f)] public float sfxVolume = 1f;
    }

    /// <summary>캐릭터가 공격을 쏘는 순간의 반동.</summary>
    [Serializable]
    public class AttackLaunchFeel
    {
        [Tooltip("대상 쪽으로 내미는 거리(월드 유닛)")]
        public float lungeDistance = 0.35f;
        public float lungeDuration = 0.25f;
        public AudioClip sfx;
        [Range(0f, 1f)] public float sfxVolume = 0.6f;
    }

    /// <summary>몬스터 공격 — 움츠렸다가 내리찍는다.</summary>
    [Serializable]
    public class MonsterStrikeFeel
    {
        [Tooltip("실행 직전 움츠리는 시간(초) — 이만큼 몬스터 행동이 늦어진다")]
        public float windupDuration = 0.16f;
        [Tooltip("움츠릴 때 뒤로 물러나는 거리")]
        public float windupPullback = 0.15f;
        public Vector2 windupSquash = new Vector2(1.08f, 0.88f);
        [Tooltip("대상 쪽으로 내리찍는 거리")]
        public float strikeDistance = 0.5f;
        public float strikeDuration = 0.28f;
        public AudioClip sfx;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
    }

    /// <summary>몬스터 처치 — 참격이 가로지르고 두 조각으로 갈라져 사라진다 (<see cref="DeathSliceEffect"/>). 거리는 스프라이트 대각선 대비 비율.</summary>
    [Serializable]
    public class DeathSliceFeel
    {
        [Header("에셋")]
        [Tooltip("잘린 조각용 재질 (셰이더 DiceOrbit/SpriteSlice)")]
        public Material sliceMaterial;
        [Tooltip("참격 줄기 스프라이트 — 가로로 긴 흰 줄기 (Tools/make_slash_streak.py)")]
        public Sprite slashSprite;

        [Header("참격 (realtime — 처치 히트스톱 중에도 그어진다)")]
        public Color slashColor = Color.white;
        [Tooltip("몬스터를 가로지르는 시간(초)")]
        public float slashSweep = 0.07f;
        [Tooltip("그어진 뒤 가늘어지며 사라지는 시간(초)")]
        public float slashFade = 0.26f;
        [Tooltip("길이 (대각선 대비)")]
        public float slashLength = 1.8f;
        [Tooltip("굵기 (대각선 대비)")]
        public float slashThickness = 0.30f;
        [Tooltip("베는 각도 범위(도, 수평 기준) — 좌우 기울기는 무작위")]
        public Vector2 angleRange = new Vector2(18f, 38f);

        [Header("갈라짐 (scaled — 히트스톱이 풀린 뒤)")]
        [Tooltip("갈라지기 전 흰 실루엣으로 붙어 있는 시간(초)")]
        public float holdDuration = 0.05f;
        [Tooltip("두 조각이 벌어지며 사라지는 시간(초)")]
        public float splitDuration = 0.55f;
        [Tooltip("위 조각이 베인 선을 따라 미끄러지는 거리 (대각선 대비)")]
        public float slideDistance = 0.26f;
        [Tooltip("베인 선에서 벌어지는 거리 (대각선 대비)")]
        public float gapDistance = 0.05f;
        [Tooltip("아래 조각이 반대로 움직이는 정도 (위 조각 대비)")]
        [Range(0f, 1f)] public float lowerRatio = 0.3f;
        [Tooltip("미끄러지며 기우는 각도(도)")]
        public float tiltDegrees = 7f;
        [Tooltip("갈라짐의 이 지점부터 사라지기 시작한다 (0~1)")]
        [Range(0f, 1f)] public float fadeStart = 0.45f;

        [Header("빛")]
        public Color flashColor = Color.white;
        [Tooltip("흰 실루엣이 제 색으로 돌아오는 구간 (갈라짐의 0~이 값)")]
        [Range(0.01f, 1f)] public float flashRelease = 0.3f;
        [Tooltip("잘린 단면이 빛나는 폭 (대각선 대비)")]
        public float edgeWidth = 0.04f;
    }

    /// <summary>
    /// 타격감 수치의 단일 권위 (타격감 리워크 2026-10-03, 스펙 Docs/superpowers/specs/2026-10-03-hit-feel-rework-design.md).
    /// 등급별 여섯 겹 + 공격자 연출 + 소리 + 플래시 재질. 에셋: Resources/Combat/HitFeelProfile.asset
    /// (메뉴 [DiceOrbit/Create HitFeel Profile]). 없으면 예외 — 다른 값으로 조용히 굴러가지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "HitFeelProfile", menuName = "Dice Orbit/Combat/Hit Feel Profile")]
    public class HitFeelProfile : ScriptableObject
    {
        public const string ResourcePath = "Combat/HitFeelProfile";
        private static HitFeelProfile _current;

        public static HitFeelProfile Current
        {
            get
            {
                if (_current == null) _current = LoadOrThrow(ResourcePath);
                return _current;
            }
        }

        public static HitFeelProfile LoadOrThrow(string resourcePath)
        {
            var profile = Resources.Load<HitFeelProfile>(resourcePath);
            if (profile == null)
                throw new InvalidOperationException(
                    $"[HitFeelProfile] Resources/{resourcePath}.asset 이 없습니다. 메뉴 [DiceOrbit/Create HitFeel Profile]로 만드세요.");
            return profile;
        }

        [Header("등급 판정")]
        public HitTierThresholds thresholds = new HitTierThresholds();

        [Header("등급별 느낌 (HitTier마다 1항목)")]
        public List<HitTierFeel> tiers = new List<HitTierFeel>();

        [Header("공격자")]
        public AttackLaunchFeel characterLaunch = new AttackLaunchFeel();
        public MonsterStrikeFeel monsterStrike = new MonsterStrikeFeel();

        [Header("플래시 · 소리 · 숫자 공통")]
        [Tooltip("흰 실루엣용 재질 (셰이더 DiceOrbit/SpriteSolidFlash)")]
        public Material flashMaterial;
        [Tooltip("타격음 피치 흔들기 (±비율) — 같은 소리가 반복돼도 기계적으로 들리지 않게")]
        [Range(0f, 0.2f)] public float sfxPitchJitter = 0.06f;
        [Tooltip("숫자가 나타날 때 팡 튀는 시작 배율과 시간")]
        public float popupPunchScale = 1.7f;
        public float popupPunchDuration = 0.12f;
        [Tooltip("동시 타격 숫자가 겹치지 않게 좌우로 흩는 폭(월드 유닛)")]
        public float popupScatter = 0.35f;
        public Color blockedPopupColor = new Color(0.62f, 0.76f, 0.95f);

        [Header("몬스터 처치")]
        public DeathSliceFeel deathSlice = new DeathSliceFeel();

        public HitTierFeel Get(HitTier tier)
        {
            if (!TryGet(tier, out var feel))
                throw new InvalidOperationException($"[HitFeelProfile] 등급 {tier} 항목이 없습니다 — 메뉴 [DiceOrbit/Create HitFeel Profile]로 기본값을 채우세요.");
            return feel;
        }

        public bool TryGet(HitTier tier, out HitTierFeel feel)
        {
            for (int i = 0; i < tiers.Count; i++)
                if (tiers[i] != null && tiers[i].tier == tier) { feel = tiers[i]; return true; }
            feel = null;
            return false;
        }

        /// <summary>스펙 §4의 기본값으로 등급 표를 다시 채운다 (소리 클립·재질은 건드리지 않는다). 에디터 생성 도구·자가 테스트가 쓴다.</summary>
        public void ResetTiersToDefaults()
        {
            var keepSfx = new Dictionary<HitTier, (AudioClip clip, float volume)>();
            foreach (var t in tiers) if (t != null) keepSfx[t.tier] = (t.sfx, t.sfxVolume);

            var red = new Color(0.95f, 0.22f, 0.20f);
            var orange = new Color(1.00f, 0.55f, 0.15f);
            var yellow = new Color(1.00f, 0.88f, 0.25f);
            var poison = new Color(0.62f, 0.85f, 0.35f);

            tiers = new List<HitTierFeel>
            {
                Feel(HitTier.Tick,    0f,     0f,    0.08f, new Color(0.85f, 1f, 0.8f), 0f,    0.16f, 1.04f, 0.96f, 0f,  0.8f, 0.85f, poison),
                Feel(HitTier.Blocked, 0.03f,  0.12f, 0.06f, new Color(0.8f, 0.9f, 1f),  0.08f, 0.16f, 0.97f, 1.03f, 2f,  0.7f, 0.9f,  blockedPopupColor),
                Feel(HitTier.Light,   0.035f, 0.22f, 0.08f, Color.white,                0.18f, 0.20f, 1.10f, 0.92f, 4f,  0.9f, 1.0f,  red),
                Feel(HitTier.Medium,  0.06f,  0.38f, 0.10f, Color.white,                0.30f, 0.22f, 1.16f, 0.86f, 7f,  1.2f, 1.2f,  red),
                Feel(HitTier.Heavy,   0.10f,  0.60f, 0.13f, Color.white,                0.48f, 0.26f, 1.24f, 0.80f, 10f, 1.6f, 1.5f,  orange),
                Feel(HitTier.Kill,    0.14f,  0.80f, 0.16f, Color.white,                0.60f, 0.28f, 1.30f, 0.75f, 14f, 1.9f, 1.7f,  yellow),
            };

            foreach (var t in tiers)
                if (keepSfx.TryGetValue(t.tier, out var kept)) { t.sfx = kept.clip; t.sfxVolume = kept.volume; }
        }

        private static HitTierFeel Feel(HitTier tier, float hitStop, float trauma, float flash, Color flashColor,
                                        float recoil, float recoilDuration, float squashX, float squashY, float tilt,
                                        float impactScale, float popupScale, Color popupColor)
        {
            return new HitTierFeel
            {
                tier = tier, hitStop = hitStop, trauma = trauma,
                flashDuration = flash, flashColor = flashColor,
                recoilDistance = recoil, recoilDuration = recoilDuration,
                squash = new Vector2(squashX, squashY), tiltDegrees = tilt,
                impactScale = impactScale, popupScale = popupScale, popupColor = popupColor,
                sfxVolume = 1f,
            };
        }
    }
}
