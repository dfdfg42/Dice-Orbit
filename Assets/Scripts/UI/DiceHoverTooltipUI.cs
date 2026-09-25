using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 주사위 호버 툴팁 — 호버한 주사위 위에 6면(2줄×3, 실제 주사위 이미지+숫자)과
    /// 사용 효과를 표시. 효과는 타일 정보처럼 효과별로 카드 셀을 동적 생성한다.
    /// 씬에 배치된 패널(DHT_Panel)을 그대로 쓴다 — 첫 표시 때 전용 최상위 캔버스(800)로
    /// 옮겨 보상창(500) 위에서도 항상 보이게만 한다.
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
        private static UiSkin Skin => UiSkin.Current;   // 팔레트·스프라이트 단일 권위 (2026-09-25)

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

        /// <summary>면 아트 스프라이트 공유 — 보상/교체 화면의 주사위 카드가 같은 아트를 쓴다.</summary>
        public Sprite DieFaceSprite => dieFaceSprite;

        public void Show(DiceElement element)
        {
            var src = element != null ? element.Data?.Source : null;
            bool skillHint = element != null && element.SkillUnusableHint;
            if ((src == null && !skillHint) || panel == null) return;

            ShowCore(src?.Faces, src?.Effect,
                (Vector2)element.transform.position,
                skillHint ? "강화 조건 불충족 · 기본 공격 발동" : null);
        }

        /// <summary>
        /// 위치 지정형 표시 — 보상 타일/교체 카드 등 다이스 패널 밖에서도 같은 GUI(6면 아트+효과)를 띄운다.
        /// anchorPos는 오버레이 캔버스 기준 위치 (카드 상단 중앙 권장).
        /// </summary>
        public void ShowAt(int[] faces, DieEffect effect, Vector2 anchorPos)
        {
            if (panel == null || faces == null) return;
            ShowCore(faces, effect, anchorPos, null);
        }

        private void ShowCore(int[] faces, DieEffect effect, Vector2 anchorPos, string skillHintText)
        {
            EnsureAutoLayout();

            BuildFaces(faces);
            if (faceGrid != null) faceGrid.gameObject.SetActive(faces != null);
            BuildEffects(effect, skillHintText);

            panel.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);   // 첫 프레임부터 내용 크기로
            panel.position = anchorPos + aboveOffset;
            panel.SetAsLastSibling();
        }

        private bool _layoutReady;

        /// <summary>
        /// 패널이 내용(글자 길이·셀 개수)에 맞춰 늘어나도록 런타임에 CSF+VLG를 얹는다.
        /// 씬의 DHT_Panel은 고정 325×145 수동 배치라, 씬 수정 없이 코드에서 1회 구성.
        /// </summary>
        private void EnsureAutoLayout()
        {
            if (_layoutReady || panel == null) return;
            _layoutReady = true;

            // 가변 높이에서도 주사위 위 간격이 일정하도록 피벗을 하단 중앙으로.
            // 기존(중앙 피벗 +90, 고정 높이 145)의 하단 라인(+17.5)을 유지하게 오프셋 변환.
            aboveOffset = new Vector2(aboveOffset.x, aboveOffset.y - 72.5f);
            panel.pivot = new Vector2(0.5f, 0f);

            // 보상 화면(루트 오버레이 캔버스, sortingOrder 500) 위에서도 보이도록 패널을
            // '전용 루트 오버레이 캔버스(800)'로 옮긴다 — 중첩 캔버스의 overrideSorting은
            // 같은 루트 안에서만 유효해서 다른 루트 캔버스를 이기지 못한다 (2026-08-28 확인).
            var rootScaler = panel.GetComponentInParent<CanvasScaler>();

            var canvasGo = new GameObject("DiceTooltipCanvas", typeof(RectTransform));
            var topCanvas = canvasGo.AddComponent<Canvas>();
            topCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            topCanvas.sortingOrder = 800;

            // 원래 캔버스의 스케일러를 복사해 패널 크기·좌표 체계를 그대로 유지한다.
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            if (rootScaler != null)
            {
                scaler.uiScaleMode = rootScaler.uiScaleMode;
                scaler.referenceResolution = rootScaler.referenceResolution;
                scaler.screenMatchMode = rootScaler.screenMatchMode;
                scaler.matchWidthOrHeight = rootScaler.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = rootScaler.referencePixelsPerUnit;
            }

            panel.SetParent(canvasGo.transform, false);

            var group = panel.gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var vlg = panel.gameObject.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 12, 12);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var fit = panel.gameObject.GetComponent<ContentSizeFitter>();
            if (fit == null) fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            if (effectRow != null)
            {
                effectRow.SetSiblingIndex(0);   // 씬과 같은 순서 유지: 효과 카드 위, 6면 그리드 아래
                var row = effectRow.GetComponent<VerticalLayoutGroup>();
                if (row != null)
                {
                    row.childForceExpandWidth = false;   // 카드가 글자 폭만큼만
                    row.childAlignment = TextAnchor.MiddleCenter;
                }
            }
        }

        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        /// <summary>면 6칸 (각 칸 = 주사위 이미지 + 숫자). GridLayoutGroup가 3열×2줄로 정렬.</summary>
        private void BuildFaces(int[] faces)
        {
            if (faceGrid == null) return;
            foreach (var c in _faceCells) { if (c != null) c.SetActive(false); Destroy(c); }   // 비활성화 → 이번 프레임 레이아웃에서 제외
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
                txt.color = Skin.Ink;
                txt.raycastTarget = false;
                if (font != null) txt.font = font;
                var rt = txt.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

                _faceCells.Add(cell);
            }
        }

        /// <summary>효과별로 카드 셀을 동적 생성 (타일 정보 카드 감각). 효과도 힌트도 없으면 컨테이너 숨김.
        /// 면 구성·확률 설명은 넣지 않는다 — 6면 그리드가 이미 전부 보여준다 (2026-08-28 결정).</summary>
        private void BuildEffects(DieEffect effect, string skillHintText = null)
        {
            foreach (var c in _effectCells) { if (c != null) c.SetActive(false); Destroy(c); }
            _effectCells.Clear();

            var effects = new List<DieEffect>();
            if (effect != null) effects.Add(effect);

            bool hasHint = !string.IsNullOrEmpty(skillHintText);
            if (effectRow != null) effectRow.gameObject.SetActive(effects.Count > 0 || hasHint);
            foreach (var e in effects) CreateEffectCell(e);
            if (hasHint) CreateCell(skillHintText, null, HintRed);
        }

        private static readonly Color HintRed = new Color(0.78f, 0.22f, 0.25f);

        private void CreateEffectCell(DieEffect effect) => CreateCell(effect.Preview(), effect.Icon, Skin.Ink);

        private void CreateCell(string label, Sprite cellIcon, Color textColor)
        {
            var cell = new GameObject("EffectCell", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            cell.transform.SetParent(effectRow, false);

            var bg = cell.GetComponent<Image>();
            Skin.ApplyCard(bg);
            bg.raycastTarget = false;

            var hl = cell.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(8, 8, 4, 4);
            hl.spacing = 6f;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            hl.childAlignment = TextAnchor.MiddleLeft;
            cell.GetComponent<LayoutElement>().preferredHeight = 28f;

            if (cellIcon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconGo.transform.SetParent(cell.transform, false);
                var im = iconGo.GetComponent<Image>();
                im.sprite = cellIcon; im.preserveAspect = true; im.raycastTarget = false;
                var le = iconGo.GetComponent<LayoutElement>();
                le.preferredWidth = 20; le.preferredHeight = 20;
            }

            var lbl = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            lbl.transform.SetParent(cell.transform, false);
            lbl.text = label;
            lbl.fontSize = 17;
            lbl.color = textColor;
            lbl.raycastTarget = false;
            lbl.enableWordWrapping = false;   // 카드가 글자 폭에 맞춰 늘어나므로 줄바꿈 없이 한 줄
            if (font != null) lbl.font = font;

            _effectCells.Add(cell);
        }
    }
}
