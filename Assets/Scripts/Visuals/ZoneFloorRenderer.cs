using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 구역 바닥을 소유 몬스터의 정체성 색 부채꼴로 칠한다.
    /// "이 색 위에 서면 이 몬스터를 때린다"를 색 하나로 읽히게 하는 것이 목적이며,
    /// 몬스터 발밑 원형 마커(MonsterIdentityManager)와 같은 색 체계를 공유한다.
    ///
    /// 소유자 변화는 이벤트 배선 없이 매 프레임 비교로 감지한다 — 구역이 4개뿐이라 비용이 없고,
    /// 사망·흡수 같은 모든 경로를 자동으로 따라간다.
    /// </summary>
    public class ZoneFloorRenderer : MonoBehaviour
    {
        public static ZoneFloorRenderer Instance { get; private set; }

        [Header("부채꼴 크기")]
        [Tooltip("안쪽 반지름 — 몬스터가 서는 중앙부는 비워 둔다")]
        [SerializeField] private float innerRadius = 2.2f;
        [Tooltip("바깥 반지름 — 타일 바깥까지 넉넉히 넘겨야 어느 구역인지 한눈에 읽힌다")]
        [SerializeField] private float outerRadius = 12f;
        [Tooltip("바닥 높이. 타일 윗면보다 확실히 위여야 색이 타일에 얹힌다 (아래면 타일에 가려 안쪽 원만 보인다)")]
        [SerializeField] private float floorY = 0.32f;
        [SerializeField] private int segmentsPerZone = 24;

        [Header("색")]
        [Range(0f, 1f)]
        [SerializeField] private float zoneAlpha = 0.13f;
        [Tooltip("몬스터가 없는 중립지대 색. 사분면은 항상 4개가 보이고 빈 구역만 이 색이 된다.")]
        [SerializeField] private Color emptyZoneColor = new Color(0.6f, 0.6f, 0.62f, 1f);
        [Range(0f, 1f)]
        [Tooltip("중립지대 투명도. 소유 구역보다 옅게 둬서 '비어 있음'이 읽히도록.")]
        [SerializeField] private float emptyZoneAlpha = 0.05f;
        [Tooltip("몬스터 스프라이트·타일보다 뒤에 그리기 위한 정렬 순서")]
        [SerializeField] private int sortingOrder = -50;

        private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        // 파괴된 오브젝트는 Unity의 == 비교가 null과 같다고 보고해 변화를 놓치므로 InstanceID로 추적한다.
        private int[] _lastOwnerIds;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static ZoneFloorRenderer EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ZoneFloorRenderer>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("[ZoneFloorRenderer]").AddComponent<ZoneFloorRenderer>();
        }

        private void LateUpdate()
        {
            var zones = CombatZoneManager.Instance;
            if (zones == null) return;

            if (_renderers.Count != zones.ZoneCount) BuildMeshes(zones);
            RefreshColors(zones);
        }

        private void BuildMeshes(CombatZoneManager zones)
        {
            foreach (var r in _renderers)
                if (r != null) Destroy(r.gameObject);
            _renderers.Clear();

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");

            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                zones.GetZoneAngularRangeDeg(zone, out float startDeg, out float endDeg);

                var go = new GameObject($"_ZoneFloor_{zone}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(0f, floorY, 0f);

                go.AddComponent<MeshFilter>().sharedMesh =
                    BuildSectorMesh(innerRadius, outerRadius, startDeg, endDeg, Mathf.Max(4, segmentsPerZone));

                var mr = go.AddComponent<MeshRenderer>();
                mr.material = new Material(shader) { mainTexture = Texture2D.whiteTexture };
                mr.sortingOrder = sortingOrder;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                _renderers.Add(mr);
            }

            _lastOwnerIds = new int[zones.ZoneCount];
            for (int i = 0; i < _lastOwnerIds.Length; i++) _lastOwnerIds[i] = int.MinValue;   // 첫 프레임에 반드시 칠하도록
        }

        private void RefreshColors(CombatZoneManager zones)
        {
            var identity = MonsterIdentityManager.Instance;
            if (identity == null) return;

            for (int zone = 0; zone < _renderers.Count; zone++)
            {
                var owner = zones.GetOwner(zone);
                int ownerId = owner != null ? owner.GetInstanceID() : 0;
                if (_lastOwnerIds[zone] == ownerId) continue;   // 변화 없음 — 머티리얼 건드리지 않는다
                _lastOwnerIds[zone] = ownerId;

                var mr = _renderers[zone];
                if (mr == null) continue;

                // 사분면은 항상 4개가 보인다. 주인이 없으면 중립색으로 남겨 '빈 구역'임을 드러낸다.
                Color c = owner != null ? identity.GetColor(owner) : emptyZoneColor;
                c.a = owner != null ? zoneAlpha : emptyZoneAlpha;
                if (mr.material.HasProperty("_Color")) mr.material.color = c;
            }
        }

        /// <summary>XZ 평면 위 도넛 부채꼴(안쪽 반지름~바깥 반지름, 시작각~끝각) 메쉬.</summary>
        private static Mesh BuildSectorMesh(float inner, float outer, float startDeg, float endDeg, int segments)
        {
            var verts = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[verts.Length];
            var tris = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float rad = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);

                verts[i * 2]     = new Vector3(cos * inner, 0f, sin * inner);
                verts[i * 2 + 1] = new Vector3(cos * outer, 0f, sin * outer);
                uv[i * 2]        = new Vector2(t, 0f);
                uv[i * 2 + 1]    = new Vector2(t, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                int t = i * 6;
                tris[t]     = v;     tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v + 1; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "ZoneFloorSector" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
