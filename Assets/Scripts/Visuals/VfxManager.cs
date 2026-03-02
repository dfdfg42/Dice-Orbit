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
            var vfx = Instantiate(prefab, position, rotation, parent);
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
