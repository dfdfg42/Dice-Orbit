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

        private const float MoveSpeed = 1.6f;
        private const float Lifetime  = 1.2f;
        private const float FontSize  = 3.0f;

        private void Awake()
        {
            _cam = Camera.main;
        }

        public void Setup(string text, Color color)
        {
            _text              = gameObject.AddComponent<TextMeshPro>();
            _text.text         = text;
            _text.color        = color;
            _text.fontSize     = FontSize;
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
    }
}
