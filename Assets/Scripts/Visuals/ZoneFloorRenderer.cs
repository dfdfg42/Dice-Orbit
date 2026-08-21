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
        [Tooltip("바깥 반지름 — 궤도 타일을 덮을 만큼")]
        [SerializeField] private float outerRadius = 9f;
        [Tooltip("바닥 높이. 타일 윗면보다 살짝 위여야 색이 타일에 얹힌다")]
        [SerializeField] private float floorY = 0.15f;
        [SerializeField] private int segmentsPerZone = 24;

        [Header("색")]
        [Range(0f, 1f)]
        [SerializeField] private float zoneAlpha = 0.13f;
        [Tooltip("몬스터 스프라이트·타일보다 뒤에 그리기 위한 정렬 순서")]
        [SerializeField] private int sortingOrder = -50;

        private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        private Monster[] _lastOwners;

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

            _lastOwners = new Monster[zones.ZoneCount];
        }

        private void RefreshColors(CombatZoneManager zones)
        {
            var identity = MonsterIdentityManager.Instance;
            if (identity == null) return;

            for (int zone = 0; zone < _renderers.Count; zone++)
            {
                var owner = zones.GetEffectiveOwner(zone);
                if (_lastOwners[zone] == owner) continue;   // 변화 없음 — 머티리얼 건드리지 않는다
                _lastOwners[zone] = owner;

                var mr = _renderers[zone];
                if (mr == null) continue;

                bool hasOwner = owner != null;
                mr.enabled = hasOwner;
                if (!hasOwner) continue;

                Color c = identity.GetColor(owner);
                c.a = zoneAlpha;
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
