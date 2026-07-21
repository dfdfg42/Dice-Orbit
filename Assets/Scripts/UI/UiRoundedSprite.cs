using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 절차 생성 라운드 사각형 9-slice 스프라이트 (아트 에셋 없이 둥근 모서리 UI).
    /// 흰색으로 생성하므로 Image.color / ColorBlock으로 틴트해서 사용한다.
    ///
    /// 에디터(비플레이)에서는 Assets/Art/Generated 에 에셋으로 저장해
    /// 씬이 스프라이트 참조를 저장해도 리로드 후 살아있게 한다.
    /// 런타임에서는 캐시된 임시 스프라이트를 쓴다.
    /// </summary>
    public static class UiRoundedSprite
    {
        private static readonly Dictionary<int, Sprite> _runtimeCache = new();

        /// <summary>반지름 radius(px)의 라운드 사각 9-slice 스프라이트. radius = 크기/2 이면 원/필.</summary>
        public static Sprite Get(int radius)
        {
            radius = Mathf.Max(2, radius);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                return GetOrCreateAsset(radius);
#endif
            if (_runtimeCache.TryGetValue(radius, out var cached) && cached != null)
                return cached;

            var sprite = Build(radius);
            _runtimeCache[radius] = sprite;
            return sprite;
        }

        private static Sprite Build(int radius)
        {
            const int stretchPad = 6;                       // 9-slice 중앙 늘어나는 영역
            int size = (radius + stretchPad) * 2;

            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"RoundedRectTex_{radius}"
            };

            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 모서리 원 중심까지의 초과 거리 → 안티앨리어싱된 알파
                    float ax = Mathf.Max(0f, Mathf.Max(radius - x, x - (size - 1 - radius)));
                    float ay = Mathf.Max(0f, Mathf.Max(radius - y, y - (size - 1 - radius)));
                    float d = Mathf.Sqrt(ax * ax + ay * ay);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, false);

            float border = radius + 2f;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = $"RoundedRect_{radius}";
            return sprite;
        }

#if UNITY_EDITOR
        private static Sprite GetOrCreateAsset(int radius)
        {
            const string root = "Assets/Art";
            const string dir = "Assets/Art/Generated";
            string path = $"{dir}/RoundedRect_{radius}.asset";

            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            if (!UnityEditor.AssetDatabase.IsValidFolder(root))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Art");
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder(root, "Generated");

            var sprite = Build(radius);
            UnityEditor.AssetDatabase.CreateAsset(sprite.texture, path);
            UnityEditor.AssetDatabase.AddObjectToAsset(sprite, path);
            UnityEditor.AssetDatabase.SaveAssets();
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
#endif
    }
}
