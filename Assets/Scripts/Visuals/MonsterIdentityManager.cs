using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 몬스터별 '정체성 색상'을 관리하고, 각 몬스터 발밑 바닥에 색상 원형 마커를 표시한다.
    /// - 색상은 웨이브 시작 시(Setup) 색상환에서 균등 분배되어 Monster 인스턴스에 저장되므로 웨이브 내내 고정된다.
    /// - 마커는 부모 없이 월드 바닥(groundY)에 수평으로 배치되어 몬스터의 회전/스케일/높이에 영향받지 않는다.
    /// </summary>
    public class MonsterIdentityManager : MonoBehaviour
    {
        public static MonsterIdentityManager Instance { get; private set; }

        [Header("Floor Marker")]
        [SerializeField] private float markerRadius = 1.7f;
        [SerializeField] private float groundY = 0.05f;       // 스프라이트를 못 찾을 때의 폴백 높이
        [SerializeField] private float feetYOffset = -1f;       // 발(스프라이트 하단) 기준 높이 미세조정 (+위 / −아래)
        [SerializeField] private int sortingOrderOffset = -1;  // 몬스터 스프라이트 대비 렌더 순서 (음수 = 뒤 → 몬스터가 앞)
        [SerializeField, Range(0f, 1f)] private float markerAlpha = 0.15f;

        [Header("Color")]
        [SerializeField, Range(0f, 1f)] private float saturation = 0.85f;
        [SerializeField, Range(0f, 1f)] private float value = 1f;

        private readonly Dictionary<Monster, GameObject> _markers = new();
        private Mesh _circleMesh;
        private float _circleMeshRadius = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_circleMesh != null) Destroy(_circleMesh);
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            new GameObject("[MonsterIdentityManager]").AddComponent<MonsterIdentityManager>();
        }

        /// <summary>
        /// 웨이브 스폰 직후 호출. 활성 몬스터들에게 균등 색상을 배정(Monster에 저장)하고 바닥 마커를 생성한다.
        /// </summary>
        public void Setup(IReadOnlyList<Monster> monsters)
        {
            ClearAll();
            if (monsters == null) return;

            var list = new List<Monster>();
            foreach (var m in monsters) if (m != null) list.Add(m);

            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                float hue = (n <= 1) ? 0f : (float)i / n;
                Color c = Color.HSVToRGB(hue, saturation, value);
                c.a = 1f;
                AssignColor(list[i], c);
            }
            Debug.Log($"[MonsterIdentity] Assigned identity colors to {n} monster(s).");
        }

        /// <summary>
        /// 몬스터 정체성 색상 조회. 이미 배정돼 있으면 그 값을 그대로 반환(웨이브 내내 고정).
        /// 미배정이면 instanceID 기반 결정적 색을 배정한다(폴백, 호출 순서와 무관하게 안정적).
        /// </summary>
        public Color GetColor(Monster monster)
        {
            if (monster == null) return Color.white;
            if (monster.HasIdentityColor) return monster.IdentityColor;

            float hue = Mathf.Repeat(Mathf.Abs(monster.GetInstanceID()) * 0.61803398f, 1f);
            Color c = Color.HSVToRGB(hue, saturation, value);
            c.a = 1f;
            AssignColor(monster, c);
            return c;
        }

        private void AssignColor(Monster monster, Color c)
        {
            if (monster == null) return;
            monster.IdentityColor = c;
            monster.HasIdentityColor = true;
            CreateMarker(monster, c);
        }

        private void CreateMarker(Monster monster, Color color)
        {
            if (monster == null) return;

            if (_markers.TryGetValue(monster, out var old) && old != null) Destroy(old);

            // 부모 없이 월드에 직접 생성 → 몬스터 transform의 회전/스케일/높이 영향을 받지 않음.
            var go = new GameObject("MonsterFloorMarker");
            go.transform.rotation = Quaternion.identity;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = GetCircleMesh();

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            var mat = new Material(shader) { mainTexture = Texture2D.whiteTexture };
            Color tint = color; tint.a = markerAlpha;
            if (mat.HasProperty("_Color")) mat.color = tint;
            mr.material = mat;

            // 보이는(활성) 스프라이트를 찾아 그보다 뒤 순서로 렌더 → 몬스터가 마커 앞으로 나옴.
            var sprite = FindVisibleSprite(monster.transform);
            if (sprite != null)
            {
                mr.sortingLayerID = sprite.sortingLayerID;
                mr.sortingOrder = sprite.sortingOrder + sortingOrderOffset;
            }
            else
            {
                mr.sortingOrder = sortingOrderOffset;
            }

            // 마커를 몬스터 '발'(스프라이트 하단) 위치에 두고 X/Z 추종, 항상 수평 유지.
            var follow = go.AddComponent<MonsterFloorMarker>();
            follow.Init(monster.transform, sprite, groundY, feetYOffset);

            _markers[monster] = go;
        }

        /// <summary>현재 markerRadius 의 단색 원형 메쉬(삼각 팬)를 반환. 모든 마커가 공유.</summary>
        private Mesh GetCircleMesh()
        {
            if (_circleMesh == null || !Mathf.Approximately(_circleMeshRadius, markerRadius))
            {
                if (_circleMesh != null) Destroy(_circleMesh);
                _circleMesh = BuildCircleXZ(markerRadius, 48);
                _circleMeshRadius = markerRadius;
            }
            return _circleMesh;
        }

        /// <summary>XZ 평면에 꽉 찬 원형(삼각 팬) 메쉬. 텍스처 그라데이션 없이 단색으로 채워진다.</summary>
        private static Mesh BuildCircleXZ(float r, int segments)
        {
            segments = Mathf.Max(8, segments);

            var verts = new Vector3[segments + 1];
            var uv = new Vector2[segments + 1];
            var normals = new Vector3[segments + 1];
            var colors = new Color[segments + 1];

            verts[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            normals[0] = Vector3.up;
            colors[0] = Color.white;

            for (int i = 0; i < segments; i++)
            {
                float ang = (float)i / segments * Mathf.PI * 2f;
                float cx = Mathf.Cos(ang);
                float cz = Mathf.Sin(ang);
                verts[i + 1] = new Vector3(cx * r, 0f, cz * r);
                uv[i + 1] = new Vector2(0.5f + cx * 0.5f, 0.5f + cz * 0.5f);
                normals[i + 1] = Vector3.up;
                colors[i + 1] = Color.white;
            }

            var tris = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % segments + 1;
            }

            var mesh = new Mesh { name = "FloorMarkerCircle" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>몬스터 하위에서 보이는(활성, 스프라이트 할당된) SpriteRenderer를 찾는다.</summary>
        public static SpriteRenderer FindVisibleSprite(Transform root)
        {
            if (root == null) return null;
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr != null && sr.enabled && sr.sprite != null) return sr;
            return null;
        }

        public void ClearAll()
        {
            foreach (var kv in _markers)
                if (kv.Value != null) Destroy(kv.Value);
            _markers.Clear();
        }
    }

    /// <summary>
    /// 바닥 색상 마커: 대상 몬스터의 X/Z를 추종하고 높이는 스프라이트 하단(발) 위치에 맞춘다(없으면 폴백 높이).
    /// 항상 수평을 유지하며, 몬스터가 사라지면 스스로 제거된다.
    /// </summary>
    public class MonsterFloorMarker : MonoBehaviour
    {
        private Transform _monster;
        private SpriteRenderer _sprite;
        private float _fallbackY;
        private float _yOffset;

        public void Init(Transform monster, SpriteRenderer sprite, float fallbackY, float yOffset)
        {
            _monster = monster;
            _sprite = sprite;
            _fallbackY = fallbackY;
            _yOffset = yOffset;
            SnapToMonster();
        }

        private void LateUpdate()
        {
            if (_monster == null)
            {
                Destroy(gameObject);
                return;
            }
            SnapToMonster();
        }

        private void SnapToMonster()
        {
            if (_monster == null) return;

            // 스프라이트가 교체/비활성화됐으면 다시 찾는다(스프라이트 분리 타이밍 대비).
            if (_sprite == null || !_sprite.enabled)
                _sprite = MonsterIdentityManager.FindVisibleSprite(_monster);

            Vector3 p = _monster.position;
            // 발 높이 = 스프라이트 월드 바운드 하단. 스프라이트가 없으면 폴백 높이.
            float y = (_sprite != null) ? _sprite.bounds.min.y + _yOffset : _fallbackY;
            transform.position = new Vector3(p.x, y, p.z);
            transform.rotation = Quaternion.identity;
        }
    }
}
