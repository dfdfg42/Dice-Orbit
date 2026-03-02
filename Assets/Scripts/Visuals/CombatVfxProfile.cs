using UnityEngine;

namespace DiceOrbit.Visuals
{
    [CreateAssetMenu(fileName = "CombatVfxProfile", menuName = "Dice Orbit/VFX/Combat VFX Profile")]
    public class CombatVfxProfile : ScriptableObject
    {
        [Header("Prefabs")]
        public GameObject castVfxPrefab;
        public GameObject hitVfxPrefab;
        public GameObject healVfxPrefab;
        public GameObject tileVfxPrefab;

        [Header("Offsets")]
        public Vector3 castOffset = new Vector3(0f, 1.0f, 0f);
        public Vector3 hitOffset = new Vector3(0f, 1.0f, 0f);
        public Vector3 healOffset = new Vector3(0f, 1.0f, 0f);
        public Vector3 tileOffset = new Vector3(0f, 0.2f, 0f);

        [Header("Timing")]
        public float defaultLifetime = 2.5f;
    }
}
