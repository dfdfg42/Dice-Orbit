using System;
using System.Collections.Generic;
using System.IO;
using DiceOrbit.Data;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 주사위 종류별 면 그림 배선 (2026-10-05). 배선표 die_faces.json(Tools/cut_die_faces.py가 만든다)대로
    /// 각 DieDefinitionSO의 Face·NumberColor를 채운다. 표가 권위다 — 다시 실행하면 표대로 다시 맞춘다.
    /// 주사위 에셋 폴더에 표에 없는 주사위가 있으면 예외 (면 없는 주사위가 조용히 남지 않게).
    /// 메뉴 [DiceOrbit → Assign Die Faces].
    /// </summary>
    public static class DieFaceSetup
    {
        private const string TablePath = "Assets/Sprites/Dice/die_faces.json";
        private const string DiceDir = "Assets/Scripts/Data/Dices";

        [Serializable]
        private class Table
        {
            public Entry[] faces;
        }

        [Serializable]
        private class Entry
        {
            public string asset;
            public string sprite;
            public float[] numberColor;
        }

        [MenuItem("DiceOrbit/Assign Die Faces")]
        public static void AssignFromMenu() => Assign();

        /// <summary>배선한 주사위 수.</summary>
        public static int Assign()
        {
            var table = JsonUtility.FromJson<Table>(File.ReadAllText(TablePath));
            if (table?.faces == null || table.faces.Length == 0)
                throw new InvalidOperationException($"[DieFaceSetup] 배선표가 비어 있다: {TablePath}");

            var entries = new Dictionary<string, Entry>();
            foreach (var entry in table.faces) entries[entry.asset] = entry;

            int assigned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:DieDefinitionSO", new[] { DiceDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var die = AssetDatabase.LoadAssetAtPath<DieDefinitionSO>(path);
                if (!entries.TryGetValue(die.name, out var entry))
                    throw new InvalidOperationException($"[DieFaceSetup] 주사위 '{die.name}'({path})이 배선표에 없다 — Tools/cut_die_faces.py의 ORDER·ALIASES에 더할 것.");

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.sprite);
                if (sprite == null) throw new InvalidOperationException($"[DieFaceSetup] '{die.name}': 스프라이트가 없다 — {entry.sprite}");
                if (entry.numberColor == null || entry.numberColor.Length != 4)
                    throw new InvalidOperationException($"[DieFaceSetup] '{die.name}': numberColor는 값 4개여야 한다.");

                die.Face = sprite;
                die.NumberColor = new Color(entry.numberColor[0], entry.numberColor[1], entry.numberColor[2], entry.numberColor[3]);
                EditorUtility.SetDirty(die);
                assigned++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[DieFaceSetup] 주사위 {assigned}종에 면 그림을 배선했다.");
            return assigned;
        }
    }
}
