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
    /// - 대형 텍스처: WebGL 플랫폼 오버라이드 maxSize — 기본 1024, VFX 시트 2048, 캐릭터 애니 프레임·몬스터 512, 의도 아이콘 256 (Windows 빌드 무영향).
    ///   폴더별 값은 ApplyTextureDiet의 규칙 주석 참고. 다운로드 크기는 DXT 여부보다 픽셀 수가 좌우한다(RGBA+브로틀리가 이미 ~20:1).
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

                    // 2026-09-26 Pages 100MB 한도 재조정: 전용 UI 스프라이트 126장 추가 후 data 파일 132MB.
                    // - 10×10 8K VFX 시트: 4096(프레임 409px)은 DXT가 브로틀리에 거의 안 줄어 장당 ~18MB → 2048(프레임 205px, 화면 투사체 ≤150px)
                    // - 전투 캐릭터 애니 프레임(new character, 264장 1200×830): 화면 ~200px → 512
                    // - 몬스터 스프라이트(102장): 화면 ≤300px → 512
                    // - 공격의도/방어 아이콘(51장): 화면 ≤64px → 256
                    // - 그 외(UI 스킨·배경·초상 등) 1024
                    int max = path.Contains("GabrielAguiarProductions") ? 2048
                            : path.Contains("/new character/") ? 512
                            : path.Contains("몬스터 스프라이트") ? 512
                            : path.Contains("공격의도 아이콘") ? 256
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
