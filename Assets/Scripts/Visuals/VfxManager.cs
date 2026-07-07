using DiceOrbit.Core;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    public class VfxManager : MonoBehaviour
    {
        public static VfxManager Instance { get; private set; }

        [Header("Fallback VFX")]
        [SerializeField] private GameObject defaultAttackHitVfx;
        [SerializeField] private GameObject defaultHealVfx;
        [SerializeField] private float fallbackLifetime = 2.5f;
        [SerializeField] private Transform vfxRoot;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var go = new GameObject("VfxManager");
            go.AddComponent<VfxManager>();
        }

        public static GameObject SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
        {
            if (prefab == null) return null;
            EnsureInstance();

            Transform parent = Instance != null ? Instance.vfxRoot : null;
            UnityEngine.Object spawned;
            try
            {
                spawned = UnityEngine.Object.Instantiate((UnityEngine.Object)prefab, position, rotation, parent);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[VfxManager] Failed to spawn VFX prefab '{prefab.name}': {ex.Message}");
                return null;
            }

            GameObject vfx = spawned as GameObject;
            if (vfx == null && spawned is Component component)
            {
                vfx = component.gameObject;
            }

            if (vfx == null)
            {
                Debug.LogWarning($"[VfxManager] Spawned object is not a GameObject for prefab '{prefab.name}'.");
                if (spawned != null)
                {
                    Destroy(spawned);
                }
                return null;
            }

            if (lifetime > 0f)
            {
                Destroy(vfx, lifetime);
            }
            return vfx;
        }

        public static void PlayCast(CombatVfxProfile profile, Unit source)
        {
            if (profile == null || source == null || profile.castVfxPrefab == null) return;
            SpawnPrefab(profile.castVfxPrefab, source.transform.position + profile.castOffset, Quaternion.identity, profile.defaultLifetime);
        }

        public static void PlayHit(CombatVfxProfile profile, Unit target)
        {
            if (profile == null || target == null || profile.hitVfxPrefab == null) return;
            SpawnPrefab(profile.hitVfxPrefab, target.transform.position + profile.hitOffset, Quaternion.identity, profile.defaultLifetime);
        }

        public static void PlayHeal(CombatVfxProfile profile, Unit target)
        {
            if (profile == null || target == null || profile.healVfxPrefab == null) return;
            SpawnPrefab(profile.healVfxPrefab, target.transform.position + profile.healOffset, Quaternion.identity, profile.defaultLifetime);
        }

        public static void PlayTile(CombatVfxProfile profile, TileData tile)
        {
            if (profile == null || tile == null || profile.tileVfxPrefab == null) return;
            SpawnPrefab(profile.tileVfxPrefab, tile.Position + profile.tileOffset, Quaternion.identity, profile.defaultLifetime);
        }

        // ── 통합 진입점 (파이프라인 ApplyAction이 호출 — 재생 판단의 유일한 지점) ──

        /// <summary>공격 적중 VFX: 프로필에 hit 프리팹이 있으면 그걸, 없으면 전역 기본.</summary>
        public static void PlayAttackHit(CombatVfxProfile profile, Unit target)
        {
            if (profile != null && profile.hitVfxPrefab != null) PlayHit(profile, target);
            else PlayDefaultAttackHit(target);
        }

        /// <summary>힐 VFX: 프로필에 heal 프리팹이 있으면 그걸, 없으면 전역 기본.</summary>
        public static void PlayHealEffect(CombatVfxProfile profile, Unit target)
        {
            if (profile != null && profile.healVfxPrefab != null) PlayHeal(profile, target);
            else PlayDefaultHeal(target);
        }

        public static void PlayDefaultAttackHit(Unit target)
        {
            if (target == null) return;
            EnsureInstance();
            if (Instance == null || Instance.defaultAttackHitVfx == null) return;
            SpawnPrefab(Instance.defaultAttackHitVfx, target.transform.position + Vector3.up, Quaternion.identity, Instance.fallbackLifetime);
        }

        public static void PlayDefaultHeal(Unit target)
        {
            if (target == null) return;
            EnsureInstance();
            if (Instance == null || Instance.defaultHealVfx == null) return;
            SpawnPrefab(Instance.defaultHealVfx, target.transform.position + Vector3.up, Quaternion.identity, Instance.fallbackLifetime);
        }
    }
}
