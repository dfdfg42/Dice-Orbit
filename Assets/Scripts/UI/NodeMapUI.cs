using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 노드맵 화면 (GameState.Map). 세로 진행형: 위 출발 → 아래 보스, 세로 스크롤.
    /// RunManager의 MapGraph를 그리고, 선택 가능 노드 클릭 → GameFlowManager.OnNodeSelected.
    ///
    /// "보드게임의 밤" 팔레트. 에디터 소유:
    /// 씬에 캔버스/타이틀/스크롤 뼈대를 배치하고 슬롯을 배선한다.
    /// 노드·간선은 맵이 런마다 랜덤이라 MapRoot(Content) 아래에 코드가 채운다.
    /// </summary>
    public class NodeMapUI : MonoBehaviour
    {
        public static NodeMapUI Instance { get; private set; }

        [Header("슬롯 (씬에서 배치 후 배선)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform mapRoot;      // 스크롤 Content — 노드/간선이 그려지는 영역
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image backgroundImage;      // 배경 (씬 배치 — 스크롤뷰 밖이면 고정, mapRoot 안이면 맵과 함께 스크롤)

        [Header("배경 스킨 (선택 — 비우면 펠트색)")]
        [SerializeField] private Sprite backgroundSprite;

        [Header("노드 스킨 (선택 — 비우면 텍스트 카드). 통짜 이미지, 상태는 색조/테두리로 표현")]
        [SerializeField] private Sprite battleSprite;
        [SerializeField] private Sprite eliteSprite;
        [SerializeField] private Sprite shopSprite;
        [SerializeField] private Sprite restSprite;
        [SerializeField] private Sprite eventSprite;
        [SerializeField] private Sprite bossSprite;

        [Header("간선 점선 (점 스프라이트는 선택 — 비우면 원형 점 자동 생성)")]
        [SerializeField] private Sprite edgeDotSprite;
        [SerializeField] private float edgeDotSize = 6f;
        [SerializeField] private float edgeDotSpacing = 16f;

        [Header("배치")]
        [SerializeField] private float nodeSize = 72f;
        [SerializeField] private float laneSpacing = 170f;   // 층 내 가로 간격
        [SerializeField] private float floorSpacing = 170f;  // 층 간 세로 간격
        [SerializeField] private float verticalMargin = 130f; // 맨 아래/위 여백

        // 보드게임의 밤 팔레트
        private static readonly Color Felt     = new Color(0.043f, 0.051f, 0.078f, 0.97f);
        private static readonly Color Card     = new Color(0.118f, 0.133f, 0.200f);
        private static readonly Color CardEdge = new Color(0.239f, 0.271f, 0.400f);
        private static readonly Color Ink      = new Color(0.910f, 0.894f, 0.847f);
        private static readonly Color Gold     = new Color(0.878f, 0.702f, 0.341f);
        private static readonly Color Dim      = new Color(0.35f, 0.35f, 0.38f);

        private TMP_FontAsset _font;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            Instance = FindFirstObjectByType<NodeMapUI>(FindObjectsInactive.Include);
            if (Instance == null)
                Debug.LogError("[NodeMapUI] 씬에 NodeMapUI가 없습니다 — 씬 배치 전용입니다 (런타임 생성 없음).");
        }

        // ── 공개 API ──────────────────────────────────────────

        public void Show()
        {
            if (rootCanvas == null)
            {
                Debug.LogError("[NodeMapUI] rootCanvas 미배선 — 씬에서 슬롯을 연결하세요 (런타임 생성 없음).");
                return;
            }
            rootCanvas.SetActive(true);
            BattleInfoPanelUI.SetVisible(false);

            if (backgroundImage != null)
            {
                backgroundImage.sprite = backgroundSprite;
                backgroundImage.color = backgroundSprite != null ? Color.white : Felt;
            }

            Rebuild();
        }

        public void Hide()
        {
            if (rootCanvas != null) rootCanvas.SetActive(false);
            BattleInfoPanelUI.SetVisible(true);
        }

        /// <summary>맵 그래프를 다시 그린다 (노드 상태 변화 시 호출).</summary>
        public void Rebuild()
        {
            if (mapRoot == null) return;

            for (int i = mapRoot.childCount - 1; i >= 0; i--)
                Destroy(mapRoot.GetChild(i).gameObject);

            var run = RunManager.Instance;
            if (run == null || !run.RunActive)
            {
                Debug.LogWarning("[NodeMapUI] RunManager가 없거나 런이 시작되지 않았습니다.");
                return;
            }

            var map = run.Map;
            if (titleText != null)
                titleText.text = run.CurrentAct != null ? run.CurrentAct.ActName : "노드맵";

            // Content 높이 = 층 수 × 간격 + 상하 여백 (위→아래 진행)
            float contentHeight = verticalMargin * 2f + (map.FloorCount - 1) * floorSpacing;
            mapRoot.sizeDelta = new Vector2(mapRoot.sizeDelta.x, contentHeight);

            // 노드 위치 계산 — 층 0이 맨 위, 보스가 맨 아래
            var positions = new Dictionary<int, Vector2>();
            for (int f = 0; f < map.FloorCount; f++)
            {
                var floorNodes = map.GetFloor(f);
                float y = contentHeight - verticalMargin - f * floorSpacing;
                for (int i = 0; i < floorNodes.Count; i++)
                {
                    float x = (i - (floorNodes.Count - 1) * 0.5f) * laneSpacing;
                    positions[floorNodes[i].Id] = new Vector2(x, y);
                }
            }

            var selectable = run.GetSelectableNodes();
            var selectableIds = new HashSet<int>();
            foreach (var s in selectable) selectableIds.Add(s.Id);
            int currentId = run.CurrentNode?.Id ?? -1;

            // 간선 (노드 아래 레이어)
            foreach (var node in map.Nodes)
            {
                foreach (int nextId in node.Next)
                {
                    bool active = node.Id == currentId && selectableIds.Contains(nextId);
                    bool traveled = node.Visited && map.Get(nextId) != null && map.Get(nextId).Visited;
                    Color c = active ? Gold : traveled ? CardEdge : new Color(0.25f, 0.27f, 0.34f);
                    CreateEdge(positions[node.Id], positions[nextId], c, active ? 5f : 3f);
                }
            }

            // 노드
            foreach (var node in map.Nodes)
                CreateNodeButton(node, positions[node.Id], selectableIds.Contains(node.Id), node.Id == currentId);

            // 현재 위치로 자동 스크롤 (출발 전이면 맨 아래)
            ScrollToFloor(run.CurrentNode?.Floor ?? 0, map.FloorCount);
        }

        private void ScrollToFloor(int floor, int floorCount)
        {
            if (scrollRect == null) return;

            Canvas.ForceUpdateCanvases();

            float contentH = mapRoot.rect.height;
            float viewportH = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 900f;
            if (contentH <= viewportH) { scrollRect.verticalNormalizedPosition = 1f; return; }

            // 현재 층이 뷰포트 중앙보다 약간 위에 오도록 (위→아래 진행)
            float nodeY = contentH - verticalMargin - floor * floorSpacing;
            float targetBottom = nodeY - viewportH * 0.6f;
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(targetBottom / (contentH - viewportH));
        }

        // ── 렌더링 ────────────────────────────────────────────

        private void CreateEdge(Vector2 from, Vector2 to, Color color, float thickness)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.01f) return;
            Vector2 dir = delta / length;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float dotSize = edgeDotSize * (thickness >= 5f ? 1.5f : 1f);   // 활성 경로는 점을 굵게

            // 노드 밑에 깔리는 양 끝 구간은 건너뛰고 점을 찍는다
            float margin = nodeSize * 0.55f;
            for (float d = margin; d <= length - margin; d += edgeDotSpacing)
            {
                var dotGo = new GameObject("Dot", typeof(RectTransform));
                dotGo.transform.SetParent(mapRoot, false);
                var rect = (RectTransform)dotGo.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);   // Content 아래 중앙 기준
                rect.anchoredPosition = from + dir * d;
                rect.sizeDelta = new Vector2(dotSize, dotSize);
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);      // 방향성 스프라이트(발자국 등) 정렬용

                var img = dotGo.AddComponent<Image>();
                if (edgeDotSprite != null)
                {
                    img.sprite = edgeDotSprite;
                    img.preserveAspect = true;
                }
                else
                {
                    img.sprite = UiRoundedSprite.Get(Mathf.CeilToInt(dotSize * 0.5f));   // 원형 점
                }
                img.color = color;
                img.raycastTarget = false;
            }
        }

        private void CreateNodeButton(MapNode node, Vector2 pos, bool isSelectable, bool isCurrent)
        {
            float size = node.Type == MapNodeType.Boss ? nodeSize * 1.4f : nodeSize;
            var skin = GetNodeSprite(node.Type);

            var go = new GameObject($"Node_{node.Id}", typeof(RectTransform));
            go.transform.SetParent(mapRoot, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);   // Content 아래 중앙 기준
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(size, size);

            // 테두리: 금 = 선택 가능, 흰(잉크) = 현재 위치 (스킨 모드 — 금색 카드 배경 대체)
            if (isSelectable)
                CreateNodeRing(go.transform, Gold);
            else if (isCurrent && skin != null)
                CreateNodeRing(go.transform, Ink);

            var img = go.AddComponent<Image>();
            if (skin != null)
            {
                // 통짜 이미지 스킨 — 상태는 색조로 (원색 = 현재/선택가능, 흐림 = 방문/잠김)
                img.sprite = skin;
                img.preserveAspect = true;
                img.color = node.Visited && !isCurrent ? new Color(0.45f, 0.45f, 0.50f, 0.85f)
                          : (isCurrent || isSelectable) ? Color.white
                          : new Color(0.60f, 0.60f, 0.66f);
            }
            else
            {
                img.sprite = UiRoundedSprite.Get(16);
                img.type = Image.Type.Sliced;
                img.color = isCurrent ? Gold
                          : node.Visited ? new Color(Dim.r, Dim.g, Dim.b, 0.55f)
                          : isSelectable ? CardEdge
                          : Card;
            }

            // 라벨 (스킨이 없을 때만 — 통짜 이미지는 아트가 타입을 표현)
            if (skin == null)
            {
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(go.transform, false);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = GetNodeLabel(node);
                label.fontSize = node.Type == MapNodeType.Boss ? 22f : 16f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = isCurrent ? new Color(0.1f, 0.09f, 0.06f) : node.Visited ? Dim : Ink;
                label.raycastTarget = false;
                if (_font != null) label.font = _font;
                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;
            }

            // 주사위 개조 예고 배지 (우상단 보라 점)
            if (node.DiceModReward && !node.Visited)
            {
                var badgeGo = new GameObject("DiceBadge", typeof(RectTransform));
                badgeGo.transform.SetParent(go.transform, false);
                var badgeRect = (RectTransform)badgeGo.transform;
                badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 1f);
                badgeRect.anchoredPosition = new Vector2(-2f, -2f);
                badgeRect.sizeDelta = new Vector2(18f, 18f);
                var badgeImg = badgeGo.AddComponent<Image>();
                badgeImg.sprite = UiRoundedSprite.Get(9);
                badgeImg.type = Image.Type.Sliced;
                badgeImg.color = new Color(0.42f, 0.28f, 0.72f);
                badgeImg.raycastTarget = false;
            }

            // 클릭 (선택 가능일 때만)
            if (isSelectable)
            {
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                var cb = ColorBlock.defaultColorBlock;
                if (skin != null)
                {
                    cb.normalColor = new Color(0.88f, 0.88f, 0.88f);   // 흰 틴트 이미지는 1 초과가 안 먹혀 — 평소 살짝 어둡게, 호버 시 원색
                    cb.highlightedColor = Color.white;
                }
                else
                {
                    cb.normalColor = Color.white;
                    cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
                }
                cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                cb.fadeDuration = 0.08f;
                btn.colors = cb;
                int id = node.Id;
                btn.onClick.AddListener(() => GameFlowManager.Instance?.OnNodeSelected(id));
            }
        }

        /// <summary>노드 둘레 링 (금 = 선택 가능, 잉크 = 현재 위치).</summary>
        private static void CreateNodeRing(Transform parent, Color color)
        {
            var ringGo = new GameObject("Ring", typeof(RectTransform));
            ringGo.transform.SetParent(parent, false);
            var ringRect = (RectTransform)ringGo.transform;
            ringRect.anchorMin = Vector2.zero; ringRect.anchorMax = Vector2.one;
            ringRect.offsetMin = new Vector2(-5, -5); ringRect.offsetMax = new Vector2(5, 5);
            var ringImg = ringGo.AddComponent<Image>();
            ringImg.sprite = UiRoundedSprite.Get(20);
            ringImg.type = Image.Type.Sliced;
            ringImg.color = color;
            ringImg.raycastTarget = false;
        }

        private Sprite GetNodeSprite(MapNodeType type) => type switch
        {
            MapNodeType.Battle => battleSprite,
            MapNodeType.Elite  => eliteSprite,
            MapNodeType.Shop   => shopSprite,
            MapNodeType.Rest   => restSprite,
            MapNodeType.Event  => eventSprite,
            MapNodeType.Boss   => bossSprite,
            _ => null,
        };

        private static string GetNodeLabel(MapNode node) => node.Type switch
        {
            MapNodeType.Battle => "전투",
            MapNodeType.Elite  => "정예",
            MapNodeType.Shop   => "상점",
            MapNodeType.Rest   => "휴식",
            MapNodeType.Event  => "이벤트",
            MapNodeType.Boss   => "보스",
            _ => "?",
        };
    }
}
