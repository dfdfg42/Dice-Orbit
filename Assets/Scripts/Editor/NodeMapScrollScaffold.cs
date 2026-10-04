using System;
using System.IO;
using DiceOrbit.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 노드맵 가운데 지도 두루마리(2026-10-04)의 씬 계층을 배치표대로 맞춘다.
    /// 배치표(scroll_layout.json)와 스프라이트는 Tools/make_nodemap_scroll.py가 만든다.
    ///
    ///   Background            탁자와 소품 (두루마리는 여기서 지웠다)
    ///   MapScroll             두루마리 한 장 — 지도 내용 아래
    ///   ScrollView            종이 안쪽에 맞춘다. 롤이 있으면 롤 밑으로 조금 들어간다
    ///   RollTop · RollBottom  말린 부분만 한 번 더, 지도 내용 위에 — 노드가 롤 밑으로 말려 들어가 보인다
    ///   (그 뒤) TitleChip …
    ///
    /// 자리는 배경 그림에서의 비율을 앵커로 쓴다. 손으로 조정할 수치가 없으므로(전부 그림에서 나온다)
    /// 다시 실행하면 있는 오브젝트의 자리·스프라이트를 배치표대로 다시 맞춘다.
    /// 메뉴 [DiceOrbit → Rebuild Node Map Scroll].
    /// </summary>
    public static class NodeMapScrollScaffold
    {
        private const string LayoutPath = "Assets/Sprites/배경/NodeMapScroll/scroll_layout.json";
        private const string SheetName = "MapScroll";
        private const string RollTopName = "RollTop";
        private const string RollBottomName = "RollBottom";

        /// <summary>뷰포트가 롤 밑으로 들어가는 깊이 (롤 높이 대비) — 잘리는 선이 롤에 가려진다.</summary>
        private const float UnderRoll = 0.45f;

        [Serializable]
        private class Layout
        {
            public float sourceWidth;
            public float sourceHeight;
            public bool hasRolls;
            public Box paper;
            public Piece[] pieces;
        }

        [Serializable]
        private class Box
        {
            // 배경 원본 픽셀, y는 위에서 아래로
            public float x, y, width, height;
        }

        [Serializable]
        private class Piece : Box
        {
            public string name;
            public string sprite;
        }

        [MenuItem("DiceOrbit/Rebuild Node Map Scroll")]
        public static void RebuildFromMenu() => Rebuild();

        public static void Rebuild()
        {
            var ui = UnityEngine.Object.FindFirstObjectByType<NodeMapUI>(FindObjectsInactive.Include);
            if (ui == null) throw new InvalidOperationException("[NodeMapScrollScaffold] 열린 씬에 NodeMapUI가 없다.");

            var so = new SerializedObject(ui);
            var background = so.FindProperty("backgroundImage").objectReferenceValue as Image;
            var scrollRect = so.FindProperty("scrollRect").objectReferenceValue as ScrollRect;
            if (background == null || scrollRect == null)
                throw new InvalidOperationException("[NodeMapScrollScaffold] NodeMapUI의 backgroundImage·scrollRect를 먼저 배선할 것.");

            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            if (layout?.pieces == null || layout.pieces.Length == 0)
                throw new InvalidOperationException($"[NodeMapScrollScaffold] 배치표가 비어 있다: {LayoutPath}");

            var root = (RectTransform)background.transform.parent;
            var scrollView = (RectTransform)scrollRect.transform;

            RectTransform sheet = null, rollTop = null, rollBottom = null;
            Piece rollTopPiece = null, rollBottomPiece = null;
            foreach (var piece in layout.pieces)
            {
                var rect = FindOrCreate(root, piece.name);
                Place(rect, piece, layout);
                SetSprite(rect, piece);

                switch (piece.name)
                {
                    case SheetName: sheet = rect; break;
                    case RollTopName: rollTop = rect; rollTopPiece = piece; break;
                    case RollBottomName: rollBottom = rect; rollBottomPiece = piece; break;
                    default: throw new InvalidOperationException($"[NodeMapScrollScaffold] 모르는 조각 '{piece.name}'");
                }
            }
            if (sheet == null) throw new InvalidOperationException($"[NodeMapScrollScaffold] 배치표에 {SheetName}이 없다.");

            // 롤 없는 그림으로 바꿨으면 예전 롤 조각을 치운다
            if (rollTop == null) Remove(root, RollTopName);
            if (rollBottom == null) Remove(root, RollBottomName);

            // 그리는 순서: 배경 → 두루마리 → 지도 내용 → 롤
            int index = background.transform.GetSiblingIndex();
            sheet.SetSiblingIndex(++index);
            scrollView.SetSiblingIndex(++index);
            if (rollTop != null) rollTop.SetSiblingIndex(++index);
            if (rollBottom != null) rollBottom.SetSiblingIndex(++index);

            // 지도 내용이 보이는 창 = 종이 안쪽 (+ 롤 밑으로 조금)
            var view = new Box { x = layout.paper.x, y = layout.paper.y, width = layout.paper.width, height = layout.paper.height };
            if (rollTopPiece != null)
            {
                float under = rollTopPiece.height * UnderRoll;
                view.y -= under;
                view.height += under;
            }
            if (rollBottomPiece != null) view.height += rollBottomPiece.height * UnderRoll;
            Undo.RecordObject(scrollView, "Rebuild Node Map Scroll");
            Place(scrollView, view, layout);

            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            Debug.Log($"[NodeMapScrollScaffold] 두루마리 {layout.pieces.Length}조각을 맞췄다 (롤 {(layout.hasRolls ? "있음" : "없음")}).");
        }

        private static RectTransform FindOrCreate(RectTransform root, string name)
        {
            var existing = root.Find(name);
            if (existing != null)
            {
                Undo.RecordObject(existing, "Rebuild Node Map Scroll");
                return (RectTransform)existing;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Rebuild Node Map Scroll");
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            return rect;
        }

        private static void Remove(RectTransform root, string name)
        {
            var existing = root.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
        }

        /// <summary>배경 그림에서의 자리를 그대로 앵커로 (그림 y는 아래로, 앵커 y는 위로).</summary>
        private static void Place(RectTransform rect, Box box, Layout layout)
        {
            float w = layout.sourceWidth, h = layout.sourceHeight;
            rect.anchorMin = new Vector2(box.x / w, 1f - (box.y + box.height) / h);
            rect.anchorMax = new Vector2((box.x + box.width) / w, 1f - box.y / h);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetSprite(RectTransform rect, Piece piece)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(piece.sprite);
            if (sprite == null) throw new InvalidOperationException($"[NodeMapScrollScaffold] '{piece.name}': 스프라이트가 없다 — {piece.sprite}");

            var image = rect.GetComponent<Image>();
            Undo.RecordObject(image, "Rebuild Node Map Scroll");
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
        }
    }
}
