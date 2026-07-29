using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// saveId → 에셋 인덱스. 복원이 에셋을 되찾는 유일한 출처다.
    ///
    /// 획득 후보 풀(artifactPool/potionPool)과 역할이 다르다 — 풀은 상점 진열·드랍 후보를 담는
    /// 게임 디자인용 목록이라 의도적으로 일부만 담지만, 카탈로그는 프로젝트에 존재하는 모든
    /// 대상 에셋을 담아야 한다. 풀 밖 경로로도 유물·포션이 게임에 들어오기 때문이다.
    ///
    /// 내용은 에디터에서 자동 유지된다 (SaveIdCatalogPostprocessor).
    /// </summary>
    [CreateAssetMenu(fileName = "SaveIdCatalog", menuName = "DiceOrbit/SaveIdCatalog")]
    public class SaveIdCatalog : ScriptableObject
    {
        /// <summary>Resources.Load 경로. 에셋은 Assets/Resources/SaveIdCatalog.asset 이어야 한다.</summary>
        public const string ResourcePath = "SaveIdCatalog";

        [SerializeField] private List<ArtifactData> artifacts = new List<ArtifactData>();
        [SerializeField] private List<Potion> potions = new List<Potion>();
        [SerializeField] private List<CharacterPreset> presets = new List<CharacterPreset>();

        private Dictionary<string, ArtifactData> _artifactIndex;
        private Dictionary<string, Potion> _potionIndex;
        private Dictionary<string, CharacterPreset> _presetIndex;

        private static SaveIdCatalog _cached;

        /// <summary>Resources에서 카탈로그를 얻는다. 없으면 null.</summary>
        public static SaveIdCatalog Get()
        {
            if (_cached == null) _cached = Resources.Load<SaveIdCatalog>(ResourcePath);
            return _cached;
        }

        public ArtifactData FindArtifact(string saveId)
        {
            if (_artifactIndex == null) _artifactIndex = BuildIndex(artifacts, a => a.SaveId, "유물");
            return Lookup(_artifactIndex, saveId);
        }

        public Potion FindPotion(string saveId)
        {
            if (_potionIndex == null) _potionIndex = BuildIndex(potions, p => p.SaveId, "포션");
            return Lookup(_potionIndex, saveId);
        }

        public CharacterPreset FindPreset(string saveId)
        {
            if (_presetIndex == null) _presetIndex = BuildIndex(presets, p => p.SaveId, "프리셋");
            return Lookup(_presetIndex, saveId);
        }

        private static T Lookup<T>(Dictionary<string, T> index, string saveId) where T : Object
        {
            if (string.IsNullOrEmpty(saveId)) return null;
            return index.TryGetValue(saveId, out var found) ? found : null;
        }

        /// <summary>인덱스를 만드는 이 지점이 곧 런타임 중복·빈 saveId 검출 지점이다.</summary>
        private static Dictionary<string, T> BuildIndex<T>(
            List<T> source, System.Func<T, string> idOf, string label) where T : Object
        {
            var map = new Dictionary<string, T>(source.Count);
            foreach (var item in source)
            {
                if (item == null) continue;

                string id = idOf(item);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogError($"[SaveIdCatalog] {label} '{item.name}'의 saveId가 비어 있습니다 — 복원 불가.");
                    continue;
                }
                if (map.ContainsKey(id))
                {
                    Debug.LogError($"[SaveIdCatalog] {label} saveId 중복 '{id}' — '{map[id].name}' vs '{item.name}'.");
                    continue;
                }
                map[id] = item;
            }
            return map;
        }

        // 아래 둘은 internal이 아니라 public이어야 한다. Assets/Scripts/Editor/ 아래 코드는
        // Assembly-CSharp-Editor라는 별도 어셈블리로 컴파일되므로 internal이 보이지 않는다.
        // 에디터 전용이므로 #if UNITY_EDITOR로 감싸 플레이어 빌드에서는 빠진다 —
        // 에디터에서는 Assembly-CSharp도 UNITY_EDITOR가 켜진 채 컴파일되므로 에디터 쪽에서 보인다.
#if UNITY_EDITOR

        /// <summary>에디터 재스캔 전용. 런타임에서 호출하지 않는다.</summary>
        public void EditorSetContents(
            List<ArtifactData> newArtifacts, List<Potion> newPotions, List<CharacterPreset> newPresets)
        {
            artifacts = newArtifacts;
            potions = newPotions;
            presets = newPresets;
            _artifactIndex = null;
            _potionIndex = null;
            _presetIndex = null;
        }

        /// <summary>재스캔 결과가 기존과 같은지 — 같으면 에셋을 더럽히지 않는다(임포트 루프 방지).</summary>
        public bool EditorContentsEqual(
            List<ArtifactData> newArtifacts, List<Potion> newPotions, List<CharacterPreset> newPresets)
        {
            return SameSequence(artifacts, newArtifacts)
                && SameSequence(potions, newPotions)
                && SameSequence(presets, newPresets);
        }

        private static bool SameSequence<T>(List<T> a, List<T> b) where T : Object
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }
#endif
    }
}
