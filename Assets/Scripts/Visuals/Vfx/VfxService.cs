using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// VFX 스폰 단일 창구 (구 VfxManager + TileVfxManager 통합).
    /// 큐 태그 → VfxLibrary 해소 → 스폰. Burst=수명 뒤 Destroy, Looping=대상 부착 후 StopLoop까지 유지.
    /// </summary>
    public class VfxService : MonoBehaviour
    {
        private const string LibraryResourcePath = "Skill/VFX/VfxLibrary";

        public static VfxService Instance { get; private set; }

        [SerializeField] private VfxLibrary library;
        [SerializeField] private Transform vfxRoot;

        // 지속(Looping) 인스턴스 추적: (유닛 인스턴스ID, 태그) → 스폰된 오브젝트
        private readonly Dictionary<(int, string), GameObject> loops = new Dictionary<(int, string), GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (library == null) library = Resources.Load<VfxLibrary>(LibraryResourcePath);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var existing = FindAnyObjectByType<VfxService>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return; }
            new GameObject("VfxService").AddComponent<VfxService>();
        }

        // ── Burst ────────────────────────────────────────────
        public static void Play(string tag, Vector3 at)
        {
            EnsureInstance();
            Instance?.SpawnBurst(tag, at);
        }

        public static void PlayOn(string tag, Unit unit)
        {
            if (unit == null) return;
            Play(tag, unit.transform.position);
        }

        public static void PlayOn(string tag, DiceOrbit.Data.TileData tile)
        {
            if (tile == null) return;
            Play(tag, tile.Position);
        }

        // ── Looping ──────────────────────────────────────────
        public static void StartLoop(string tag, Unit unit)
        {
            if (unit == null) return;
            EnsureInstance();
            Instance?.SpawnLoop(tag, unit);
        }

        public static void StopLoop(Unit unit, string tag)
        {
            if (unit == null || Instance == null) return;
            var key = (unit.GetInstanceID(), tag);
            if (Instance.loops.TryGetValue(key, out var go))
            {
                if (go != null) Destroy(go);
                Instance.loops.Remove(key);
            }
        }

        // ── 타일 속성 VFX (구 TileVfxManager) ─────────────────
        public static void PlayTileEvent(DiceOrbit.Data.TileData tile, TileVfxTrigger trigger)
        {
            if (tile == null) return;
            EnsureInstance();
            Instance?.PlayTileEventImpl(tile, trigger);
        }

        // ── 구현 ─────────────────────────────────────────────
        private void SpawnBurst(string tag, Vector3 at)
        {
            var cue = library != null ? library.ResolveCue(tag) : null;
            if (cue == null || cue.prefab == null) return;
            var go = Instantiate(cue.prefab, at + cue.offset, Quaternion.identity, vfxRoot);
            if (cue.lifetime > 0f) Destroy(go, cue.lifetime);
            if (cue.shake != null && cue.shake.amplitude > 0f)
                ImpactFeedback.Shake(cue.shake.amplitude, cue.shake.duration);
            if (cue.hitStop > 0f)
                ImpactFeedback.HitStop(cue.hitStop);
        }

        private void SpawnLoop(string tag, Unit unit)
        {
            var cue = library != null ? library.ResolveCue(tag) : null;
            if (cue == null || cue.prefab == null) return;
            var key = (unit.GetInstanceID(), tag);
            if (loops.TryGetValue(key, out var existing) && existing != null) return;   // 중복 방지
            var go = Instantiate(cue.prefab, unit.transform.position + cue.offset, Quaternion.identity, unit.transform);
            loops[key] = go;   // Looping은 lifetime 무시 — StopLoop까지 유지
        }

        private void PlayTileEventImpl(DiceOrbit.Data.TileData tile, TileVfxTrigger trigger)
        {
            if (library == null) return;
            foreach (var attribute in tile.GetAttributes())
            {
                if (attribute == null) continue;
                if (!library.TryGetTile(attribute.Type, trigger, out var entry)) continue;
                if (entry == null || entry.prefab == null) continue;
                var go = Instantiate(entry.prefab, tile.Position + entry.offset, Quaternion.identity, vfxRoot);
                if (entry.lifetime > 0f) Destroy(go, entry.lifetime);
                return;   // 기존 동작 보존: 첫 매칭 하나만
            }
        }
    }
}
