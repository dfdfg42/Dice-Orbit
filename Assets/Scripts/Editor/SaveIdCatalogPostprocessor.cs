#if UNITY_EDITOR
using UnityEditor;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 대상 에셋이 임포트·삭제·이동될 때 카탈로그를 자동 재스캔한다.
    /// 수동 등록 단계를 두지 않는 것이 이 설계의 전제다 — 등록을 깜빡해서 복원이 깨진다면
    /// 획득 후보 풀을 조회 출처로 쓰던 때와 달라지는 게 없다.
    /// </summary>
    public class SaveIdCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!HasAssetFile(importedAssets)
                && !HasAssetFile(deletedAssets)
                && !HasAssetFile(movedAssets)) return;

            SaveIdValidator.Rescan();
        }

        private static bool HasAssetFile(string[] paths)
        {
            foreach (var path in paths)
                if (path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
#endif
