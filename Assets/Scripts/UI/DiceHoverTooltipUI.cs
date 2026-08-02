using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 주사위 호버 툴팁 — 호버한 주사위 위에 6면(2줄×3, 실제 주사위 이미지+숫자)과
    /// 사용 효과를 표시. 효과는 타일 정보처럼 효과별로 카드 셀을 동적 생성한다.
    /// 씬에 배치하거나 EnsureInstance로 자동 생성.
    /// </summary>
    public class DiceHoverTooltipUI : MonoBehaviour
    {
        public static DiceHoverTooltipUI Instance { get; private set; }

        [Header("참조 (씬 배치 시)")]
        [SerializeField] private RectTransform panel;        // 툴팁 루트 패널
        [SerializeField] private RectTransform faceGrid;     // 3열 GridLayoutGroup (6면)
        [SerializeField] private RectTransform effectRow;    // 효과 카드 컨테이너 (세로 스택)
        [SerializeField] private Sprite dieFaceSprite;       // Assets/Sprites/Dice.png
        [SerializeField] private TMP_FontAsset font;         // 숫자/라벨 폰트 (Pretendard SDF 권장)
        [SerializeField] private Vector2 aboveOffset = new Vector2(0f, 90f);

        // 색
        private static readonly Color Ink  = new Color(0.12f, 0.11f, 0.16f);   // 진한 잉크 (밝은 배경 위)
        private static readonly Color Card = new Color(0.95f, 0.93f, 0.885f, 0.98f); // 크림 카드

        private readonly List<GameObject> _faceCells = new List<GameObject>();
        private readonly List<GameObject> _effectCells = new List<GameObject>();

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
            BuildEffects(src.Effect);

            panel.gameObject.SetActive(true);
            panel.position = (Vector2)element.transform.position + aboveOffset;
            panel.SetAsLastSibling();
        }

        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        /// <summary>면 6칸 (각 칸 = 주사위 이미지 + 숫자). GridLayoutGroup가 3열×2줄로 정렬.</summary>
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
                img.preserveAspect = true;
                img.raycastTarget = false;

                var txt = new GameObject("Val", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                txt.transform.SetParent(cell.transform, false);   // 자식 = 이미지 위에 렌더
                txt.text = v.ToString();
                txt.alignment = TextAlignmentOptions.Center;
                txt.fontSize = 22;
                txt.fontStyle = FontStyles.Bold;
                txt.color = Ink;
                txt.raycastTarget = false;
                if (font != null) txt.font = font;
                var rt = txt.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

                _faceCells.Add(cell);
            }
        }

        /// <summary>효과별로 카드 셀을 동적 생성 (타일 정보 카드 감각). 효과 없으면 컨테이너 숨김.</summary>
        private void BuildEffects(DieEffect effect)
        {
            foreach (var c in _effectCells) Destroy(c);
            _effectCells.Clear();

            // 현재는 주사위당 효과 1개. 여러 개가 되면 여기서 순회.
            var effects = new List<DieEffect>();
            if (effect != null) effects.Add(effect);

            if (effectRow != null) effectRow.gameObject.SetActive(effects.Count > 0);
            foreach (var e in effects) CreateEffectCell(e);
        }

        private void CreateEffectCell(DieEffect effect)
        {
            var cell = new GameObject("EffectCell", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            cell.transform.SetParent(effectRow, false);

            var bg = cell.GetComponent<Image>();
            bg.sprite = UiRoundedSprite.Get(10);
            bg.type = Image.Type.Sliced;
            bg.color = Card;
            bg.raycastTarget = false;

            var hl = cell.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(8, 8, 4, 4);
            hl.spacing = 6f;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            hl.childAlignment = TextAnchor.MiddleLeft;
            cell.GetComponent<LayoutElement>().preferredHeight = 28f;

            if (effect.Icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconGo.transform.SetParent(cell.transform, false);
                var im = iconGo.GetComponent<Image>();
                im.sprite = effect.Icon; im.preserveAspect = true; im.raycastTarget = false;
                var le = iconGo.GetComponent<LayoutElement>();
                le.preferredWidth = 20; le.preferredHeight = 20;
            }

            var lbl = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            lbl.transform.SetParent(cell.transform, false);
            lbl.text = effect.Preview();
            lbl.fontSize = 17;
            lbl.color = Ink;
            lbl.raycastTarget = false;
            if (font != null) lbl.font = font;

            _effectCells.Add(cell);
        }
    }
}
