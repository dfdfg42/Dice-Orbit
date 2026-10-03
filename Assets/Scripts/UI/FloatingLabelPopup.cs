using System.Collections;
using TMPro;
using UnityEngine;
using DiceOrbit.Visuals;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 월드 공간에 텍스트를 띄우고 위로 이동하며 사라지는 팝업.
    /// DamagePopup의 텍스트 일반화 버전.
    /// </summary>
    public class FloatingLabelPopup : MonoBehaviour
    {
        private TextMeshPro _text;
        private Color _baseColor;
        private Camera _cam;

        /// <summary>화면에 살아 있는 팝업 수. 몬스터 턴 전환이 0이 될 때까지 기다린다.</summary>
        public static int ActiveCount { get; private set; }

        private const float MoveSpeed      = 1.6f;
        /// <summary>팝업이 떠올랐다 사라지기까지의 수명(초). 표시 대기열이 이 시간을 기다린다.</summary>
        public  const float Lifetime       = 1.2f;
        private const float LabelFontSize  = 13.0f;   // 패시브/상태 버블
        private const float DamageFontSize = 15.0f;   // 데미지 숫자 (등급 배율이 곱해진다 — HitFeelProfile)

        // 나타날 때 팡 튀는 연출 (데미지 숫자·막음 전용 — 버블은 0)
        private float _punchScale = 1f;
        private float _punchDuration;

        // KOTRA HOPE 폰트 + 검은 외곽선(두껍게). 런타임 생성 팝업이라 Resources에서 로드해 정적 캐시.
        private static TMP_FontAsset _font;
        private static Material _outlineMat;

        private static TMP_FontAsset PopupFont
            => _font != null ? _font : (_font = Resources.Load<TMP_FontAsset>("Fonts/KOTRA HOPE SDF"));

        /// <summary>KOTRA HOPE 머티리얼을 복제해 외곽선을 켠 공용 머티리얼 (모든 팝업이 공유, 색은 정점색으로).</summary>
        private static Material PopupMaterial
        {
            get
            {
                if (_outlineMat != null) return _outlineMat;
                var f = PopupFont;
                if (f == null || f.material == null) return null;
                _outlineMat = new Material(f.material);
                _outlineMat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                _outlineMat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                _outlineMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
                return _outlineMat;
            }
        }

        private void Awake()
        {
            _cam = Camera.main;
            ActiveCount++;
        }

        private void OnDestroy()
        {
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
        }

        public void Setup(string text, Color color, float fontSize = LabelFontSize)
        {
            _text              = gameObject.AddComponent<TextMeshPro>();

            var font = PopupFont;
            if (font != null)
            {
                _text.font = font;
                var mat = PopupMaterial;
                if (mat != null) _text.fontSharedMaterial = mat;   // KOTRA HOPE + 검은 외곽선(두껍게)
            }

            _text.text         = text;
            _text.color        = color;
            _text.fontSize     = fontSize;
            _text.fontStyle    = FontStyles.Bold;                  // 볼드
            _text.alignment    = TextAlignmentOptions.Center;
            _text.sortingOrder = 200;
            _baseColor         = color;
            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            float   elapsed = 0f;
            Vector3 start   = transform.position;

            while (elapsed < Lifetime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Lifetime);

                transform.position = start + Vector3.up * (MoveSpeed * elapsed);

                if (_punchDuration > 0f)
                {
                    float k = Mathf.Clamp01(elapsed / _punchDuration);
                    transform.localScale = Vector3.one * Mathf.LerpUnclamped(_punchScale, 1f, EaseOutBack(k));
                }

                if (_text != null)
                    _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 1f - t);

                if (_cam != null)
                    transform.rotation = _cam.transform.rotation;

                yield return null;
            }

            Destroy(gameObject);
        }

        public static FloatingLabelPopup Create(string text, Color color, Vector3 worldPos)
        {
            var go    = new GameObject("_FloatingLabel");
            var popup = go.AddComponent<FloatingLabelPopup>();
            go.transform.position = worldPos;
            popup.Setup(text, color);
            return popup;
        }

        /// <summary>데미지 숫자 — 타격 등급이 크기·색을 정하고, 나타날 때 팡 튄다 (타격감 리워크 2026-10-03).</summary>
        public static FloatingLabelPopup CreateDamage(int damage, Vector3 worldPos, HitTier tier)
        {
            var profile = HitFeelProfile.Current;
            var feel = profile.Get(tier);
            return CreatePunched("_DamagePopup", damage.ToString(), feel.popupColor, DamageFontSize * feel.popupScale, worldPos, profile);
        }

        /// <summary>공격이 방어도에 막혀 피해가 0일 때 — 숫자 대신 "막음".</summary>
        public static FloatingLabelPopup CreateBlocked(Vector3 worldPos)
        {
            var profile = HitFeelProfile.Current;
            var feel = profile.Get(HitTier.Blocked);
            return CreatePunched("_BlockedPopup", "막음", profile.blockedPopupColor, DamageFontSize * feel.popupScale, worldPos, profile);
        }

        private static FloatingLabelPopup CreatePunched(string objectName, string text, Color color, float fontSize, Vector3 worldPos, HitFeelProfile profile)
        {
            var go = new GameObject(objectName);
            var popup = go.AddComponent<FloatingLabelPopup>();

            // 동시 타격 숫자가 겹치지 않게 화면 좌우로 살짝 흩는다
            var cam = Camera.main;
            Vector3 scatter = cam != null
                ? cam.transform.right * Random.Range(-profile.popupScatter, profile.popupScatter)
                : Vector3.zero;
            go.transform.position = worldPos + scatter;

            popup._punchScale = profile.popupPunchScale;
            popup._punchDuration = profile.popupPunchDuration;
            go.transform.localScale = Vector3.one * popup._punchScale;
            popup.Setup(text, color, fontSize);
            return popup;
        }

        private static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
        }
    }
}
