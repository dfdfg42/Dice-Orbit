using System.Collections;
using TMPro;
using UnityEngine;

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

        private const float MoveSpeed      = 1.6f;
        /// <summary>팝업이 떠올랐다 사라지기까지의 수명(초). 표시 대기열이 이 시간을 기다린다.</summary>
        public  const float Lifetime       = 1.2f;
        private const float LabelFontSize  = 13.0f;   // 패시브/상태 버블
        private const float DamageFontSize = 15.0f;   // 데미지 숫자
        private const float CritFontSize   = 19.0f;   // 치명타

        private static readonly Color DamageColor   = Color.red;
        private static readonly Color CriticalColor = Color.yellow;

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

        public static FloatingLabelPopup CreateDamage(int damage, Vector3 worldPos, bool isCritical = false)
        {
            Color color = isCritical ? CriticalColor : DamageColor;
            float size  = isCritical ? CritFontSize  : DamageFontSize;
            var go      = new GameObject("_DamagePopup");
            var popup   = go.AddComponent<FloatingLabelPopup>();
            go.transform.position = worldPos;
            popup.Setup(damage.ToString(), color, size);
            return popup;
        }
    }
}
