using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;
using UnityEngine;

namespace DiceOrbit.UI
{
    public class TileAttributeBubbleManager : MonoBehaviour
    {
        public static TileAttributeBubbleManager Instance { get; private set; }
        private const string DefaultVisualDatabaseResourcePath = "UI/TileAttributeVisualDatabase";
        private const string DefaultBubbleSpriteResourcePath = "UI/TileBubbleBG";

        [Header("References")]
        [SerializeField] private TileAttributeVisualDatabase visualDatabase;
        [SerializeField] private Sprite bubbleSprite;

        [Header("Rules")]
        [SerializeField] private bool hideLevelUpAttribute = true;

        private readonly Dictionary<TileData, TileAttributeBubbleUI> activeBubbles = new Dictionary<TileData, TileAttributeBubbleUI>();
        private readonly HashSet<TileAttributeType> missingMappingLogged = new HashSet<TileAttributeType>();
        private readonly Dictionary<TileData, float> _liftOffsets = new Dictionary<TileData, float>();   // 타일 리프트 연출 연동
        private bool _iconsHidden = false;   // 인트로 타일 낙하 동안 아이콘 일괄 숨김

        /// <summary>인트로 등에서 타일 속성 아이콘을 일괄 숨김/표시 (타일 낙하 전엔 숨기고 낙하 후 표시).</summary>
        public void SetIconsHidden(bool hidden)
        {
            _iconsHidden = hidden;
            foreach (var kv in activeBubbles)
                if (kv.Value != null) kv.Value.gameObject.SetActive(!hidden);
        }

        /// <summary>
        /// 타일 버블(속성 아이콘)을 위로 띄우는 오프셋 지정 (타일 리프트 연출과 함께 움직이도록).
        /// 0이면 원위치. RefreshTile이 일어나도 오프셋은 유지된다.
        /// </summary>
        public void SetLiftOffset(TileData tile, float height)
        {
            if (tile == null) return;

            if (height > 0f) _liftOffsets[tile] = height;
            else _liftOffsets.Remove(tile);

            if (activeBubbles.TryGetValue(tile, out var bubble) && bubble != null)
                bubble.SetLiftOffset(height);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Bootstrap()
        {
            // Do not create a runtime instance before scene objects are available.
            // This prevents losing inspector-assigned references (e.g., bubbleSprite).
            if (Instance == null)
            {
                Instance = FindFirstObjectByType<TileAttributeBubbleManager>();
            }
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;

            var existing = FindFirstObjectByType<TileAttributeBubbleManager>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            var go = new GameObject("TileAttributeBubbleManager");
            Instance = go.AddComponent<TileAttributeBubbleManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            TryResolveVisualDatabase();
            TryResolveBubbleSprite();
        }

        private void Start()
        {
            RefreshAllTiles();
        }

        public void RefreshAllTiles()
        {
            var allTiles = GameManager.Instance?.GetOrbitManager().Tiles;
            foreach (var tile in allTiles)
            {
                RefreshTile(tile);
            }
        }

        public void RefreshTile(TileData tile)
        {
            if (tile == null) return;
            if (bubbleSprite == null) TryResolveBubbleSprite();
            if (visualDatabase == null) TryResolveVisualDatabase();

            var attrs = tile.GetAttributes();
            if (attrs == null || attrs.Count == 0)
            {
                RemoveBubble(tile);
                return;
            }

            var displayAttributes = attrs
                .Where(a => a != null && (!hideLevelUpAttribute || a.Type != TileAttributeType.LevelUp))
                .ToList();

            if (displayAttributes.Count == 0)
            {
                RemoveBubble(tile);
                return;
            }

            if (visualDatabase == null)
            {
                Debug.LogError("[TileAttributeBubble] TileAttributeVisualDatabase is not assigned.");
                return;
            }

            var iconData = new List<TileAttributeBubbleUI.BubbleIconData>();
            string primaryLabel = string.Empty;

            for (int i = 0; i < displayAttributes.Count; i++)
            {
                var attr = displayAttributes[i];
                if (!visualDatabase.TryGet(attr.Type, out var visual))
                {
                    if (!missingMappingLogged.Contains(attr.Type))
                    {
                        missingMappingLogged.Add(attr.Type);
                        Debug.LogError($"[TileAttributeBubble] Missing visual mapping for {attr.Type}.");
                    }
                    continue;
                }

                if (visual.icon == null)
                {
                    Debug.LogError($"[TileAttributeBubble] Icon is not assigned for {attr.Type}.");
                    continue;
                }

                iconData.Add(new TileAttributeBubbleUI.BubbleIconData(visual.icon, visual.iconTint, IsCentered(attr.Type)));
                if (string.IsNullOrWhiteSpace(primaryLabel))
                {
                    primaryLabel = visual.shortLabel;
                }
            }

            if (iconData.Count == 0)
            {
                RemoveBubble(tile);
                return;
            }

            var bubble = GetOrCreateBubble(tile);
            bubble.Setup(
                tile.transform,
                bubbleSprite,
                iconData,
                primaryLabel
            );

            // 리프트 중인 타일이면 재배치 후에도 띄운 상태 유지
            if (_liftOffsets.TryGetValue(tile, out float liftOffset))
                bubble.SetLiftOffset(liftOffset);
        }

        /// <summary>공격/방어 타일 아이콘은 가장자리 대신 타일 정중앙에 놓는다.</summary>
        private static bool IsCentered(TileAttributeType type)
            => type == TileAttributeType.Attack || type == TileAttributeType.Defense;

        private TileAttributeBubbleUI GetOrCreateBubble(TileData tile)
        {
            if (activeBubbles.TryGetValue(tile, out var existing) && existing != null)
            {
                return existing;
            }

            var go = new GameObject($"TileBubble_{tile.TileIndex}");
            var bubble = go.AddComponent<TileAttributeBubbleUI>();
            if (_iconsHidden) go.SetActive(false);   // 숨김 중 생성되는 버블도 숨긴 채 시작
            activeBubbles[tile] = bubble;
            return bubble;
        }

        private void RemoveBubble(TileData tile)
        {
            if (!activeBubbles.TryGetValue(tile, out var bubble)) return;
            activeBubbles.Remove(tile);
            if (bubble != null)
            {
                Destroy(bubble.gameObject);
            }
        }

        private void TryResolveVisualDatabase()
        {
            if (visualDatabase != null) return;
            visualDatabase = Resources.Load<TileAttributeVisualDatabase>(DefaultVisualDatabaseResourcePath);
            if (visualDatabase == null)
            {
                Debug.LogWarning($"[TileAttributeBubble] Could not load TileAttributeVisualDatabase from Resources/{DefaultVisualDatabaseResourcePath}.asset");
            }
        }

        private void TryResolveBubbleSprite()
        {
            if (bubbleSprite != null) return;
            bubbleSprite = Resources.Load<Sprite>(DefaultBubbleSpriteResourcePath);
            if (bubbleSprite == null)
            {
                Debug.LogWarning($"[TileAttributeBubble] Could not load bubble sprite from Resources/{DefaultBubbleSpriteResourcePath}.");
            }
        }
    }
}
