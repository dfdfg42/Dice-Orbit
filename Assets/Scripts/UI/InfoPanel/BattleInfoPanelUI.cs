using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 화면 오른쪽 상시 정보 패널 (스펙: Docs/superpowers/specs/2026-07-05-battle-info-panel-design.md).
    ///
    /// ── 에디터 소유 레이아웃 ─────────────────────────────────────
    /// 섹션(헤더/액티브/패시브/상태이상/타일)의 위치·크기·배경은 씬에서 고정 슬롯으로 배치하고,
    /// 이 컴포넌트는 슬롯 참조에 "내용만" 채워 넣는다. 섹션이 비어도 슬롯 위치는 변하지 않는다.
    ///
    /// 최초 셋업: 빈 GameObject에 이 컴포넌트를 붙이고 컴포넌트 우클릭 →
    /// [기본 레이아웃 생성] 실행 → 생성된 계층을 자유롭게 스타일링/재배치 후 씬 저장.
    ///
    /// 행 모양 커스텀: cardPrefab에 GlossaryCardUI 프리팹을 꽂으면 항목이 카드로 렌더링된다.
    /// 비워두면 기본 텍스트 행(InfoPanelRows)으로 렌더링.
    /// </summary>
    public class BattleInfoPanelUI : MonoBehaviour
    {
        public static BattleInfoPanelUI Instance { get; private set; }

        [Header("헤더 슬롯 (씬에서 배치)")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI flavorText;

        [Header("섹션 컨테이너 슬롯 (씬에서 배치 — 내용 행이 이 안에 생성됨)")]
        [SerializeField] private TextMeshProUGUI activesTitle;      // 액티브 섹션 제목 (캐릭터 "액티브" / 몬스터 "다음 행동"으로 교체됨)
        [SerializeField] private RectTransform activesContainer;
        [SerializeField] private RectTransform passivesContainer;
        [SerializeField] private RectTransform statusesContainer;
        [SerializeField] private RectTransform tileContainer;
        [SerializeField] private RectTransform keywordsContainer;   // 선택 (기본 레이아웃엔 미생성)

        [Header("타일 카드 슬롯")]
        [SerializeField] private Image tileCardImage;               // 타일 카드 이미지 (스프라이트를 코드가 교체)
        [SerializeField] private TextMeshProUGUI tileMetaText;      // "#3  Normal" 표기

        [Header("기타 슬롯")]
        [SerializeField] private GameObject emptyState;             // 대상 없을 때 표시할 오브젝트

        [Header("행 프리팹 (선택 — 비우면 기본 텍스트 행)")]
        [SerializeField] private GlossaryCardUI cardPrefab;

        [Header("스킨")]
        [SerializeField] private Sprite normalTileCardSprite;
        [SerializeField] private Sprite levelUpTileCardSprite;
        [SerializeField] private TileAttributeVisualDatabase attributeVisuals;
        [SerializeField] private float refreshInterval = 0.5f;

        [Header("정렬")]
        [Tooltip("사이드바는 배경 레이어 — 일반 UI(0)와 팝업(캐릭터 액션 패널 등)이 항상 위에 그려지도록 음수 유지")]
        [SerializeField] private int panelSortingOrder = -5;

        private InfoPanelSelectionController _selection;
        private float _nextRefresh;
        private object _lastTargetKey;          // 대상 변경 감지용

        // ═══════════════════════════════════════════════════════
        // 초기화
        // ═══════════════════════════════════════════════════════

        public static void EnsureInstance()
        {
            if (Instance != null) return;

            Instance = FindFirstObjectByType<BattleInfoPanelUI>();
            if (Instance == null)
                Debug.LogWarning("[BattleInfoPanelUI] 씬에 인스턴스가 없습니다. 빈 오브젝트에 컴포넌트를 붙이고 " +
                                 "우클릭 → [기본 레이아웃 생성]으로 셋업해주세요.");
        }

        /// <summary>
        /// 패널 전체 표시/숨김. 전체 화면 UI(보상/모집/결과 등)가 열릴 때 false, 닫힐 때 true.
        /// 숨김 중에는 호버/핀 갱신도 멈춘다.
        /// </summary>
        public static void SetVisible(bool visible)
        {
            if (Instance == null) return;
            Instance.SetPanelVisible(visible);
        }

        private GameObject _canvasRoot;

        private void SetPanelVisible(bool visible)
        {
            if (_canvasRoot == null)
                _canvasRoot = transform.Find("InfoPanelCanvas")?.gameObject;

            if (_canvasRoot != null) _canvasRoot.SetActive(visible);
            if (_selection != null) _selection.enabled = visible;   // 호버/핀 갱신 중지
            enabled = visible;                                       // 렌더 루프 중지

            if (!visible)
            {
                // 패널 숨김 시 월드 인디케이터도 제거
                Visuals.PassiveRangeIndicator.Instance?.Hide();
                Visuals.IntentTileLiftEffect.Instance?.Hide();
            }
            if (visible) _lastTargetKey = new object();              // 다시 켜질 때 강제 리렌더
        }

        /// <summary>
        /// 조회 대상별 월드 인디케이터 동기화.
        /// 캐릭터 → 패시브 범위 브래킷 / 몬스터 → 공격 예정 타일 리프트 / 그 외 → 모두 숨김.
        /// </summary>
        private static void SyncWorldIndicators(IBattleInfoProvider unit)
        {
            Visuals.PassiveRangeIndicator.EnsureInstance();
            Visuals.IntentTileLiftEffect.EnsureInstance();
            var brackets = Visuals.PassiveRangeIndicator.Instance;
            var lift = Visuals.IntentTileLiftEffect.Instance;

            if (unit is Core.Character ch)
            {
                brackets?.Show(ch);
                lift?.Hide();
            }
            else if (unit is Core.Monster m)
            {
                lift?.Show(m);
                brackets?.Hide();
            }
            else
            {
                brackets?.Hide();
                lift?.Hide();
            }
        }

        /// <summary>패널 캔버스 정렬값 적용. 액션 패널 등 팝업이 항상 패널 위에 그려지게 한다.</summary>
        private void ApplyPanelSortingOrder()
        {
            var canvasTr = transform.Find("InfoPanelCanvas");
            var canvas = canvasTr != null ? canvasTr.GetComponent<Canvas>() : null;
            if (canvas != null) canvas.sortingOrder = panelSortingOrder;
        }

        /// <summary>UI 요소(파티 로스터 등)가 특정 유닛을 패널에 임시 표시. 벗어나면 ClearUnitExternal 호출.</summary>
        public void ShowUnitExternal(IBattleInfoProvider unit) => _selection?.SetExternalHover(unit);

        /// <summary>ShowUnitExternal로 지정한 표시를 해제.</summary>
        public void ClearUnitExternal(IBattleInfoProvider unit) => _selection?.ClearExternalHover(unit);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _selection = GetComponent<InfoPanelSelectionController>();
            if (_selection == null) _selection = gameObject.AddComponent<InfoPanelSelectionController>();

            if (attributeVisuals == null)
                attributeVisuals = Resources.Load<TileAttributeVisualDatabase>("UI/TileAttributeVisualDatabase");

            // 씬에 저장된 캔버스에도 최신 정렬값 강제 (기존 생성 레이아웃의 100 등 옛 값 교정)
            ApplyPanelSortingOrder();
        }

        private void Update()
        {
            object key = (object)_selection.CurrentUnit ?? _selection.CurrentTile;
            bool targetChanged = !ReferenceEquals(key, _lastTargetKey);
            if (!targetChanged && Time.unscaledTime < _nextRefresh) return;

            _lastTargetKey = key;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Render();
        }

        // ═══════════════════════════════════════════════════════
        // 렌더링 — 고정 슬롯에 내용만 채운다
        // ═══════════════════════════════════════════════════════

        private void Render()
        {
            ClearAllSlots();

            var unit = _selection.CurrentUnit;
            if (unit is Component c && c == null) unit = null;   // 파괴된 유닛 방어

            // 월드 인디케이터 연동: 캐릭터 → 패시브 브래킷 / 몬스터 → 공격 타일 리프트
            SyncWorldIndicators(unit);

            if (unit != null)
            {
                if (emptyState != null) emptyState.SetActive(false);
                var d = unit.GetBattleInfo();
                SetSectionVisibility(unitSections: true, tileSection: d.CurrentTile.HasValue);   // 몬스터는 타일 섹션 숨김
                RenderUnit(d);
                return;
            }

            var tile = _selection.CurrentTile;
            if (tile != null)
            {
                if (emptyState != null) emptyState.SetActive(false);
                SetSectionVisibility(unitSections: false, tileSection: true);   // 타일 단독 뷰: 타일 효과만
                RenderTile(tile.GetTileInfo());
                return;
            }

            SetSectionVisibility(unitSections: false, tileSection: false);      // 빈 상태: 안내 문구만
            if (emptyState != null) emptyState.SetActive(true);
        }

        private void ClearAllSlots()
        {
            SetText(nameText, "");
            SetText(hpText, "");
            SetText(flavorText, "");
            SetText(tileMetaText, "");
            SetText(ResolveActivesTitle(), "액티브");   // 기본 라벨로 복원
            ClearContainer(activesContainer);
            ClearContainer(passivesContainer);
            ClearContainer(statusesContainer);
            ClearContainer(tileContainer);
            ClearContainer(keywordsContainer);
            if (tileCardImage != null) tileCardImage.enabled = false;
        }

        private void RenderUnit(UnitInfoData d)
        {
            // ── Header ──
            SetText(nameText, d.Name);
            SetText(hpText, $"HP {d.CurrentHp}/{d.MaxHp}" + (d.Armor > 0 ? $"   방어도 {d.Armor}" : ""));
            SetText(flavorText, d.FlavorText);

            // 액티브 섹션 제목: 캐릭터 "액티브" / 몬스터 "다음 행동" (빌더가 결정)
            if (!string.IsNullOrWhiteSpace(d.ActivesLabel))
                SetText(ResolveActivesTitle(), d.ActivesLabel);

            // ── Actives ──
            if (d.Actives != null)
            {
                foreach (var a in d.Actives)
                {
                    string title = a.Level > 1 ? $"{a.Name}  Lv.{a.Level}" : a.Name;   // "Lv." 접두어는 렌더 계층 담당
                    string desc = string.IsNullOrWhiteSpace(a.DynamicDescription) ? "" : a.DynamicDescription;
                    AddEntry(activesContainer, title, a.DiceCondition, desc, Color.white);
                }
            }

            // ── Passives ──
            if (d.Passives != null)
            {
                foreach (var p in d.Passives)
                {
                    string title = p.Level > 0 ? $"{p.Name}  Lv.{p.Level}" : p.Name;
                    string desc = JoinLines(p.DynamicEffect, p.FlavorText);
                    AddEntry(passivesContainer, title, "", desc, InfoPanelRows.PassiveColor);
                }
            }

            // ── Statuses ──
            if (d.Statuses != null)
            {
                foreach (var s in d.Statuses)
                {
                    string meta = $"{s.StackText} {s.DurationText}".Trim();
                    AddEntry(statusesContainer, s.Name, meta, s.Description, s.Color);
                }
            }

            // ── Tile ──
            if (d.CurrentTile.HasValue)
                RenderTile(d.CurrentTile.Value);

            // ── Keywords (컨테이너가 배선된 경우에만) ──
            RenderKeywords(d);
        }

        private void RenderTile(TileInfoData t)
        {
            SetText(tileMetaText, $"#{t.TileIndex}  {t.Type}");

            if (tileCardImage != null)
            {
                var sprite = t.Type == TileType.LevelUp ? levelUpTileCardSprite : normalTileCardSprite;
                tileCardImage.sprite = sprite;
                tileCardImage.enabled = sprite != null;
            }

            // 속성별 행: 아이콘 + 이름 + x스택 (nT) + 설명 — 항상 표시 (스펙 §5.1)
            // 이름/설명은 속성 인스턴스 제공 (Bone/Reagent 등 오버라이드), 아이콘은 월드 버블과 동일 DB
            foreach (var a in t.Attributes)
            {
                string label = !string.IsNullOrWhiteSpace(a.DisplayName) ? a.DisplayName : a.Type.ToString();
                string desc = a.Description ?? "";
                Color tint = Color.white;
                Sprite icon = null;
                if (attributeVisuals != null && attributeVisuals.TryGet(a.Type, out var entry))
                {
                    tint = entry.iconTint;
                    icon = entry.icon;
                    // DB에 설명을 채웠으면 그것이 우선 (수동 오버라이드용)
                    if (!string.IsNullOrWhiteSpace(entry.description)) desc = entry.description;
                }
                string dur = a.Duration < 0 ? "" : $"({a.Duration}T)";   // 영구는 지속턴 표기 생략
                string stack = a.Value > 0 ? $"x{a.Value}" : "";
                AddEntry(tileContainer, label, $"{stack} {dur}".Trim(), desc, tint, icon, tint);
            }
        }

        private void RenderKeywords(UnitInfoData d)
        {
            if (keywordsContainer == null) return;

            // 액티브/패시브 설명을 합쳐 키워드 추출 (기존 ExtractMatches 재사용 — 평문 부분일치 매칭)
            var sb = new System.Text.StringBuilder();
            if (d.Actives != null)
                foreach (var a in d.Actives) sb.AppendLine(a.DynamicDescription);
            if (d.Passives != null)
                foreach (var p in d.Passives) { sb.AppendLine(p.DynamicEffect); sb.AppendLine(p.FlavorText); }

            var matches = TooltipKeywordFormatter.ExtractMatches(sb.ToString());
            if (matches == null) return;

            foreach (var k in matches)
                AddEntry(keywordsContainer, k.Key, "", k.Description, k.Color);
        }

        // ═══════════════════════════════════════════════════════
        // 슬롯 채우기 헬퍼
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 조회 대상에 맞는 섹션만 표시.
        /// 유닛 뷰: 헤더+액티브+패시브+상태이상+키워드 / 타일 뷰: 타일 섹션만 / 빈 상태: 전부 숨김.
        /// 섹션 위치는 고정(앵커)이므로 숨겨도 다른 섹션이 밀리지 않는다.
        /// </summary>
        private void SetSectionVisibility(bool unitSections, bool tileSection)
        {
            // 헤더(이름/HP/설명): nameText의 부모 오브젝트를 통째로 토글
            if (nameText != null && nameText.transform.parent != null)
                SetActiveIfChanged(nameText.transform.parent.gameObject, unitSections);

            ToggleSection(activesContainer, unitSections);
            ToggleSection(passivesContainer, unitSections);
            ToggleSection(statusesContainer, unitSections);
            ToggleSection(keywordsContainer, unitSections);
            ToggleSection(tileContainer, tileSection);
        }

        /// <summary>컨테이너의 부모(섹션 루트: 타이틀 포함)를 토글.</summary>
        private static void ToggleSection(RectTransform container, bool active)
        {
            if (container == null) return;
            var go = container.parent != null ? container.parent.gameObject : container.gameObject;
            SetActiveIfChanged(go, active);
        }

        private static void SetActiveIfChanged(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target == null) return;
            target.text = value ?? "";
        }

        private static void ClearContainer(RectTransform container)
        {
            if (container == null) return;
            InfoPanelRows.Clear(container);
        }

        /// <summary>
        /// 항목 1개(제목/메타/설명)를 컨테이너에 추가. cardPrefab이 있으면 카드로, 없으면 텍스트 행으로.
        /// icon이 있으면 제목 행 왼쪽에 아이콘 표시 (타일 속성 등).
        /// </summary>
        private void AddEntry(RectTransform container, string title, string meta, string desc, Color titleColor,
            Sprite icon = null, Color? iconTint = null)
        {
            if (container == null) return;

            if (cardPrefab != null)
            {
                var card = Instantiate(cardPrefab, container);
                card.SetStatus(title, meta, "", desc, titleColor);
                if (icon != null) card.SetIcon(icon, iconTint ?? Color.white);
                return;
            }

            string line = string.IsNullOrWhiteSpace(meta) ? title : $"{title}  {meta}";
            InfoPanelRows.AddIconTextRow(container, icon, iconTint ?? Color.white, line, 16f, titleColor, FontStyles.Bold);
            if (!string.IsNullOrWhiteSpace(desc))
                InfoPanelRows.AddText(container, desc, 13f, InfoPanelRows.MutedColor);
        }

        /// <summary>
        /// 액티브 섹션 타이틀 TMP를 찾는다. Inspector 배선이 우선, 없으면
        /// 기본 레이아웃 관례(activesContainer의 형제 "Title")로 폴백 — 이미 생성한 레이아웃도 재배선 없이 동작.
        /// </summary>
        private TextMeshProUGUI ResolveActivesTitle()
        {
            if (activesTitle != null) return activesTitle;
            var title = activesContainer != null ? activesContainer.parent?.Find("Title") : null;
            activesTitle = title != null ? title.GetComponent<TextMeshProUGUI>() : null;
            return activesTitle;
        }

        private static string JoinLines(string a, string b)
        {
            bool hasA = !string.IsNullOrWhiteSpace(a);
            bool hasB = !string.IsNullOrWhiteSpace(b);
            if (hasA && hasB) return $"{a}\n{b}";
            if (hasA) return a;
            if (hasB) return b;
            return "";
        }

        // ═══════════════════════════════════════════════════════
        // [에디터] 기본 레이아웃 생성 — 1회 실행 후 씬에서 자유롭게 스타일링
        // ═══════════════════════════════════════════════════════

        [ContextMenu("기본 레이아웃 생성")]
        private void GenerateDefaultLayout()
        {
            if (transform.Find("InfoPanelCanvas") != null)
            {
                Debug.LogWarning("[BattleInfoPanelUI] InfoPanelCanvas가 이미 있습니다. 다시 생성하려면 기존 것을 삭제하세요.");
                return;
            }

            // 캔버스
            var canvasGo = new GameObject("InfoPanelCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = panelSortingOrder;         // 배경 레이어 — 일반 UI/팝업이 항상 위
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // 오른쪽 도킹 패널 배경
            var panel = CreateRect("Panel", canvasGo.transform, new Vector2(0.70f, 0f), Vector2.one);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.11f, 0.96f);   // raycastTarget=true 유지 → 패널 위 3D 호버 차단

            // ── 고정 섹션 슬롯 (패널 내 앵커 비율) ──
            // Header 0.88~1.00
            var header = CreateRect("Header", panel, new Vector2(0.03f, 0.88f), new Vector2(0.97f, 0.995f));
            nameText   = CreateTmp("NameText",   header, new Vector2(0f, 0.55f), new Vector2(1f, 1f),    26f, Color.white, FontStyles.Bold);
            hpText     = CreateTmp("HpText",     header, new Vector2(0f, 0.30f), new Vector2(1f, 0.55f), 18f, InfoPanelRows.HpColor, FontStyles.Normal);
            flavorText = CreateTmp("FlavorText", header, new Vector2(0f, 0f),    new Vector2(1f, 0.30f), 13f, InfoPanelRows.MutedColor, FontStyles.Italic);

            // 섹션: 액티브 0.64~0.88 / 패시브 0.44~0.64 / 상태이상 0.30~0.44 / 타일 0.14~0.30 / 키워드 0.00~0.14
            activesContainer  = CreateSection("ActivesSection",  panel, "액티브",   0.64f, 0.88f);
            activesTitle      = activesContainer.parent.Find("Title").GetComponent<TextMeshProUGUI>();
            passivesContainer = CreateSection("PassivesSection", panel, "패시브",   0.44f, 0.64f);
            statusesContainer = CreateSection("StatusesSection", panel, "상태이상", 0.30f, 0.44f);
            tileContainer     = CreateSection("TileSection",     panel, "밟고 있는 타일", 0.14f, 0.30f);
            keywordsContainer = CreateSection("KeywordsSection", panel, "키워드",   0.00f, 0.14f);

            // 타일 섹션 부속: 카드 이미지(왼쪽) + 메타 텍스트
            var tileSection = tileContainer.parent;
            var cardRect = CreateRect("TileCardImage", tileSection, new Vector2(0f, 0.30f), new Vector2(0.30f, 0.85f));
            tileCardImage = cardRect.gameObject.AddComponent<Image>();
            tileCardImage.preserveAspect = true;
            tileCardImage.raycastTarget = false;
            tileCardImage.enabled = false;
            tileMetaText = CreateTmp("TileMetaText", tileSection, new Vector2(0f, 0.06f), new Vector2(0.30f, 0.28f), 12f, InfoPanelRows.MutedColor, FontStyles.Normal);
            // 카드 이미지가 왼쪽 30%를 쓰므로 속성 행 컨테이너를 오른쪽으로 밀어준다
            tileContainer.anchorMin = new Vector2(0.32f, 0.02f);
            tileContainer.anchorMax = new Vector2(1f, 0.85f);

            // 빈 상태 안내
            var emptyRect = CreateRect("EmptyState", panel, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.55f));
            var emptyTmp = emptyRect.gameObject.AddComponent<TextMeshProUGUI>();
            emptyTmp.text = "캐릭터나 몬스터에 마우스를 올리거나\n클릭해 고정하세요.";
            emptyTmp.fontSize = 16f;
            emptyTmp.color = InfoPanelRows.MutedColor;
            emptyTmp.alignment = TextAlignmentOptions.Center;
            emptyTmp.raycastTarget = false;
            emptyState = emptyRect.gameObject;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            Debug.Log("[BattleInfoPanelUI] 기본 레이아웃 생성 완료 — 계층을 자유롭게 스타일링한 뒤 씬을 저장하세요.");
        }

        /// <summary>
        /// [에디터] 이미 생성한 레이아웃에 키워드 섹션만 추가한다 (기존 스타일링 보존).
        /// 패널 하단(0~0.14)에 생성되므로 기존 타일 섹션과 겹치면 씬에서 재배치할 것.
        /// </summary>
        [ContextMenu("키워드 섹션만 추가")]
        private void AddKeywordsSection()
        {
            if (keywordsContainer != null)
            {
                Debug.LogWarning("[BattleInfoPanelUI] keywordsContainer가 이미 배선돼 있습니다.");
                return;
            }

            var panel = transform.Find("InfoPanelCanvas/Panel");
            if (panel == null)
            {
                Debug.LogWarning("[BattleInfoPanelUI] InfoPanelCanvas/Panel을 찾을 수 없습니다. 먼저 [기본 레이아웃 생성]을 실행하세요.");
                return;
            }

            keywordsContainer = CreateSection("KeywordsSection", panel, "키워드", 0.00f, 0.14f);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            Debug.Log("[BattleInfoPanelUI] 키워드 섹션 추가 완료 — 기존 섹션과 겹치면 씬에서 위치를 조정하세요.");
        }

        /// <summary>섹션 슬롯 생성: 고정 타이틀 + 내용 컨테이너(세로 쌓기, 넘침 클리핑). 내용 컨테이너를 반환.</summary>
        private RectTransform CreateSection(string name, Transform parent, string title, float yMin, float yMax)
        {
            var section = CreateRect(name, parent, new Vector2(0.03f, yMin + 0.005f), new Vector2(0.97f, yMax - 0.005f));

            // 타이틀 (코드가 건드리지 않음 — 씬에서 자유 수정)
            var titleTmp = CreateTmp("Title", section, new Vector2(0f, 0.86f), new Vector2(1f, 1f), 18f, InfoPanelRows.SectionTitleColor, FontStyles.Bold);
            titleTmp.text = title;

            // 내용 컨테이너
            var content = CreateRect("Content", section, new Vector2(0f, 0f), new Vector2(1f, 0.86f));
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childAlignment = TextAnchor.UpperLeft;
            content.gameObject.AddComponent<RectMask2D>();   // 넘치는 내용은 잘림 (슬롯 크기 고정)

            return content;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static TextMeshProUGUI CreateTmp(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            float size, Color color, FontStyles style)
        {
            var rect = CreateRect(name, parent, anchorMin, anchorMax);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
