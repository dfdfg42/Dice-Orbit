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
            public readonly bool Centered;   // true면 가장자리 대신 타일 정중앙에 배치 (공격/방어 타일)

            public BubbleIconData(Sprite icon, Color tint, bool centered = false)
            {
                Icon = icon;
                Tint = tint;
                Centered = centered;
            }
        }

        [Header("Flat Icon Settings")]
        [SerializeField] private float iconScale = 0.35f;    // 아이콘 크기(월드)
        [SerializeField] private float iconSpacing = 0.5f;   // 여러 개일 때 가장자리 따라 간격(월드)
        [SerializeField] private float edgeInset = 0.7f;     // 안쪽 가장자리까지 비율 (0=타일 중심, 1=가장자리)
        [SerializeField] private float lift = 0.08f;         // 타일 윗면 위로 살짝 띄움 (z-fighting 방지)
        [SerializeField] private int sortingOrder = 50;
        [SerializeField] private int outerEdgeFromIndex = 10; // 이 인덱스 이상 타일은 안쪽 대신 '바깥쪽' 가장자리에 표시 (체력바 가림 회피)

        private Transform target;
        private readonly List<SpriteRenderer> iconRenderers = new List<SpriteRenderer>();
        private bool[] _centered = System.Array.Empty<bool>();   // 아이콘별 중앙배치 여부 (iconRenderers와 인덱스 정렬)
        private int _lastIconCount;
        private float _liftOffset;   // 타일 리프트 연출 연동 (IntentTileLiftEffect)

        /// <summary>타일 리프트 연출 연동: 아이콘들을 위로 띄운다. 0이면 원위치.</summary>
        public void SetLiftOffset(float height)
        {
            _liftOffset = Mathf.Max(0f, height);
            PlaceIcons(_lastIconCount);
        }

        public void Setup(Transform followTarget, Sprite bubbleSprite, IReadOnlyList<BubbleIconData> icons, string label)
        {
            // bubbleSprite/label 은 더 이상 사용하지 않음(말풍선 제거).
            target = followTarget;

            int iconCount = icons != null ? icons.Count : 0;
            _lastIconCount = iconCount;
            EnsureIconRenderers(iconCount);
            if (_centered.Length < iconCount) _centered = new bool[iconCount];

            for (int i = 0; i < iconRenderers.Count; i++)
            {
                var r = iconRenderers[i];
                bool active = i < iconCount;
                r.gameObject.SetActive(active);
                if (!active) continue;

                r.sprite = icons[i].Icon;
                r.color = icons[i].Tint;
                r.sortingOrder = sortingOrder;
                _centered[i] = icons[i].Centered;
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

            ResolveEdge(out Vector3 edgeCenter, out Vector3 edgeDir, out Vector3 tangent, out Vector3 topCenter);

            // 평평하게 눕힘: 법선(+Z)=월드 위, 아이콘 윗방향(+Y)=가장자리에서 타일 안쪽을 바라보게(=-edgeDir).
            Quaternion rot = Quaternion.LookRotation(Vector3.up, -edgeDir);

            // 중앙 배치(공격/방어)와 가장자리 배치를 분리해 각자 나란히 정렬.
            int centeredTotal = 0, edgeTotal = 0;
            for (int i = 0; i < iconCount && i < iconRenderers.Count; i++)
            {
                if (iconRenderers[i] == null || !iconRenderers[i].gameObject.activeSelf) continue;
                if (i < _centered.Length && _centered[i]) centeredTotal++; else edgeTotal++;
            }

            int centeredPlaced = 0, edgePlaced = 0;
            for (int i = 0; i < iconRenderers.Count; i++)
            {
                var r = iconRenderers[i];
                if (r == null || !r.gameObject.activeSelf) continue;

                bool centered = i < _centered.Length && _centered[i];
                Vector3 basePos = centered ? topCenter : edgeCenter;
                int idx   = centered ? centeredPlaced++ : edgePlaced++;
                int total = centered ? centeredTotal   : edgeTotal;

                float offset = idx - ((total - 1) * 0.5f);
                Vector3 pos = basePos + tangent * (offset * iconSpacing) + Vector3.up * (lift + _liftOffset);
                r.transform.SetPositionAndRotation(pos, rot);
                r.transform.localScale = Vector3.one * iconScale;
            }
        }

        /// <summary>
        /// 아이콘을 놓을 타일 윗면 가장자리 중점(월드), 가장자리 방향(중심쪽 또는 바깥쪽), 접선 벡터를 구한다.
        /// 타일 인덱스가 outerEdgeFromIndex 이상이면 안쪽 대신 바깥쪽 가장자리를 사용(뒤쪽 타일 체력바 가림 회피).
        /// </summary>
        private void ResolveEdge(out Vector3 edgeCenter, out Vector3 edgeDir, out Vector3 tangent, out Vector3 topCenter)
        {
            Vector3 tilePos = target.position;

            // 궤도 중심은 월드 원점. 타일에서 중심으로 향하는 수평 방향.
            Vector3 toCenter = new Vector3(-tilePos.x, 0f, -tilePos.z);
            if (toCenter.sqrMagnitude < 1e-6f) toCenter = target.forward; // 폴백(타일이 LookAt(중심) 이므로 forward=중심)
            toCenter.Normalize();

            tangent = Vector3.Cross(Vector3.up, toCenter).normalized;

            // 타일 인덱스에 따라 안쪽/바깥쪽 가장자리 선택
            bool outward = false;
            var tileData = target.GetComponent<DiceOrbit.Data.TileData>();
            if (tileData != null && tileData.TileIndex >= outerEdgeFromIndex) outward = true;
            edgeDir = outward ? -toCenter : toCenter;

            float topY = tilePos.y;
            float dist = 0.6f;

            var mf = target.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                Transform t = mf.transform;
                topY = t.TransformPoint(b.center + new Vector3(0f, b.extents.y, 0f)).y; // 윗면 높이(월드)
                dist = b.extents.z * Mathf.Abs(t.lossyScale.z) * edgeInset;             // 가장자리까지 거리(월드)
            }

            topCenter  = new Vector3(tilePos.x, topY, tilePos.z);            // 타일 윗면 정중앙(공격/방어 아이콘)
            edgeCenter = topCenter + edgeDir * dist;                          // 가장자리(기존 속성 아이콘)
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
