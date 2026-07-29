using System.Collections.Generic;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 상태이상 → 비주얼(아이콘/오버레이) 매핑 DB (스펙 2026-07-29 §②).
    /// 타일 버블(attributeVisuals)과 같은 인스펙터 리스트 패턴 — 씬에 배치하고 아트를 꽂는다.
    /// 아이콘 없으면 소비처가 글자 칩 폴백, 오버레이 없으면 오버레이 생략.
    /// </summary>
    public class StatusVisualLibrary : MonoBehaviour
    {
        public static StatusVisualLibrary Instance { get; private set; }

        [System.Serializable]
        public class Entry
        {
            public EffectType Type;
            [Tooltip("체력바 아래 아이콘 (비우면 상태색 글자 칩 폴백)")]
            public Sprite Icon;
            [Tooltip("유닛 스프라이트 위에 겹치는 장식 (비우면 오버레이 생략)")]
            public Sprite Overlay;
            public Color Tint = Color.white;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<EffectType, Entry> _map;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildMap();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildMap()
        {
            _map = new Dictionary<EffectType, Entry>();
            foreach (var e in entries)
                if (e != null) _map[e.Type] = e;
        }

        public static bool TryGet(EffectType type, out Entry entry)
        {
            entry = null;
            var lib = Instance;
            if (lib == null)
            {
                lib = FindFirstObjectByType<StatusVisualLibrary>(FindObjectsInactive.Include);
                if (lib == null) return false;
                Instance = lib;
                lib.BuildMap();
            }
            if (lib._map == null) lib.BuildMap();
            return lib._map.TryGetValue(type, out entry) && entry != null;
        }
    }
}
