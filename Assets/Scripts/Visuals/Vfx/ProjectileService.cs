using System.Collections;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 발사체 연출 — 프리팹을 from→to 포물선으로 날린 뒤 파괴하고 도착 콜백(onArrive)을 호출한다.
    /// 데미지/피격 VFX를 도착 시점에 적용하려면 onArrive에서 CombatPipeline.Process를 부르면 된다.
    /// 씬에 없으면 자동 생성(VfxService 패턴 미러).
    /// </summary>
    public class ProjectileService : MonoBehaviour
    {
        public static ProjectileService Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var existing = FindAnyObjectByType<ProjectileService>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return; }
            new GameObject("ProjectileService").AddComponent<ProjectileService>();
        }

        /// <summary>
        /// prefab을 from→to 포물선으로 duration초 동안 날린 뒤 파괴하고 onArrive를 호출.
        /// prefab이 null이어도 duration 뒤 onArrive는 반드시 호출된다(연출만 생략, 데미지는 그대로 적용).
        /// </summary>
        public static void Launch(GameObject prefab, Vector3 from, Vector3 to, float duration, float arcHeight, System.Action onArrive)
        {
            EnsureInstance();
            if (Instance == null) { onArrive?.Invoke(); return; }
            Instance.StartCoroutine(Instance.FlyRoutine(prefab, from, to, Mathf.Max(0.01f, duration), arcHeight, onArrive));
        }

        private IEnumerator FlyRoutine(GameObject prefab, Vector3 from, Vector3 to, float duration, float arcHeight, System.Action onArrive)
        {
            GameObject go = prefab != null ? Instantiate(prefab, from, Quaternion.identity) : null;

            Vector3 prev = from;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                Vector3 pos = Vector3.LerpUnclamped(from, to, k);
                pos.y += arcHeight * 4f * k * (1f - k);   // 포물선(중앙에서 최고 높이)

                if (go != null)
                {
                    Vector3 dir = pos - prev;
                    if (dir.sqrMagnitude > 1e-6f)
                        go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    go.transform.position = pos;
                }
                prev = pos;
                yield return null;
            }

            if (go != null) Destroy(go);
            onArrive?.Invoke();
        }
    }
}
