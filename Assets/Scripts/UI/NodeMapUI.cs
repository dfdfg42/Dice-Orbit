using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 노드맵 화면 (GameState.Map). 가로 진행형: 왼쪽 출발 → 오른쪽 보스.
    /// RunManager의 MapGraph를 그리고, 선택 가능 노드 클릭 → GameFlowManager.OnNodeSelected.
    ///
    /// "보드게임의 밤" 팔레트 (보상 화면 계승). 에디터 소유 + 런타임 폴백.
    /// v1: 맵 전체가 한 화면에 들어온다 (스크롤 없음).
    /// </summary>
    public class NodeMapUI : MonoBehaviour
    {
        public static NodeMapUI Instance { get; private set; }

        [Header("슬롯 (씬에서 배치 — [기본 레이아웃 생성]으로 자동 배선)")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private RectTransform mapRoot;      // 노드/간선이 그려지는 영역
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("배치")]
        [SerializeField] private float nodeSize = 64f;
        [SerializeField] private float laneSpacing = 130f;
        [SerializeField] private Vector2 horizontalMargin = new Vector2(140f, 140f);   // 좌/우 여백

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
            {
                var go = new GameObject("NodeMapUI");
                Instance = go.AddComponent<NodeMapUI>();
            }
        }

        // ── 공개 API ──────────────────────────────────────────

        public void Show()
        {
            if (rootCanvas == null) BuildDefaultLayout();
            rootCanvas.SetActive(true);
            BattleInfoPanelUI.SetVisible(false);
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

            // 노드 위치 계산 (레이아웃 계산 전 rect가 0이면 기준 해상도 폴백)
            var positions = new Dictionary<int, Vector2>();
            float areaWidth = mapRoot.rect.width > 100f ? mapRoot.rect.width : 1920f;
            float left = -areaWidth * 0.5f + horizontalMargin.x;
            float right = areaWidth * 0.5f - horizontalMargin.y;

            for (int f = 0; f < map.FloorCount; f++)
            {
                var floorNodes = map.GetFloor(f);
                float x = map.FloorCount > 1 ? Mathf.Lerp(left, right, (float)f / (map.FloorCount - 1)) : 0f;
                for (int i = 0; i < floorNodes.Count; i++)
                {
                    float y = (i - (floorNodes.Count - 1) * 0.5f) * laneSpacing;
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
        }

        // ── 렌더링 ────────────────────────────────────────────

        private void CreateEdge(Vector2 from, Vector2 to, Color color, float thickness)
        {
            var go = new GameObject("Edge", typeof(RectTransform));
            go.transform.SetParent(mapRoot, false);
            var rect = (RectTransform)go.transform;

            Vector2 delta = to - from;
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private void CreateNodeButton(MapNode node, Vector2 pos, bool isSelectable, bool isCurrent)
        {
            float size = node.Type == MapNodeType.Boss ? nodeSize * 1.4f : nodeSize;

            var go = new GameObject($"Node_{node.Id}", typeof(RectTransform));
            go.transform.SetParent(mapRoot, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(size, size);

            // 선택 가능 노드: 골드 테두리
            if (isSelectable)
            {
                var edgeGo = new GameObject("Edge", typeof(RectTransform));
                edgeGo.transform.SetParent(go.transform, false);
                var edgeRect = (RectTransform)edgeGo.transform;
                edgeRect.anchorMin = Vector2.zero; edgeRect.anchorMax = Vector2.one;
                edgeRect.offsetMin = new Vector2(-5, -5); edgeRect.offsetMax = new Vector2(5, 5);
                var edgeImg = edgeGo.AddComponent<Image>();
                edgeImg.sprite = UiRoundedSprite.Get(20);
                edgeImg.type = Image.Type.Sliced;
                edgeImg.color = Gold;
                edgeImg.raycastTarget = false;
            }

            var img = go.AddComponent<Image>();
            img.sprite = UiRoundedSprite.Get(16);
            img.type = Image.Type.Sliced;
            img.color = isCurrent ? Gold
                      : node.Visited ? new Color(Dim.r, Dim.g, Dim.b, 0.55f)
                      : isSelectable ? CardEdge
                      : Card;

            // 라벨
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

            // 주사위 개조 예고 배지 (우상단 골드 점)
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
                badgeImg.color = new Color(0.42f, 0.28f, 0.72f);   // 모디파이어 보라 계열 = 아이템 신호
                badgeImg.raycastTarget = false;
            }

            // 클릭 (선택 가능일 때만)
            if (isSelectable)
            {
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                var cb = ColorBlock.defaultColorBlock;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
                cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                cb.fadeDuration = 0.08f;
                btn.colors = cb;
                int id = node.Id;
                btn.onClick.AddListener(() => GameFlowManager.Instance?.OnNodeSelected(id));
            }
        }

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

        // ─────────────────────────────────────────────
        // [에디터] 기본 레이아웃 생성 (런타임 폴백 겸용)
        // ─────────────────────────────────────────────
        [ContextMenu("기본 레이아웃 생성")]
        private void BuildDefaultLayout()
        {
            if (transform.Find("_NodeMapCanvas") != null)
            {
                Debug.LogWarning("[NodeMapUI] _NodeMapCanvas가 이미 있습니다. 다시 생성하려면 기존 것을 삭제하세요.");
                return;
            }

            _font = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include)?.font;

            var canvasGo = new GameObject("_NodeMapCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;   // 일반 UI 위, 보상(1500)·툴팁 아래
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            rootCanvas = canvasGo;

            // 펠트 배경
            var felt = new GameObject("Felt", typeof(RectTransform));
            felt.transform.SetParent(canvasGo.transform, false);
            var feltRect = (RectTransform)felt.transform;
            feltRect.anchorMin = Vector2.zero; feltRect.anchorMax = Vector2.one;
            feltRect.offsetMin = Vector2.zero; feltRect.offsetMax = Vector2.zero;
            felt.AddComponent<Image>().color = Felt;

            // 타이틀
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(canvasGo.transform, false);
            var titleRect = (RectTransform)titleGo.transform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -28f);
            titleRect.sizeDelta = new Vector2(800f, 60f);
            titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "노드맵";
            titleText.fontSize = 38f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Ink;
            titleText.raycastTarget = false;
            if (_font != null) titleText.font = _font;

            // 맵 영역
            var rootGo = new GameObject("MapRoot", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            mapRoot = (RectTransform)rootGo.transform;
            mapRoot.anchorMin = Vector2.zero; mapRoot.anchorMax = Vector2.one;
            mapRoot.offsetMin = new Vector2(0f, 30f);
            mapRoot.offsetMax = new Vector2(0f, -90f);

            rootCanvas.SetActive(false);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            Debug.Log("[NodeMapUI] 기본 레이아웃 생성 완료 — 계층을 자유롭게 스타일링한 뒤 씬을 저장하세요.");
        }
    }
}
