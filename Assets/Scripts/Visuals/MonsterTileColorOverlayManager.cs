using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 타일 윗면에 '몬스터 색상' 오버레이를 깐다.
    /// 한 타일을 여러 몬스터가 공격하면 색을 '가로 밴드'로 N등분해 한 타일에 함께 표시한다.
    /// (1마리 → 타일 전체가 그 색 / 2마리 → 절반·절반 / 3마리 → 1/3씩 …)
    /// 분할 기준은 타일 자체 윗면 축(로컬 엣지)이며, 카메라/월드 방향과 무관하다.
    /// </summary>
    public class MonsterTileColorOverlayManager : MonoBehaviour
    {
        public static MonsterTileColorOverlayManager Instance { get; private set; }

        [SerializeField] private float elevation = 0.05f;       // 타일 윗면 위로 살짝 띄움 (z-fighting 방지)
        [SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.55f;

        private readonly Dictionary<TileData, GameObject> _overlays = new();
        private readonly Dictionary<string, Texture2D> _bandCache = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Clear();
            foreach (var tex in _bandCache.Values)
                if (tex != null) Destroy(tex);
            _bandCache.Clear();
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

        /// <summary>오버레이 GameObject/메쉬/재질만 제거 (밴드 텍스처는 캐시 유지).</summary>
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

            // UV.v 를 타일 자체 윗면 축(로컬 ±Z 엣지)에 고정 매핑 → 밴드를 '타일 윗면 기준'으로 나눈다.
            // corner0,1 = 한쪽 엣지(v=1, colors[0]) / corner2,3 = 반대쪽 엣지(v=0). u는 밴드 텍스처에 무관.
            mesh.uv = new[]
            {
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            };

            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            var mat = new Material(shader) { mainTexture = GetBandTexture(colors) };
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

        private Texture2D GetBandTexture(List<Color> colors)
        {
            string sig = BuildSignature(colors);
            if (_bandCache.TryGetValue(sig, out var cached) && cached != null) return cached;

            var tex = BuildBandTexture(colors);
            _bandCache[sig] = tex;
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

        /// <summary>
        /// 색 목록을 세로로 쌓은 1×N 밴드 텍스처. 위(v=1)부터 colors[0] 순으로 배치.
        /// Point 필터 + 정확히 N행이라 밴드 경계가 또렷하다(블러 없음).
        /// </summary>
        private Texture2D BuildBandTexture(List<Color> colors)
        {
            int n = Mathf.Max(1, colors.Count);

            var tex = new Texture2D(1, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                name = "TileBandTex"
            };

            var px = new Color[n];
            for (int row = 0; row < n; row++)
            {
                // row 0 = 아래(화면 아래), row n-1 = 위(화면 위). 위부터 colors[0]이 오도록 역순 인덱싱.
                int idx = n - 1 - row;
                Color col = colors[idx];
                col.a = overlayAlpha;
                px[row] = col;
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
