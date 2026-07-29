#if UNITY_EDITOR
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Core.Run.Save;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// SaveIdCatalog를 프로젝트 전체 스캔으로 채우고, 빈 saveId를 굽고, 중복을 보고한다.
    /// 자동 호출은 SaveIdCatalogPostprocessor가 한다.
    /// </summary>
    public static class SaveIdValidator
    {
        // 재스캔이 SaveAssets를 부르면 다시 OnPostprocessAllAssets가 도므로 재진입을 막는다.
        private static bool _running;

        [MenuItem("도구/Dice Orbit/세이브 ID 전체 점검")]
        public static void RescanFromMenu()
        {
            Rescan();
            Debug.Log("[SaveId] 전체 점검 완료 — 위 콘솔에 에러가 없으면 정상입니다.");
        }

        public static void Rescan()
        {
            if (_running) return;
            _running = true;
            try
            {
                var catalog = FindCatalog();
                if (catalog == null)
                {
                    Debug.LogWarning(
                        "[SaveId] SaveIdCatalog 에셋이 없습니다. " +
                        "Create > DiceOrbit > SaveIdCatalog 로 만들어 Assets/Resources/SaveIdCatalog.asset 에 두세요.");
                    return;
                }

                var artifacts = LoadAll<ArtifactData>();
                var potions   = LoadAll<Potion>();
                var presets   = LoadAll<CharacterPreset>();

                FillMissingIds(artifacts, a => a.SaveId, "유물");
                FillMissingIds(potions,   p => p.SaveId, "포션");
                FillMissingIds(presets,   p => p.SaveId, "프리셋");

                ReportDuplicates(artifacts, a => a.SaveId, "유물");
                ReportDuplicates(potions,   p => p.SaveId, "포션");
                ReportDuplicates(presets,   p => p.SaveId, "프리셋");

                if (catalog.EditorContentsEqual(artifacts, potions, presets)) return;

                catalog.EditorSetContents(artifacts, potions, presets);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
                Debug.Log($"[SaveId] 카탈로그 갱신 — 유물 {artifacts.Count} · 포션 {potions.Count} · 프리셋 {presets.Count}");
            }
            finally
            {
                _running = false;
            }
        }

        private static SaveIdCatalog FindCatalog()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:SaveIdCatalog"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var found = AssetDatabase.LoadAssetAtPath<SaveIdCatalog>(path);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>t:이름 검색은 파생 타입도 잡는다 (t:Potion → HealPotion 포함).</summary>
        private static List<T> LoadAll<T>() where T : Object
        {
            var result = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) result.Add(asset);
            }
            return result;
        }

        /// <summary>
        /// OnValidate가 놓친 에셋(임포트 전이었던 것 등)의 saveId를 굽는다.
        /// SerializedObject를 쓰는 이유는 saveId가 private 필드이기 때문이다.
        /// </summary>
        private static void FillMissingIds<T>(List<T> assets, System.Func<T, string> idOf, string label)
            where T : Object
        {
            foreach (var asset in assets)
            {
                if (!string.IsNullOrEmpty(idOf(asset))) continue;

                var so = new SerializedObject(asset);
                var prop = so.FindProperty("saveId");
                if (prop == null)
                {
                    Debug.LogError($"[SaveId] {label} '{asset.name}'에 saveId 필드가 없습니다.");
                    continue;
                }
                prop.stringValue = asset.name;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                Debug.Log($"[SaveId] {label} '{asset.name}'의 saveId를 '{asset.name}'으로 채웠습니다.");
            }
        }

        private static void ReportDuplicates<T>(List<T> assets, System.Func<T, string> idOf, string label)
            where T : Object
        {
            var seen = new Dictionary<string, T>(assets.Count);
            foreach (var asset in assets)
            {
                string id = idOf(asset);
                if (string.IsNullOrEmpty(id)) continue;
                if (seen.TryGetValue(id, out var other))
                {
                    Debug.LogError(
                        $"[SaveId] {label} saveId 중복 '{id}' — '{other.name}' 와 '{asset.name}'. " +
                        "에셋을 복제(Ctrl+D)하면 saveId까지 복사됩니다. 한쪽을 고쳐 주세요.", asset);
                    continue;
                }
                seen[id] = asset;
            }
        }
    }
}
#endif
