using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 주사위 호버 툴팁 — 호버한 주사위 위에 6면(2줄×3, 실제 주사위 이미지)과 사용 효과를 표시.
    /// 씬에 배치하거나 EnsureInstance로 자동 생성. 캔버스는 최상단 정렬.
    /// </summary>
    public class DiceHoverTooltipUI : MonoBehaviour
    {
        public static DiceHoverTooltipUI Instance { get; private set; }

        [Header("참조 (씬 배치 시)")]
        [SerializeField] private RectTransform panel;        // 툴팁 루트 패널
        [SerializeField] private RectTransform faceGrid;     // 2줄×3 GridLayoutGroup
        [SerializeField] private RectTransform effectRow;    // 효과 행 (없으면 숨김)
        [SerializeField] private Image effectIcon;
        [SerializeField] private TextMeshProUGUI effectLabel;
        [SerializeField] private Sprite dieFaceSprite;       // Assets/Sprites/Dice.png
        [SerializeField] private Vector2 aboveOffset = new Vector2(0f, 90f);

        private readonly List<GameObject> _faceCells = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static DiceHoverTooltipUI EnsureInstance()
        {
            if (Instance != null) return Instance;
            Instance = FindAnyObjectByType<DiceHoverTooltipUI>(FindObjectsInactive.Include);
            if (Instance == null)
                Debug.LogWarning("[DiceHoverTooltipUI] 씬에 인스턴스가 없습니다. 패널을 배치해주세요.");
            return Instance;
        }

        public void Show(DiceElement element)
        {
            var src = element != null ? element.Data?.Source : null;
            if (src == null || panel == null) return;

            BuildFaces(src.Faces);

            var effect = src.Effect;
            if (effectRow != null) effectRow.gameObject.SetActive(effect != null);
            if (effect != null)
            {
                if (effectLabel != null) effectLabel.text = effect.Preview();
                if (effectIcon != null)
                {
                    effectIcon.enabled = effect.Icon != null;
                    if (effect.Icon != null) effectIcon.sprite = effect.Icon;
                }
            }

            panel.gameObject.SetActive(true);
            // 호버한 주사위 위에 배치
            panel.position = (Vector2)element.transform.position + aboveOffset;
            panel.SetAsLastSibling();
        }

        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        /// <summary>faceGrid 아래에 면 6칸을 채운다 (각 칸 = 주사위 이미지 + 값). GridLayoutGroup가 2줄×3로 정렬.</summary>
        private void BuildFaces(int[] faces)
        {
            if (faceGrid == null) return;
            foreach (var c in _faceCells) Destroy(c);
            _faceCells.Clear();
            if (faces == null) return;

            foreach (int v in faces)
            {
                var cell = new GameObject("FaceCell", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(faceGrid, false);
                var img = cell.GetComponent<Image>();
                img.sprite = dieFaceSprite;
                img.raycastTarget = false;

                var txt = new GameObject("Val", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                txt.transform.SetParent(cell.transform, false);
                txt.text = v.ToString();
                txt.alignment = TextAlignmentOptions.Center;
                txt.raycastTarget = false;
                txt.enableAutoSizing = true;
                var rt = txt.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

                _faceCells.Add(cell);
            }
        }
    }
}
