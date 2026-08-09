using TMPro;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// GitHub Pages 100MB 한도용 WebGL 용량 다이어트 + 빌드.
    /// 근거: 빌드 리포트 상 텍스처가 비압축 1.6GB(99.2%) — Pretendard 동적 아틀라스 176MB,
    /// GabrielAguiar 8K 스프라이트시트 85MB, 2048+ 스프라이트/배경 다수.
    ///
    /// - Pretendard(Dynamic): Clear Dynamic Data On Build → 빌드 시 아틀라스 비우고 런타임 재생성.
    /// - 대형 텍스처: WebGL 플랫폼 오버라이드 maxSize 1024 (VFX 스프라이트시트는 2048) — Windows 빌드 무영향.
    /// 사용: -executeMethod DiceOrbit.EditorTools.WebGLSizeDiet.ApplyAndBuild
    /// </summary>
    public static class WebGLSizeDiet
    {
        private static readonly string[] TextureRoots =
        {
            "Assets/Sprites",
            "Assets/GabrielAguiarProductions",
            "Assets/Resources",
            "Assets/JMO Assets",
            "Assets/TextMesh Pro",
        };

        [MenuItem("Tools/Build/WebGL Size Diet + Build (GitHub Pages)")]
        public static void ApplyAndBuild()
        {
            ApplyFontDiet();
            ApplyTextureDiet();
            AssetDatabase.SaveAssets();
            WebGLBuilder.BuildForPages();
        }

        private static void ApplyFontDiet()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Pretendard-Regular SDF.asset");
            if (font == null) { Debug.LogWarning("[SizeDiet] Pretendard SDF not found"); return; }
            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                Debug.LogWarning("[SizeDiet] Pretendard SDF is not Dynamic — skip");
                return;
            }
            // 이 TMP 버전엔 공개 프로퍼티가 없어 직렬화 필드로 설정 (asset에 m_ClearDynamicDataOnBuild 존재 확인됨)
            var so = new SerializedObject(font);
            var prop = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (prop == null) { Debug.LogWarning("[SizeDiet] m_ClearDynamicDataOnBuild not found — skip"); return; }
            prop.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(font);
            Debug.Log("[SizeDiet] Pretendard SDF: m_ClearDynamicDataOnBuild = true (빌드 시 동적 아틀라스 비움)");
        }

        private static void ApplyTextureDiet()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", TextureRoots);
            int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) continue;

                    // VFX 플립북(격자 시트)은 프레임 해상도 확보 — 10×10 8K 시트는 4096(프레임 409px),
                    // 나머지 GabrielAguiar 2048, 그 외 1024
                    int max = path.Contains("10X10_8K") ? 4096
                            : path.Contains("GabrielAguiarProductions") ? 2048
                            : 1024;

                    var s = imp.GetPlatformTextureSettings("WebGL");
                    if (s.overridden && s.maxTextureSize == max) continue;   // 이미 목표값

                    s.overridden = true;
                    s.maxTextureSize = max;
                    s.format = TextureImporterFormat.Automatic;
                    s.textureCompression = TextureImporterCompression.Compressed;
                    imp.SetPlatformTextureSettings(s);
                    EditorUtility.SetDirty(imp);
                    imp.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Debug.Log($"[SizeDiet] WebGL texture overrides applied: {changed} textures");
        }
    }
}
