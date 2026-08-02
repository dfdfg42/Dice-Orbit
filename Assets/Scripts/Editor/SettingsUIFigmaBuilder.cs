// 임시 빌더 — Figma "게임 설정" 디자인을 Setting UI.prefab에 재구성한다.
// 시각 확정 후 삭제해도 됨. (Tools/Rebuild Settings UI From Figma)
#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.EditorTools
{
    public static class SettingsUIFigmaBuilder
    {
        const string PrefabPath = "Assets/Art/Setting UI.prefab";

        // ── Figma 팔레트 ──
        static readonly Color Cream  = Hex("FAF3E0");
        static readonly Color Brown  = Hex("5C4D43");
        static readonly Color PanelC = Hex("F3E5CE");
        static readonly Color TagC   = Hex("E8D4BB");
        static readonly Color DropC  = Hex("E1CCB0");
        static readonly Color TextC  = Hex("4B425C");

        static TMP_FontAsset Font;
        static Sprite S_UI, S_Knob, S_Check, S_Bg, S_Mask, S_Arrow, S_Input, S_Dice;

        // 배선용 참조
        static Slider _bgm, _sfx;
        static TextMeshProUGUI _bgmVal, _sfxVal;
        static Toggle _full;
        static TMP_Dropdown _lang, _res;
        static Button _save, _quit, _close;

        [MenuItem("Tools/Rebuild Settings UI From Figma")]
        public static void Build()
        {
            Font    = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Pretendard-Regular SDF.asset");
            S_UI    = GetB("UI/Skin/UISprite.psd");
            S_Knob  = GetB("UI/Skin/Knob.psd");
            S_Check = GetB("UI/Skin/Checkmark.psd");
            S_Bg    = GetB("UI/Skin/Background.psd");
            S_Mask  = GetB("UI/Skin/UIMask.psd");
            S_Arrow = GetB("UI/Skin/DropdownArrow.psd");
            S_Input = GetB("UI/Skin/InputFieldBackground.psd");
            S_Dice  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/dice_handle.png");

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var canvas = root.transform.Find("_SettingsCanvas") as RectTransform;
                if (canvas == null) { Debug.LogError("[Builder] _SettingsCanvas 없음"); return; }

                var panel = canvas.Find("Panel") as RectTransform;
                if (panel == null)
                {
                    var pg = new GameObject("Panel", typeof(RectTransform), typeof(Image));
                    pg.transform.SetParent(canvas, false);
                    panel = pg.GetComponent<RectTransform>();
                }
                // Panel 자식 전부 제거 후 재구성
                for (int i = panel.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(panel.GetChild(i).gameObject);

                // Panel = 테두리(브라운), 750x922, 중앙
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(750, 922);
                panel.anchoredPosition = Vector2.zero;
                var pImg = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
                pImg.sprite = S_UI; pImg.type = Image.Type.Sliced; pImg.color = Brown;

                // 크림 배경(테두리 7 안쪽)
                Panel(panel, "BG", new Vector2(7, 7), new Vector2(736, 908), Cream).SetAsFirstSibling();

                // 제목
                Text(panel, "Title", new Vector2(0, 55), new Vector2(750, 100), "게임 설정", 64, TextC, TextAlignmentOptions.Center, true);

                // 닫기(원형 + X)
                var closeBorder = Panel(panel, "Button_닫기", new Vector2(618, 45), new Vector2(85, 85), PanelC);
                closeBorder.GetComponent<Image>().sprite = S_Knob;   // 원형
                _close = closeBorder.gameObject.AddComponent<Button>();
                XBar(closeBorder, 45f);
                XBar(closeBorder, -45f);

                // ── 그래픽 섹션 ──
                Panel(panel, "Sec_Graphics", new Vector2(44, 218), new Vector2(661, 246), PanelC);
                Panel(panel, "Tag_Graphics", new Vector2(54, 180), new Vector2(185, 76), TagC);
                Text(panel, "L_Graphics", new Vector2(84, 180), new Vector2(160, 76), "그래픽", 40, TextC, TextAlignmentOptions.Left, true);

                Text(panel, "L_Lang", new Vector2(84, 283), new Vector2(130, 56), "언어", 36, TextC, TextAlignmentOptions.Left);
                _lang = Dropdown(panel, "Dropdown_언어", new Vector2(209, 277), new Vector2(170, 70), new List<string> { "한국어", "English" });

                Text(panel, "L_Full", new Vector2(437, 283), new Vector2(180, 56), "전체화면", 36, TextC, TextAlignmentOptions.Left);
                _full = Toggle(panel, "Toggle_전체화면", new Vector2(609, 277), new Vector2(70, 70));

                Text(panel, "L_Res", new Vector2(84, 385), new Vector2(130, 56), "해상도", 36, TextC, TextAlignmentOptions.Left);
                _res = Dropdown(panel, "Dropdown_해상도", new Vector2(209, 379), new Vector2(470, 70), new List<string> { "1920X1080", "1600X900", "1366X768", "1280X720" });

                // ── 오디오 섹션 ──
                Panel(panel, "Sec_Audio", new Vector2(44, 530), new Vector2(661, 246), PanelC);
                Panel(panel, "Tag_Audio", new Vector2(54, 492), new Vector2(185, 76), TagC);
                Text(panel, "L_Audio", new Vector2(84, 492), new Vector2(160, 76), "오디오", 40, TextC, TextAlignmentOptions.Left, true);

                Text(panel, "L_Bgm", new Vector2(84, 597), new Vector2(190, 56), "배경음악", 36, TextC, TextAlignmentOptions.Left);
                _bgm = Slider(panel, "Slider_배경음", new Vector2(255, 612), new Vector2(300, 26));
                _bgmVal = Text(panel, "V_Bgm", new Vector2(566, 597), new Vector2(150, 56), "100%", 36, TextC, TextAlignmentOptions.Left);

                Text(panel, "L_Sfx", new Vector2(84, 699), new Vector2(190, 56), "효과음", 36, TextC, TextAlignmentOptions.Left);
                _sfx = Slider(panel, "Slider_효과음", new Vector2(255, 714), new Vector2(300, 26));
                _sfx.value = 0.5f;
                _sfxVal = Text(panel, "V_Sfx", new Vector2(566, 699), new Vector2(150, 56), "50%", 36, TextC, TextAlignmentOptions.Left);

                // ── 하단 버튼 ──
                _save = TextButton(panel, "Button_저장", new Vector2(140, 809), new Vector2(185, 76), "저장");
                _quit = TextButton(panel, "Button_종료", new Vector2(425, 809), new Vector2(185, 76), "종료");

                // ── SettingsUI 배선 ──
                var so = new SerializedObject(root.GetComponent<DiceOrbit.UI.SettingsUI>());
                Set(so, "rootCanvas", canvas.gameObject);
                Set(so, "bgmSlider", _bgm);
                Set(so, "sfxSlider", _sfx);
                Set(so, "bgmValueText", _bgmVal);
                Set(so, "sfxValueText", _sfxVal);
                Set(so, "languageDropdown", _lang);
                Set(so, "resolutionDropdown", _res);
                Set(so, "fullscreenToggle", _full);
                Set(so, "saveButton", _save);
                Set(so, "quitButton", _quit);
                Set(so, "closeButton", _close);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Builder] Setting UI.prefab 재구성 완료");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.SaveAssets();
            }
        }

        // ── 헬퍼 ───────────────────────────────────────────────

        static RectTransform Place(RectTransform rt, Vector2 figPos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(figPos.x, -figPos.y);
            return rt;
        }

        static RectTransform Panel(Transform parent, string name, Vector2 figPos, Vector2 size, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = S_UI; img.type = Image.Type.Sliced; img.color = col;
            return Place(go.GetComponent<RectTransform>(), figPos, size);
        }

        static TextMeshProUGUI Text(Transform parent, string name, Vector2 figPos, Vector2 size,
            string s, float fs, Color col, TextAlignmentOptions align, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = fs; t.color = col; t.alignment = align;
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            if (Font != null) t.font = Font;
            if (bold) t.fontStyle = FontStyles.Bold;
            Place(go.GetComponent<RectTransform>(), figPos, size);
            return t;
        }

        static void XBar(Transform parent, float angle)
        {
            var go = new GameObject("XBar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = S_UI; img.type = Image.Type.Sliced; img.color = Brown;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(44, 9);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.Euler(0, 0, angle);
        }

        static Button TextButton(Transform parent, string name, Vector2 figPos, Vector2 size, string label)
        {
            var rt = Panel(parent, name, figPos, size, TagC);
            var btn = rt.gameObject.AddComponent<Button>();
            Text(rt, "Text", Vector2.zero, size, label, 40, TextC, TextAlignmentOptions.Center, true)
                .rectTransform.anchoredPosition = Vector2.zero;
            return btn;
        }

        static TMP_Dropdown Dropdown(Transform parent, string name, Vector2 figPos, Vector2 size, List<string> options)
        {
            var border = Panel(parent, name, figPos, size, Brown);   // 테두리
            var res = new TMP_DefaultControls.Resources
            { standard = S_UI, background = S_Bg, inputField = S_Input, knob = S_Knob, checkmark = S_Check, dropdown = S_Arrow, mask = S_Mask };
            var go = TMP_DefaultControls.CreateDropdown(res);
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(border, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(5, 5); rt.offsetMax = new Vector2(-5, -5);   // 테두리 5 안쪽

            var img = go.GetComponent<Image>();
            img.sprite = S_UI; img.type = Image.Type.Sliced; img.color = DropC;

            var dd = go.GetComponent<TMP_Dropdown>();
            if (dd.captionText != null) { dd.captionText.font = Font; dd.captionText.color = TextC; dd.captionText.fontSize = 30; dd.captionText.alignment = TextAlignmentOptions.Left; }
            if (dd.itemText != null) { dd.itemText.font = Font; dd.itemText.color = TextC; dd.itemText.fontSize = 28; }
            dd.ClearOptions(); dd.AddOptions(options);
            return dd;
        }

        static Toggle Toggle(Transform parent, string name, Vector2 figPos, Vector2 size)
        {
            var border = Panel(parent, name, figPos, size, Brown);
            var res = new DefaultControls.Resources
            { standard = S_UI, background = S_Bg, knob = S_Knob, checkmark = S_Check, mask = S_Mask };
            var go = DefaultControls.CreateToggle(res);
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(border, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(5, 5); rt.offsetMax = new Vector2(-5, -5);

            var tog = go.GetComponent<Toggle>();
            var label = go.transform.Find("Label");
            if (label != null) Object.DestroyImmediate(label.gameObject);

            var bgT = go.transform.Find("Background") as RectTransform;
            if (bgT != null)
            {
                bgT.anchorMin = Vector2.zero; bgT.anchorMax = Vector2.one;
                bgT.offsetMin = Vector2.zero; bgT.offsetMax = Vector2.zero;
                var bgImg = bgT.GetComponent<Image>();
                bgImg.sprite = S_UI; bgImg.type = Image.Type.Sliced; bgImg.color = Color.white;
                var chk = bgT.Find("Checkmark") as RectTransform;
                if (chk != null)
                {
                    chk.anchorMin = new Vector2(0.15f, 0.15f); chk.anchorMax = new Vector2(0.85f, 0.85f);
                    chk.offsetMin = Vector2.zero; chk.offsetMax = Vector2.zero;
                    chk.GetComponent<Image>().color = Brown;
                }
            }
            tog.isOn = false;
            return tog;
        }

        static Slider Slider(Transform parent, string name, Vector2 figPos, Vector2 size)
        {
            var res = new DefaultControls.Resources
            { standard = S_UI, background = S_Bg, knob = S_Knob, checkmark = S_Check, mask = S_Mask };
            var go = DefaultControls.CreateSlider(res);
            go.name = name;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Place(rt, figPos, size);

            var sl = go.GetComponent<Slider>();
            sl.minValue = 0; sl.maxValue = 1; sl.value = 1;

            // 얇은 트랙 + 브라운
            var bg = go.transform.Find("Background") as RectTransform;
            if (bg != null)
            {
                bg.anchorMin = new Vector2(0, 0.5f); bg.anchorMax = new Vector2(1, 0.5f);
                bg.sizeDelta = new Vector2(0, 8); bg.anchoredPosition = Vector2.zero;
                var i = bg.GetComponent<Image>(); i.sprite = S_UI; i.type = Image.Type.Sliced; i.color = new Color(Brown.r, Brown.g, Brown.b, 0.28f);
            }
            var fillArea = go.transform.Find("Fill Area") as RectTransform;
            if (fillArea != null)
            {
                fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
                fillArea.sizeDelta = new Vector2(-20, 8); fillArea.anchoredPosition = Vector2.zero;
                var fill = fillArea.Find("Fill") as RectTransform;
                if (fill != null) { var i = fill.GetComponent<Image>(); i.sprite = S_UI; i.type = Image.Type.Sliced; i.color = Brown; }
            }
            var handleArea = go.transform.Find("Handle Slide Area") as RectTransform;
            if (handleArea != null)
            {
                var handle = handleArea.Find("Handle") as RectTransform;
                if (handle != null)
                {
                    var i = handle.GetComponent<Image>();
                    if (S_Dice != null) { handle.sizeDelta = new Vector2(46, 46); i.sprite = S_Dice; i.type = Image.Type.Simple; i.color = Color.white; }
                    else { handle.sizeDelta = new Vector2(34, 34); i.sprite = S_Knob; i.color = Brown; }
                }
            }
            return sl;
        }

        static void Set(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[Builder] 프로퍼티 없음: {prop}"); return; }
            p.objectReferenceValue = value;
        }

        static Sprite GetB(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString("#" + h, out var c);
            return c;
        }
    }
}
#endif
