#if UNITY_EDITOR
using System.Collections.Generic;
using DiceOrbit.UI.Skin;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// UiSkin 에셋 점검 — 빈 필드, Sliced 부품의 9-slice 경계 0, 텍스처 타입이 Sprite가 아닌 경우를 에러로 나열한다.
    /// 메뉴 「도구/Dice Orbit/UI 스킨 점검」. 순수 부분(Validate)은 자가 테스트 대상.
    /// </summary>
    public static class UiSkinValidator
    {
        [MenuItem("도구/Dice Orbit/UI 스킨 점검")]
        public static void ValidateFromMenu()
        {
            UiSkin skin;
            try { skin = UiSkin.LoadOrThrow(UiSkin.ResourcePath); }
            catch (System.InvalidOperationException e) { Debug.LogError(e.Message); return; }

            var issues = Validate(skin);
            foreach (var issue in issues) Debug.LogError("[UiSkin] " + issue);
            if (issues.Count == 0) Debug.Log("[UiSkin] 점검 완료 — 이슈 0");
            else Debug.LogError($"[UiSkin] 이슈 {issues.Count}건 — 위 로그 확인");
        }

        /// <summary>이슈 목록을 돌려준다. 비어 있으면 정상.</summary>
        public static List<string> Validate(UiSkin skin)
        {
            var issues = new List<string>();
            CheckSliced(issues, skin.Panel,   nameof(skin.Panel));
            CheckSliced(issues, skin.Card,    nameof(skin.Card));
            CheckSliced(issues, skin.Chip,    nameof(skin.Chip));
            CheckSliced(issues, skin.Tooltip, nameof(skin.Tooltip));
            CheckSliced(issues, skin.Slot,    nameof(skin.Slot));
            CheckSliced(issues, skin.Divider, nameof(skin.Divider));
            CheckButtonSet(issues, skin.ButtonPrimary,   nameof(skin.ButtonPrimary));
            CheckButtonSet(issues, skin.ButtonSecondary, nameof(skin.ButtonSecondary));
            CheckPresent(issues, skin.Coin,            nameof(skin.Coin));
            CheckPresent(issues, skin.PotionSlotEmpty, nameof(skin.PotionSlotEmpty));
            CheckPresent(issues, skin.Close,           nameof(skin.Close));
            CheckPresent(issues, skin.Circle,          nameof(skin.Circle));
            CheckSliced(issues, skin.IntentBubble,     nameof(skin.IntentBubble));
            CheckPresent(issues, skin.ZonePlate,       nameof(skin.ZonePlate));
            return issues;
        }

        private static void CheckButtonSet(List<string> issues, ButtonSpriteSet set, string field)
        {
            if (set == null) { issues.Add($"{field}: 세트가 null"); return; }
            CheckSliced(issues, set.Normal,   field + ".Normal");
            CheckSliced(issues, set.Hover,    field + ".Hover");
            CheckSliced(issues, set.Pressed,  field + ".Pressed");
            CheckSliced(issues, set.Disabled, field + ".Disabled");
        }

        private static void CheckSliced(List<string> issues, Sprite sprite, string field)
        {
            if (!CheckPresent(issues, sprite, field)) return;
            if (sprite.border == Vector4.zero)
                issues.Add($"{field}: 9-slice 경계가 0 — 임포터 Sprite Border를 설정하세요 ({AssetDatabase.GetAssetPath(sprite)})");
        }

        private static bool CheckPresent(List<string> issues, Sprite sprite, string field)
        {
            if (sprite == null) { issues.Add($"{field}: 비어 있음"); return false; }
            var path = AssetDatabase.GetAssetPath(sprite);
            if (!string.IsNullOrEmpty(path)
                && AssetImporter.GetAtPath(path) is TextureImporter importer
                && importer.textureType != TextureImporterType.Sprite)
                issues.Add($"{field}: 텍스처 타입이 Sprite가 아님 ({path})");
            return true;
        }
    }
}
#endif
