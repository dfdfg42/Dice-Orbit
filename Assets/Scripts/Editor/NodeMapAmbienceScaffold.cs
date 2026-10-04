using System;
using System.IO;
using DiceOrbit.UI;
using DiceOrbit.UI.Ambience;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 노드맵 배경 장식 애니메이션(2026-10-04)의 씬 계층을 만든다 — 배경 Image 아래 Ambience에,
    /// 배치표(ambience_layout.json — Tools/make_nodemap_ambience.py가 만든다)의 조각마다 Image + 움직임 컴포넌트.
    /// 조각은 배경 그림의 같은 자리에 앵커로 붙으므로 화면 크기가 달라져도 배경과 어긋나지 않는다.
    ///
    /// 에디터 소유 패턴: 한 번 만들면 씬이 수치를 소유한다. 이미 Ambience가 있으면 덮어쓰지 않는다
    /// (다시 만들려면 Background/Ambience를 지우고 다시 실행).
    /// 메뉴 [DiceOrbit → Rebuild Node Map Ambience].
    /// </summary>
    public static class NodeMapAmbienceScaffold
    {
        private const string LayoutPath = "Assets/Sprites/배경/NodeMapAmbience/ambience_layout.json";
        private const string RootName = "Ambience";

        [Serializable]
        private class Layout
        {
            public float sourceWidth;
            public float sourceHeight;
            public Piece[] pieces;
        }

        [Serializable]
        private class Piece
        {
            public string name;
            public string kind;      // patch | glow | sparkle
            public string sprite;
            // 원본 그림 픽셀, y는 위에서 아래로
            public float x, y, width, height, pivotX, pivotY;
            public float[] color;
            public WaveData rotation, scaleX, scaleY, alpha;
            public float intervalMin, intervalMax, duration, spin;
        }

        [Serializable]
        private class WaveData
        {
            public float amplitude, period, jitter;
        }

        [MenuItem("DiceOrbit/Rebuild Node Map Ambience")]
        public static void RebuildFromMenu() => Rebuild();

        public static void Rebuild()
        {
            var ui = UnityEngine.Object.FindFirstObjectByType<NodeMapUI>(FindObjectsInactive.Include);
            if (ui == null) throw new InvalidOperationException("[NodeMapAmbienceScaffold] 열린 씬에 NodeMapUI가 없다.");

            var background = new SerializedObject(ui).FindProperty("backgroundImage").objectReferenceValue as Image;
            if (background == null) throw new InvalidOperationException("[NodeMapAmbienceScaffold] NodeMapUI.backgroundImage가 비어 있다 — 배경을 먼저 배선할 것.");

            if (background.transform.Find(RootName) != null)
            {
                Debug.LogWarning($"[NodeMapAmbienceScaffold] {background.name}/{RootName}가 이미 있다 — 다시 만들려면 지우고 다시 실행할 것.");
                return;
            }

            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            if (layout?.pieces == null || layout.pieces.Length == 0)
                throw new InvalidOperationException($"[NodeMapAmbienceScaffold] 배치표가 비어 있다: {LayoutPath}");

            // 자식 캔버스 — 장식이 매 프레임 움직여도 노드맵 본체(스크롤·노드)는 다시 그리지 않는다
            var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas));
            Undo.RegisterCreatedObjectUndo(root, "Rebuild Node Map Ambience");
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(background.transform, false);
            Stretch(rootRect);

            foreach (var piece in layout.pieces)
                Build(piece, layout, rootRect);

            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            Debug.Log($"[NodeMapAmbienceScaffold] {layout.pieces.Length}개 조각을 {background.name}/{RootName} 아래에 만들었다.");
        }

        private static void Build(Piece piece, Layout layout, RectTransform parent)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(piece.sprite);
            if (sprite == null) throw new InvalidOperationException($"[NodeMapAmbienceScaffold] '{piece.name}': 스프라이트가 없다 — {piece.sprite}");

            var go = new GameObject(piece.name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            // 배경 그림에서의 자리를 그대로 앵커로 (그림 y는 아래로, 앵커 y는 위로)
            float w = layout.sourceWidth, h = layout.sourceHeight;
            rect.anchorMin = new Vector2(piece.x / w, 1f - (piece.y + piece.height) / h);
            rect.anchorMax = new Vector2((piece.x + piece.width) / w, 1f - piece.y / h);
            rect.pivot = new Vector2((piece.pivotX - piece.x) / piece.width, 1f - (piece.pivotY - piece.y) / piece.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            if (piece.color != null && piece.color.Length == 4)
                image.color = new Color(piece.color[0], piece.color[1], piece.color[2], piece.color[3]);

            switch (piece.kind)
            {
                case "patch":
                case "glow":
                    go.AddComponent<AmbientMotion>().Configure(
                        ToWave(piece.rotation), ToWave(piece.scaleX), ToWave(piece.scaleY), ToWave(piece.alpha));
                    break;
                case "sparkle":
                    go.AddComponent<AmbientTwinkle>().Configure(
                        new Vector2(piece.intervalMin, piece.intervalMax), piece.duration, piece.spin);
                    break;
                default:
                    throw new InvalidOperationException($"[NodeMapAmbienceScaffold] '{piece.name}': 모르는 kind '{piece.kind}'");
            }
        }

        /// <summary>배치표에 없는 채널은 쉰다 (진폭 0).</summary>
        private static AmbientMotion.Wave ToWave(WaveData data)
            => data == null ? default : new AmbientMotion.Wave(data.amplitude, data.period, data.jitter);

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
