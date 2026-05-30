using UnityEngine;
using System.Collections.Generic;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 타일 속성 표시 컴포넌트.
    /// 말풍선 대신, 타일 윗면의 '안쪽(궤도 중심 방향) 가장자리'에 아이콘을 평평하게(데칼) 눕혀서 표시한다.
    /// 속성이 여러 개면 그 가장자리를 따라 나란히 배치한다.
    /// (클래스/메서드 이름은 호환을 위해 유지: TileAttributeBubbleManager 가 그대로 호출)
    /// </summary>
    public class TileAttributeBubbleUI : MonoBehaviour
    {
        public readonly struct BubbleIconData
        {
            public readonly Sprite Icon;
            public readonly Color Tint;

            public BubbleIconData(Sprite icon, Color tint)
            {
                Icon = icon;
                Tint = tint;
            }
        }

        [Header("Flat Icon Settings")]
        [SerializeField] private float iconScale = 0.35f;    // 아이콘 크기(월드)
        [SerializeField] private float iconSpacing = 0.5f;   // 여러 개일 때 가장자리 따라 간격(월드)
        [SerializeField] private float edgeInset = 0.7f;     // 안쪽 가장자리까지 비율 (0=타일 중심, 1=가장자리)
        [SerializeField] private float lift = 0.08f;         // 타일 윗면 위로 살짝 띄움 (z-fighting 방지)
        [SerializeField] private int sortingOrder = 50;

        private Transform target;
        private readonly List<SpriteRenderer> iconRenderers = new List<SpriteRenderer>();

        public void Setup(Transform followTarget, Sprite bubbleSprite, IReadOnlyList<BubbleIconData> icons, string label)
        {
            // bubbleSprite/label 은 더 이상 사용하지 않음(말풍선 제거).
            target = followTarget;

            int iconCount = icons != null ? icons.Count : 0;
            EnsureIconRenderers(iconCount);

            for (int i = 0; i < iconRenderers.Count; i++)
            {
                var r = iconRenderers[i];
                bool active = i < iconCount;
                r.gameObject.SetActive(active);
                if (!active) continue;

                r.sprite = icons[i].Icon;
                r.color = icons[i].Tint;
                r.sortingOrder = sortingOrder;
            }

            PlaceIcons(iconCount);
        }

        private void LateUpdate()
        {
            // 타일이 사라지면 같이 정리. (타일은 고정이라 위치 갱신은 Setup 시 한 번이면 충분)
            if (target == null) Destroy(gameObject);
        }

        private void PlaceIcons(int iconCount)
        {
            if (target == null || iconCount <= 0) return;

            ResolveInnerEdge(out Vector3 edgeCenter, out Vector3 toCenter, out Vector3 tangent);

            // 평평하게 눕힘: 법선(+Z)=월드 위, 아이콘 윗방향(+Y)=중심 반대(=타일 안쪽을 바라보게 위아래 뒤집음).
            Quaternion rot = Quaternion.LookRotation(Vector3.up, -toCenter);

            int placed = 0;
            for (int i = 0; i < iconRenderers.Count; i++)
            {
                var r = iconRenderers[i];
                if (r == null || !r.gameObject.activeSelf) continue;

                float centered = placed - ((iconCount - 1) * 0.5f);
                Vector3 pos = edgeCenter + tangent * (centered * iconSpacing) + Vector3.up * lift;
                r.transform.SetPositionAndRotation(pos, rot);
                r.transform.localScale = Vector3.one * iconScale;
                placed++;
            }
        }

        /// <summary>타일 윗면의 안쪽(중심 방향) 가장자리 중점(월드), 중심 방향, 가장자리 접선 벡터를 구한다.</summary>
        private void ResolveInnerEdge(out Vector3 edgeCenter, out Vector3 toCenter, out Vector3 tangent)
        {
            Vector3 tilePos = target.position;

            // 궤도 중심은 월드 원점. 타일에서 중심으로 향하는 수평 방향.
            toCenter = new Vector3(-tilePos.x, 0f, -tilePos.z);
            if (toCenter.sqrMagnitude < 1e-6f) toCenter = target.forward; // 폴백(타일이 LookAt(중심) 이므로 forward=중심)
            toCenter.Normalize();

            tangent = Vector3.Cross(Vector3.up, toCenter).normalized;

            float topY = tilePos.y;
            float innerDist = 0.6f;

            var mf = target.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                Transform t = mf.transform;
                topY = t.TransformPoint(b.center + new Vector3(0f, b.extents.y, 0f)).y; // 윗면 높이(월드)
                innerDist = b.extents.z * Mathf.Abs(t.lossyScale.z) * edgeInset;        // 중심 방향 가장자리까지(월드)
            }

            edgeCenter = new Vector3(tilePos.x, topY, tilePos.z) + toCenter * innerDist;
        }

        private void EnsureIconRenderers(int required)
        {
            while (iconRenderers.Count < required)
            {
                var go = new GameObject($"AttrIcon_{iconRenderers.Count}");
                go.transform.SetParent(transform, false);
                iconRenderers.Add(go.AddComponent<SpriteRenderer>());
            }
        }
    }
}
