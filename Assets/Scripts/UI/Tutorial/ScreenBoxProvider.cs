using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>
    /// 월드 Transform(캐릭터/몬스터)을 오버레이용 RectTransform(스크린 박스)로 매 프레임 미러링.
    /// Director가 매 프레임 Target()을 호출하므로 월드 오브젝트별로 박스를 캐시해 누수를 막는다.
    /// </summary>
    public class ScreenBoxProvider : MonoBehaviour
    {
        public Transform world;
        public Vector2 size = new Vector2(180, 250);   // 캐릭터/몬스터를 넉넉히 덮게 세로로 크게
        private RectTransform _rt;

        private static readonly Dictionary<Transform, ScreenBoxProvider> _cache = new Dictionary<Transform, ScreenBoxProvider>();

        public static RectTransform ForWorld(Transform world)
        {
            if (world == null) return null;
            if (_cache.TryGetValue(world, out var existing) && existing != null) return existing._rt;

            var canvas = TutorialOverlayUI.EnsureInstance();
            var go = new GameObject("ScreenBox", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var self = go.AddComponent<ScreenBoxProvider>();
            self.world = world;
            self._rt = (RectTransform)go.transform;
            self.Sync();
            _cache[world] = self;
            return self._rt;
        }

        /// <summary>모든 캐시 박스 제거 (튜토리얼 종료 정리).</summary>
        public static void ClearAll()
        {
            foreach (var kv in _cache)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _cache.Clear();
        }

        private void LateUpdate() => Sync();

        private void Sync()
        {
            if (world == null || Camera.main == null) return;
            var sp = Camera.main.WorldToScreenPoint(world.position);
            _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0, 0);
            _rt.sizeDelta = size;
            _rt.anchoredPosition = new Vector2(sp.x - size.x * 0.5f, sp.y - size.y * 0.5f);
        }
    }
}
