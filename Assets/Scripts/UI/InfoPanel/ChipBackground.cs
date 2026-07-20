using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 대상 TMP 텍스트의 선호 폭에 맞춰 자동으로 폭을 조정하는 둥근 크림 칩 배경.
    /// Figma 배틀 UI의 섹션 제목/이름 칩(#E8DBC3, radius 10)에 사용.
    /// 이름·액티브 제목처럼 런타임에 내용이 바뀌어도 칩이 텍스트를 감싼다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class ChipBackground : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI target;
        [SerializeField] private float horizontalPadding = 28f;
        [SerializeField] private float minWidth = 40f;
        [SerializeField] private float maxWidth = 300f;

        private RectTransform _rt;
        private string _lastText;

        public void Bind(TextMeshProUGUI tmp) => target = tmp;

        private void Awake() => _rt = (RectTransform)transform;

        private void LateUpdate()
        {
            if (target == null) return;
            string t = target.text;
            if (t == _lastText) return;
            _lastText = t;

            if (_rt == null) _rt = (RectTransform)transform;
            bool empty = string.IsNullOrEmpty(t);
            var img = GetComponent<Image>();
            if (img != null) img.enabled = !empty;   // 내용 없으면 칩 숨김
            if (empty) return;

            float prefW = target.GetPreferredValues(t).x;
            float w = Mathf.Clamp(prefW + horizontalPadding, minWidth, maxWidth);
            _rt.offsetMax = new Vector2(w, _rt.offsetMax.y);
        }
    }
}
