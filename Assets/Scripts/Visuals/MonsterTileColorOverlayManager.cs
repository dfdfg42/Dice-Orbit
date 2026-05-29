using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 타일 윗면에 '몬스터 색상' 오버레이를 깐다.
    /// 한 타일을 여러 몬스터가 공격하면 색을 파이(부채꼴)로 균등 분할해 한 타일에 함께 표시한다.
    /// (몬스터 1마리 → 타일 전체가 그 색)
    /// </summary>
    public class MonsterTileColorOverlayManager : MonoBehaviour
    {
        public static MonsterTileColorOverlayManager Instance { get; private set; }

        [SerializeField] private float elevation = 0.05f;       // 타일 윗면 위로 살짝 띄움 (z-fighting 방지)
        [SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.55f;
        [SerializeField] private int pieTextureSize = 128;

        private readonly Dictionary<TileData, GameObject> _overlays = new();
        private readonly Dictionary<string, Texture2D> _pieCache = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Clear();
            foreach (var tex in _pieCache.Values)
                if (tex != null) Destroy(tex);
            _pieCache.Clear();
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            new GameObject("[MonsterTileColorOverlayManager]").AddComponent<MonsterTileColorOverlayManager>();
        }

        /// <summary>현재 (타일 → 색상 목록) 으로 오버레이 전체를 재구성한다.</summary>
        public void Rebuild(Dictionary<TileData, List<Color>> tileColors)
        {
            Clear();
            if (tileColors == null) return;

            foreach (var kv in tileColors)
            {
                var tile = kv.Key;
                var colors = kv.Value;
                if (tile == null || colors == null || colors.Count == 0) continue;
                _overlays[tile] = BuildOverlay(tile, colors);
            }
        }

        /// <summary>오버레이 GameObject/메쉬/재질만 제거 (파이 텍스처는 캐시 유지).</summary>
        public void Clear()
        {
            foreach (var kv in _overlays)
            {
                var go = kv.Value;
                if (go == null) continue;

                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null && mr.material != null) Destroy(mr.material);
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
                Destroy(go);
            }
            _overlays.Clear();
        }

        private GameObject BuildOverlay(TileData tile, List<Color> colors)
        {
            Vector3[] corners = ResolveTopCorners(tile);

            var go = new GameObject("_MonsterTileColor");

            var mf = go.AddComponent<MeshFilter>();
            var mesh = new Mesh { name = "TileColorQuad" };
            mesh.vertices = corners;
            // UV: corner0→(0,1), corner1→(1,1), corner2→(1,0), corner3→(0,0)
            // → 텍스처 중심(0.5,0.5)이 타일 중심에 오도록 매핑 (파이 분할의 기준점).
            mesh.uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            var mat = new Material(shader) { mainTexture = GetPieTexture(colors) };
            if (mat.HasProperty("_Color")) mat.color = Color.white;
            mr.material = mat;

            return go;
        }

        /// <summary>타일 윗면 4코너를 월드 좌표로 반환 (TileSkillPreviewManager.ResolveCorners 와 동일 로직).</summary>
        private Vector3[] ResolveTopCorners(TileData tile)
        {
            var mf = tile.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds local = mf.sharedMesh.bounds;
                Transform t = mf.transform;
                float topY = local.center.y + local.extents.y;
                float ex = local.extents.x;
                float ez = local.extents.z;
                Vector3 c = local.center;

                Vector3[] localCorners =
                {
                    c + new Vector3(-ex, topY - c.y,  ez),
                    c + new Vector3( ex, topY - c.y,  ez),
                    c + new Vector3( ex, topY - c.y, -ez),
                    c + new Vector3(-ex, topY - c.y, -ez),
                };

                var corners = new Vector3[4];
                for (int i = 0; i < 4; i++)
                    corners[i] = t.TransformPoint(localCorners[i]) + Vector3.up * elevation;
                return corners;
            }

            // 메쉬가 없을 때의 폴백
            Vector3 center = tile.Position + Vector3.up * elevation;
            const float h = 0.75f;
            return new[]
            {
                center + new Vector3(-h, 0,  h),
                center + new Vector3( h, 0,  h),
                center + new Vector3( h, 0, -h),
                center + new Vector3(-h, 0, -h),
            };
        }

        private Texture2D GetPieTexture(List<Color> colors)
        {
            string sig = BuildSignature(colors);
            if (_pieCache.TryGetValue(sig, out var cached) && cached != null) return cached;

            var tex = BuildPieTexture(colors);
            _pieCache[sig] = tex;
            return tex;
        }

        private static string BuildSignature(List<Color> colors)
        {
            var sb = new StringBuilder();
            foreach (var c in colors)
            {
                sb.Append(ColorUtility.ToHtmlStringRGB(c));
                sb.Append(';');
            }
            return sb.ToString();
        }

        /// <summary>중심에서의 각도로 N등분한 파이 텍스처. 사각 타일을 각도 부채꼴로 가득 채운다.</summary>
        private Texture2D BuildPieTexture(List<Color> colors)
        {
            int n = Mathf.Max(1, colors.Count);
            int size = Mathf.Max(16, pieTextureSize);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "TilePieTex"
            };

            float center = (size - 1) * 0.5f;
            const float twoPi = Mathf.PI * 2f;
            var px = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float ang = Mathf.Atan2(dy, dx);     // -PI..PI
                    float tt = (ang + Mathf.PI) / twoPi;  // 0..1
                    int sector = Mathf.Clamp(Mathf.FloorToInt(tt * n), 0, n - 1);

                    Color col = colors[sector];
                    col.a = overlayAlpha;
                    px[y * size + x] = col;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
