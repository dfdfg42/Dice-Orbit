using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 화면 오른쪽 1/3 상시 정보 패널 (스펙: Docs/superpowers/specs/2026-07-05-battle-info-panel-design.md).
    /// UI 계층을 코드로 생성한다 (씬 수작업 의존 최소화). 표시 대상은 InfoPanelSelectionController가 결정.
    /// 서식(색/태그/"Lv." 접두어)은 전부 이 렌더 계층에서 붙인다 — 데이터는 원시값 (서식 제로 원칙).
    /// </summary>
    public class BattleInfoPanelUI : MonoBehaviour
    {
        public static BattleInfoPanelUI Instance { get; private set; }

        [Header("스킨 (선택 — 비우면 해당 요소 생략)")]
        [SerializeField] private Sprite normalTileCardSprite;
        [SerializeField] private Sprite levelUpTileCardSprite;
        [SerializeField] private TileAttributeVisualDatabase attributeVisuals;

        [Header("레이아웃")]
        [SerializeField, Range(0.15f, 0.5f)] private float panelWidthRatio = 0.30f;
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.11f, 0.96f);
        [SerializeField] private float refreshInterval = 0.5f;

        private InfoPanelSelectionController _selection;
        private RectTransform _content;         // 스크롤 내용 (섹션 행들이 붙는 곳)
        private float _nextRefresh;
        private object _lastTargetKey;          // 대상 변경 감지용

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            Instance = FindFirstObjectByType<BattleInfoPanelUI>();
            if (Instance == null)
            {
                var go = new GameObject("BattleInfoPanelUI");
                Instance = go.AddComponent<BattleInfoPanelUI>();
            }
        }

        /// <summary>UI 요소(파티 로스터 등)가 특정 유닛을 패널에 임시 표시. 벗어나면 ClearUnitExternal 호출.</summary>
        public void ShowUnitExternal(IBattleInfoProvider unit) => _selection?.SetExternalHover(unit);

        /// <summary>ShowUnitExternal로 지정한 표시를 해제.</summary>
        public void ClearUnitExternal(IBattleInfoProvider unit) => _selection?.ClearExternalHover(unit);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _selection = gameObject.AddComponent<InfoPanelSelectionController>();
            BuildLayout();

            if (attributeVisuals == null)
                attributeVisuals = Resources.Load<TileAttributeVisualDatabase>("UI/TileAttributeVisualDatabase");
        }

        private void Update()
        {
            object key = (object)_selection.CurrentUnit ?? _selection.HoveredTile;
            bool targetChanged = !ReferenceEquals(key, _lastTargetKey);
            if (!targetChanged && Time.unscaledTime < _nextRefresh) return;

            _lastTargetKey = key;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Render();
        }

        // ═══════════════════════════════════════════════════════
        // 레이아웃 생성 (1회)
        // ═══════════════════════════════════════════════════════

        private void BuildLayout()
        {
            var canvasGo = new GameObject("InfoPanelCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;                       // 툴팁(30000)보다 아래, 일반 UI보다 위
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // 오른쪽 도킹 패널 (앵커로 폭 비율 고정)
            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(1f - panelWidthRatio, 0f);
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var bg = panelGo.AddComponent<Image>();
            bg.color = backgroundColor;                      // raycastTarget=true 유지 → 패널 위 3D 호버 차단

            // 스크롤 영역
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(panelGo.transform, false);
            var scrollRectTr = (RectTransform)scrollGo.transform;
            scrollRectTr.anchorMin = Vector2.zero;
            scrollRectTr.anchorMax = Vector2.one;
            scrollRectTr.offsetMin = new Vector2(16f, 16f);
            scrollRectTr.offsetMax = new Vector2(-16f, -16f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scrollGo.AddComponent<RectMask2D>();
            scroll.horizontal = false;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            _content = (RectTransform)contentGo.transform;
            _content.anchorMin = new Vector2(0f, 1f);        // 상단 스트레치
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _content;
        }

        // ═══════════════════════════════════════════════════════
        // 렌더링 (대상 변경 시 + refreshInterval마다)
        // ═══════════════════════════════════════════════════════

        private void Render()
        {
            InfoPanelRows.Clear(_content);

            var unit = _selection.CurrentUnit;
            if (unit is Component c && c == null) unit = null;   // 파괴된 유닛 방어

            if (unit != null)
            {
                RenderUnit(unit.GetBattleInfo());
                return;
            }

            if (_selection.HoveredTile != null)
            {
                RenderTileSection(_selection.HoveredTile.GetTileInfo(), standalone: true);
                return;
            }

            InfoPanelRows.AddText(_content, "캐릭터나 몬스터에 마우스를 올리거나\n클릭해 고정하세요.", 16f, InfoPanelRows.MutedColor);
        }

        private void RenderUnit(UnitInfoData d)
        {
            // ── Header ──
            InfoPanelRows.AddText(_content, d.Name, 26f, Color.white, FontStyles.Bold);
            string hpLine = $"HP {d.CurrentHp}/{d.MaxHp}" + (d.Armor > 0 ? $"   방어도 {d.Armor}" : "");
            InfoPanelRows.AddText(_content, hpLine, 18f, InfoPanelRows.HpColor);
            if (!string.IsNullOrWhiteSpace(d.FlavorText))
                InfoPanelRows.AddText(_content, d.FlavorText, 14f, InfoPanelRows.MutedColor, FontStyles.Italic);
            InfoPanelRows.AddDivider(_content);

            // ── Actives ──
            if (d.Actives != null && d.Actives.Count > 0)
            {
                InfoPanelRows.AddSectionTitle(_content, "액티브");
                foreach (var a in d.Actives)
                {
                    string title = a.Level > 1 ? $"{a.Name}  Lv.{a.Level}" : a.Name;   // "Lv." 접두어는 렌더 계층 담당
                    InfoPanelRows.AddText(_content, title, 17f, Color.white, FontStyles.Bold);
                    if (!string.IsNullOrWhiteSpace(a.DiceCondition))
                        InfoPanelRows.AddText(_content, a.DiceCondition, 14f, InfoPanelRows.DiceColor);
                    if (!string.IsNullOrWhiteSpace(a.DynamicDescription))
                        InfoPanelRows.AddText(_content, a.DynamicDescription, 14f, InfoPanelRows.MutedColor);
                }
                InfoPanelRows.AddDivider(_content);
            }

            // ── Passives ──
            if (d.Passives != null && d.Passives.Count > 0)
            {
                InfoPanelRows.AddSectionTitle(_content, "패시브");
                foreach (var p in d.Passives)
                {
                    string title = p.Level > 0 ? $"{p.Name}  Lv.{p.Level}" : p.Name;
                    InfoPanelRows.AddText(_content, title, 17f, InfoPanelRows.PassiveColor, FontStyles.Bold);
                    if (!string.IsNullOrWhiteSpace(p.DynamicEffect))
                        InfoPanelRows.AddText(_content, p.DynamicEffect, 14f, Color.white);
                    if (!string.IsNullOrWhiteSpace(p.FlavorText))
                        InfoPanelRows.AddText(_content, p.FlavorText, 13f, InfoPanelRows.MutedColor);
                }
                InfoPanelRows.AddDivider(_content);
            }

            // ── Statuses ──
            if (d.Statuses != null && d.Statuses.Count > 0)
            {
                InfoPanelRows.AddSectionTitle(_content, "상태이상");
                foreach (var s in d.Statuses)
                {
                    string meta = $"{s.StackText} {s.DurationText}".Trim();
                    InfoPanelRows.AddText(_content, meta.Length > 0 ? $"{s.Name}  {meta}" : s.Name, 16f, s.Color, FontStyles.Bold);
                    if (!string.IsNullOrWhiteSpace(s.Description))
                        InfoPanelRows.AddText(_content, s.Description, 13f, InfoPanelRows.MutedColor);
                }
                InfoPanelRows.AddDivider(_content);
            }

            // ── Tile ──
            if (d.CurrentTile.HasValue)
                RenderTileSection(d.CurrentTile.Value, standalone: false);

            // ── Keywords ──
            RenderKeywords(d);
        }

        private void RenderTileSection(TileInfoData t, bool standalone)
        {
            InfoPanelRows.AddSectionTitle(_content, standalone ? $"타일 #{t.TileIndex}" : "밟고 있는 타일");

            // 타일 카드 이미지 (스프라이트 미지정 시 생략)
            var sprite = t.Type == TileType.LevelUp ? levelUpTileCardSprite : normalTileCardSprite;
            if (sprite != null)
            {
                var cardGo = new GameObject("TileCard", typeof(RectTransform));
                cardGo.transform.SetParent(_content, false);
                var img = cardGo.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                cardGo.AddComponent<LayoutElement>().preferredHeight = 120f;
            }
            InfoPanelRows.AddText(_content, $"#{t.TileIndex}  {t.Type}", 14f, InfoPanelRows.MutedColor);

            // 속성별 행: 라벨 + x스택 (nT) + 설명 — 숨김 인터랙션 없이 항상 표시 (스펙 §5.1)
            foreach (var a in t.Attributes)
            {
                string label = a.Type.ToString();
                Color tint = Color.white;
                string desc = "";
                if (attributeVisuals != null && attributeVisuals.TryGet(a.Type, out var entry))
                {
                    if (!string.IsNullOrWhiteSpace(entry.shortLabel)) label = entry.shortLabel;
                    tint = entry.iconTint;
                    desc = entry.description ?? "";
                }
                string dur = a.Duration < 0 ? "(∞T)" : $"({a.Duration}T)";
                string stack = a.Value > 0 ? $"x{a.Value} " : "";
                InfoPanelRows.AddText(_content, $"{label}  {stack}{dur}", 15f, tint, FontStyles.Bold);
                if (!string.IsNullOrWhiteSpace(desc))
                    InfoPanelRows.AddText(_content, desc, 13f, InfoPanelRows.MutedColor);
            }
        }

        private void RenderKeywords(UnitInfoData d)
        {
            // 액티브/패시브 설명을 합쳐 키워드 추출 (기존 ExtractMatches 재사용 — 평문 부분일치 매칭)
            var sb = new System.Text.StringBuilder();
            if (d.Actives != null)
                foreach (var a in d.Actives) sb.AppendLine(a.DynamicDescription);
            if (d.Passives != null)
                foreach (var p in d.Passives) { sb.AppendLine(p.DynamicEffect); sb.AppendLine(p.FlavorText); }

            var matches = TooltipKeywordFormatter.ExtractMatches(sb.ToString());
            if (matches == null || matches.Count == 0) return;

            InfoPanelRows.AddSectionTitle(_content, "키워드");
            foreach (var k in matches)
            {
                InfoPanelRows.AddText(_content, k.Key, 15f, k.Color, FontStyles.Bold);
                if (!string.IsNullOrWhiteSpace(k.Description))
                    InfoPanelRows.AddText(_content, k.Description, 13f, InfoPanelRows.MutedColor);
            }
        }
    }
}
