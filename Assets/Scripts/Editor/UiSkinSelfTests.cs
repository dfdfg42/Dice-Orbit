using System;
using DiceOrbit.UI.Skin;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// UiSkin 순수 로직 자가 테스트 (2026-09-25 리스킨 2단계). 씬·플레이 모드 불필요.
    /// 메뉴 [DiceOrbit → Run UiSkin Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// </summary>
    public static class UiSkinSelfTests
    {
        private static int _failures;

        [MenuItem("DiceOrbit/Run UiSkin Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;

            TestLoadOrThrowOnMissingAsset();
            TestApplySlicedSetsSpriteTypeAndWhite();
            TestApplyThrowsWhenSpriteMissing();
            TestApplyButtonSetsSpriteSwap();
            TestGetSpriteCoversEveryPart();
            TestValidatorReportsEmptySkin();
            TestValidatorPassesCompleteSkin();
            TestApplySpriteByPart();
            TestScrimPartIsColorOnly();
            TestApplyCircleKeepsTint();
            TestUiSkinImageAppliesPart();

            if (_failures == 0) Debug.Log("[SelfTest] 전체 PASS — UiSkin");
            else Debug.LogError($"[SelfTest] 실패 {_failures}건 — 위 로그 확인");
            return _failures == 0;
        }

        private static void Check(bool condition, string label)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[SelfTest] FAIL: " + label);
        }

        private static Sprite MakeSprite(bool withBorder)
        {
            var tex = new Texture2D(8, 8);
            var border = withBorder ? new Vector4(2, 2, 2, 2) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static ButtonSpriteSet MakeSet() => new ButtonSpriteSet
        {
            Normal = MakeSprite(true), Hover = MakeSprite(true), Pressed = MakeSprite(true), Disabled = MakeSprite(true)
        };

        private static UiSkin MakeCompleteSkin()
        {
            var s = ScriptableObject.CreateInstance<UiSkin>();
            s.Panel = MakeSprite(true); s.Card = MakeSprite(true); s.Chip = MakeSprite(true);
            s.Tooltip = MakeSprite(true); s.Slot = MakeSprite(true); s.Divider = MakeSprite(true);
            s.ButtonPrimary = MakeSet(); s.ButtonSecondary = MakeSet();
            s.Coin = MakeSprite(false); s.PotionSlotEmpty = MakeSprite(false); s.Close = MakeSprite(false);
            s.Circle = MakeSprite(false);
            return s;
        }

        private static void TestLoadOrThrowOnMissingAsset()
        {
            bool threw = false;
            try { UiSkin.LoadOrThrow("UI/DoesNotExist_UiSkin"); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, "없는 스킨 경로는 InvalidOperationException (폴백 없음)");
        }

        private static void TestApplySlicedSetsSpriteTypeAndWhite()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-test", typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = Color.red;
            skin.ApplyPanel(img);
            Check(img.sprite == skin.Panel, "ApplyPanel: 스프라이트 지정");
            Check(img.type == Image.Type.Sliced, "ApplyPanel: Image.Type.Sliced");
            Check(img.color == Color.white, "ApplyPanel: 색 = white (틴트 금지)");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyThrowsWhenSpriteMissing()
        {
            var skin = ScriptableObject.CreateInstance<UiSkin>();
            var go = new GameObject("skin-test", typeof(Image));
            bool threw = false;
            try { skin.ApplyCard(go.GetComponent<Image>()); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, "빈 Card로 ApplyCard는 InvalidOperationException (폴백 없음)");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyButtonSetsSpriteSwap()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-btn", typeof(Image), typeof(Button));
            var btn = go.GetComponent<Button>();
            skin.ApplyButton(btn, ButtonKind.Secondary);
            var set = skin.ButtonSecondary;
            Check(btn.transition == Selectable.Transition.SpriteSwap, "ApplyButton: transition = SpriteSwap");
            Check(btn.spriteState.highlightedSprite == set.Hover, "ApplyButton: highlighted = Hover");
            Check(btn.spriteState.pressedSprite == set.Pressed, "ApplyButton: pressed = Pressed");
            Check(btn.spriteState.disabledSprite == set.Disabled, "ApplyButton: disabled = Disabled");
            Check(btn.targetGraphic is Image tg && tg.sprite == set.Normal && tg.type == Image.Type.Sliced, "ApplyButton: targetGraphic = Normal, Sliced");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestGetSpriteCoversEveryPart()
        {
            var skin = MakeCompleteSkin();
            foreach (SkinPart part in Enum.GetValues(typeof(SkinPart)))
            {
                if (part == SkinPart.Scrim) { Check(skin.GetSprite(part) == null, "GetSprite(Scrim) == null (색만)"); continue; }
                Check(skin.GetSprite(part) != null, $"GetSprite({part}) != null");
            }
            Check(skin.GetSprite(SkinPart.ButtonPrimary) == skin.ButtonPrimary.Normal, "GetSprite(ButtonPrimary) = Primary.Normal");
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestValidatorReportsEmptySkin()
        {
            var skin = ScriptableObject.CreateInstance<UiSkin>();
            var issues = UiSkinValidator.Validate(skin);
            Check(issues.Count == 18, $"빈 스킨 이슈 18건 (6 sliced + 8 button + 3 icon + circle), 실제 {issues.Count}");
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestValidatorPassesCompleteSkin()
        {
            var skin = MakeCompleteSkin();
            var issues = UiSkinValidator.Validate(skin);
            Check(issues.Count == 0, "완전한 스킨 이슈 0 — " + string.Join(" / ", issues));
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplySpriteByPart()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-part", typeof(Image));
            var img = go.GetComponent<Image>();
            skin.ApplySprite(img, SkinPart.Chip);
            Check(img.sprite == skin.Chip && img.type == Image.Type.Sliced && img.color == Color.white, "ApplySprite(Chip): Chip·Sliced·white");
            skin.ApplySprite(img, SkinPart.ButtonSecondary);
            Check(img.sprite == skin.ButtonSecondary.Normal, "ApplySprite(ButtonSecondary): Normal 면");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestScrimPartIsColorOnly()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-scrim", typeof(Image));
            var img = go.GetComponent<Image>();
            img.sprite = skin.Panel;
            skin.ApplySprite(img, SkinPart.Scrim);
            Check(img.sprite == null && img.type == Image.Type.Simple && img.color == skin.Scrim, "ApplySprite(Scrim): sprite null·Simple·Scrim 색");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyCircleKeepsTint()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-circle", typeof(Image));
            var img = go.GetComponent<Image>();
            skin.ApplyCircle(img, Color.red);
            Check(img.sprite == skin.Circle && img.type == Image.Type.Simple && img.preserveAspect && img.color == Color.red, "ApplyCircle: Circle·Simple·preserveAspect·틴트 유지");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestUiSkinImageAppliesPart()
        {
            // 실제 Resources/UI/UiSkin.asset을 쓴다 (씬 배치 컴포넌트의 통합 확인)
            var go = new GameObject("skin-image", typeof(Image), typeof(DiceOrbit.UI.UiSkinImage));
            var comp = go.GetComponent<DiceOrbit.UI.UiSkinImage>();
            comp.SetPart(SkinPart.Card);
            var img = go.GetComponent<Image>();
            Check(img.sprite == UiSkin.Current.Card && img.type == Image.Type.Sliced, "UiSkinImage.SetPart(Card): Card 스프라이트 Sliced");
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
