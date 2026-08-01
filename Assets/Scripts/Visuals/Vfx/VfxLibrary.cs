using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 모든 VFX 큐의 단일 출처 (구 CombatVfxProfile 5종 + TileVfxDatabase 통합).
    /// 태그 해소는 계층 상향 폴백. 타일 속성 VFX는 (attributeType, trigger) 키.
    /// </summary>
    [CreateAssetMenu(fileName = "VfxLibrary", menuName = "Dice Orbit/VFX/Vfx Library")]
    public class VfxLibrary : ScriptableObject
    {
        [SerializeField] private List<VfxCue> cues = new List<VfxCue>();
        [SerializeField] private List<TileAttributeEntry> attributeEntries = new List<TileAttributeEntry>();

        private Dictionary<string, VfxCue> cueCache;
        private Dictionary<(TileAttributeType, TileVfxTrigger), TileAttributeEntry> tileCache;

        /// <summary>태그를 구체→일반으로 폴백하며 첫 매칭 큐 반환. 없으면 null.</summary>
        public VfxCue ResolveCue(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            BuildCache();
            foreach (var t in VfxTags.Lineage(tag))
                if (cueCache.TryGetValue(t, out var cue)) return cue;
            return null;
        }

        public bool TryGetTile(TileAttributeType type, TileVfxTrigger trigger, out TileAttributeEntry entry)
        {
            BuildCache();
            return tileCache.TryGetValue((type, trigger), out entry);
        }

        private void BuildCache()
        {
            if (cueCache != null && tileCache != null) return;
            cueCache = new Dictionary<string, VfxCue>();
            foreach (var c in cues)
                if (c != null && !string.IsNullOrEmpty(c.tag)) cueCache[c.tag] = c;
            tileCache = new Dictionary<(TileAttributeType, TileVfxTrigger), TileAttributeEntry>();
            foreach (var e in attributeEntries)
                if (e != null) tileCache[(e.attributeType, e.trigger)] = e;
        }

        private void OnValidate() { cueCache = null; tileCache = null; }

        // 에디터 배선용 (RunCommand가 SerializedObject로 접근하므로 런타임 API는 최소)
        public IReadOnlyList<VfxCue> Cues => cues;
        public IReadOnlyList<TileAttributeEntry> AttributeEntries => attributeEntries;
    }
}
