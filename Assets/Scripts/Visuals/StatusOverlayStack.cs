using System.Collections.Generic;
using DiceOrbit.Core;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 유닛 스프라이트 위에 상태이상 장식 스프라이트를 겹겹이 얹는다 (스펙 2026-07-29 §④).
    /// 레퍼런스: Sprites/상태이상/적용했을 때의 모습.png (얼음+꿀 동시 적용).
    ///
    /// - 오버레이 아트가 있는 상태(StatusVisualLibrary.Overlay)만 표시, 획득순으로 정렬 +1씩.
    /// - 유닛 스프라이트 bounds에 맞춰 스케일 (유닛마다 크기 달라도 맞음).
    /// - 정렬 순서는 LateUpdate에서 유닛을 따라감 (호버 시 sortingOrder +100 되는 캐릭터 대응).
    /// </summary>
    public class StatusOverlayStack : MonoBehaviour
    {
        private Unit _unit;
        private SpriteRenderer _unitSr;
        private readonly List<SpriteRenderer> _layers = new();

        /// <summary>유닛에 오버레이 스택 부착 (중복 방지). CharacterUI/MonsterUI가 호출.</summary>
        public static void Attach(Unit unit)
        {
            if (unit == null) return;
            if (unit.GetComponentInChildren<StatusOverlayStack>(true) != null) return;

            var go = new GameObject("StatusOverlays");
            go.transform.SetParent(unit.transform, false);
            go.AddComponent<StatusOverlayStack>().Init(unit);
        }

        private void Init(Unit unit)
        {
            _unit = unit;
            _unitSr = unit.GetComponentInChildren<SpriteRenderer>();

            if (_unit.StatusEffects != null)
                _unit.StatusEffects.OnChanged += Rebuild;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (_unit != null && _unit.StatusEffects != null)
                _unit.StatusEffects.OnChanged -= Rebuild;
        }

        private void Rebuild()
        {
            foreach (var layer in _layers)
                if (layer != null) Destroy(layer.gameObject);
            _layers.Clear();

            if (_unit == null || _unit.StatusEffects == null || _unitSr == null) return;

            foreach (var effect in _unit.StatusEffects.GetActiveEffects())
            {
                if (effect == null) continue;
                if (!StatusVisualLibrary.TryGet(effect.Type, out var visual) || visual.Overlay == null) continue;

                var go = new GameObject($"Overlay_{effect.Type}");
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = visual.Overlay;
                sr.color = visual.Tint;
                sr.sortingLayerID = _unitSr.sortingLayerID;

                // 유닛 스프라이트 크기에 맞춤 (월드 기준)
                var target = _unitSr.bounds.size;
                var overlaySize = visual.Overlay.bounds.size;
                float scale = 1f;
                if (overlaySize.x > 0.001f && overlaySize.y > 0.001f)
                    scale = Mathf.Min(target.x / overlaySize.x, target.y / overlaySize.y);

                float parentScale = Mathf.Max(0.001f, transform.lossyScale.x);
                go.transform.localScale = Vector3.one * (scale / parentScale);
                go.transform.position = _unitSr.bounds.center;

                _layers.Add(sr);
            }
        }

        private void LateUpdate()
        {
            if (_unitSr == null || _layers.Count == 0) return;

            // 유닛을 따라다니며 항상 유닛 바로 위 순서로 (호버 정렬 변화 대응)
            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer == null) continue;
                layer.sortingOrder = _unitSr.sortingOrder + 1 + i;
                layer.transform.position = _unitSr.bounds.center;
            }
        }
    }
}
