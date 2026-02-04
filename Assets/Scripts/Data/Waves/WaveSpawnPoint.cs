using UnityEngine;

namespace DiceOrbit.Data.Waves
{
    public class WaveSpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
