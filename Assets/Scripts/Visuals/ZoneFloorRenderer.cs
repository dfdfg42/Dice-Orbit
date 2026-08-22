using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 구역을 피자 조각(중앙 근처 → 타일 바깥) 실루엣으로 표시하되, 변 전체를 긋지 않고
    /// 꺾이는 지점만 그린다 — 전사 패시브 브래킷과 같은 형태 언어.
    ///
    /// 구역당 세 조각:
    ///  · 가운데 꼭짓점 = V자 (두 곧은 변이 모이는 피자 끝. 바닥은 작은 호, 꺾임은 둥글게)
    ///  · 바깥 모서리 2개 = 둥근 ㄱ자 브래킷 (곧은 변 + 호를 따라 굽는 팔)
    ///
    /// 브래킷 색 = 그 구역 주인 몬스터의 정체성 색 (발밑 원과 같은 체계).
    /// 주인이 없는 중립지대는 옅은 회색 — 사분면 구조는 항상 보인다.
    /// 이웃 구역과 겹치지 않도록 경계에서 각도를 살짝 들여 그린다.
    /// </summary>
    public class ZoneFloorRenderer : MonoBehaviour
    {
        public static ZoneFloorRenderer Instance { get; private set; }

        [Header("피자 조각 범위 (구 색칠 범위와 동일)")]
        [Tooltip("꼭짓점(V) 반지름 — 중앙 근처")]
        [SerializeField] private float innerRadius = 2.2f;
        [Tooltip("바깥 모서리 반지름 — 타일 바깥선(13.75) 너머")]
        [SerializeField] private float outerRadius = 15.5f;
        [Tooltip("브래킷 높이. 타일 윗면(0.1)보다 위여야 가려지지 않는다")]
        [SerializeField] private float floorY = 0.28f;

        [Header("브래킷 모양")]
        [Tooltip("곧은 변을 따라 뻗는 팔 길이")]
        [SerializeField] private float radialArmLength = 1.1f;
        [Tooltip("호를 따라 뻗는 팔 길이 (월드 단위 — 반지름에 맞춰 각도로 환산)")]
        [SerializeField] private float arcArmLength = 1.1f;
        [Tooltip("이웃 구역과 겹치지 않게 경계에서 안쪽으로 들이는 각도 (도)")]
        [SerializeField] private float angularInsetDeg = 2.5f;
        [Tooltip("꺾임을 둥글리는 반경. 0이면 각진 모서리")]
        [SerializeField] private float cornerRoundness = 0.4f;
        [SerializeField] private float lineWidth = 0.18f;

        [Header("색")]
        [Range(0f, 1f)]
        [SerializeField] private float ownerAlpha = 0.9f;
        [Tooltip("주인 없는 중립지대의 브래킷 색")]
        [SerializeField] private Color emptyZoneColor = new Color(0.55f, 0.55f, 0.6f, 0.35f);
        [Tooltip("유닛 스프라이트 대비 렌더 순서. 음수여야 카드 뒤로 간다")]
        [SerializeField] private int sortingOrderOffset = -100;

        // 구역당 조각 3개: 가운데 V 꼭짓점 1 + 바깥 모서리 ㄱ자 2
        private const int PiecesPerZone = 3;
        private readonly List<LineRenderer> _pieces = new List<LineRenderer>();
        // 파괴된 오브젝트는 Unity의 == 비교가 null과 같다고 보고해 변화를 놓치므로 InstanceID로 추적한다.
        private int[] _lastOwnerIds;
        private bool _sortingSynced;

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

            // 궤도가 준비된 뒤에 그린다 — 준비 전 각도로 그리면 반 칸 밀기가 빠져
            // 브래킷이 타일 정중앙 각도에 굳는다.
            if (!zones.IsGeometryReady) return;

            if (_pieces.Count != zones.ZoneCount * PiecesPerZone) BuildPieces(zones);
            SyncSortingBehindUnits();
            RefreshColors(zones);
        }

        // ── 생성 ─────────────────────────────────────────────

        private void BuildPieces(CombatZoneManager zones)
        {
            foreach (var line in _pieces)
                if (line != null) Destroy(line.gameObject);
            _pieces.Clear();

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");

            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                zones.GetZoneAngularRangeDeg(zone, out float startDeg, out float endDeg);
                float a0 = startDeg + angularInsetDeg;   // 이웃과 겹치지 않게 들임
                float a1 = endDeg - angularInsetDeg;

                _pieces.Add(CreateLine(shader, $"_Z{zone}_Tip", BuildTip(a0, a1)));
                _pieces.Add(CreateLine(shader, $"_Z{zone}_OutStart", BuildOuterCorner(a0, +1)));
                _pieces.Add(CreateLine(shader, $"_Z{zone}_OutEnd",   BuildOuterCorner(a1, -1)));
            }

            _lastOwnerIds = new int[zones.ZoneCount];
            for (int i = 0; i < _lastOwnerIds.Length; i++) _lastOwnerIds[i] = int.MinValue;   // 첫 프레임에 반드시 칠하도록
            _sortingSynced = false;   // 새 선들은 유닛 정렬을 다시 따라가야 한다
        }

        private LineRenderer CreateLine(Shader shader, string name, List<Vector3> points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(shader);
            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
            return lr;
        }

        /// <summary>
        /// 가운데 꼭짓점(V): 한쪽 곧은 변을 타고 내려와 → 둥근 꺾임 → 짧은 안쪽 호 →
        /// 둥근 꺾임 → 반대쪽 곧은 변을 타고 올라간다. 피자 조각의 뾰족한 끝.
        /// </summary>
        private List<Vector3> BuildTip(float a0, float a1)
        {
            var points = new List<Vector3>();

            float fillet = Mathf.Clamp(cornerRoundness, 0f, radialArmLength * 0.9f);
            float filletDeg = WorldToDeg(fillet, innerRadius);
            float halfArcDeg = (a1 - a0) * 0.5f;
            filletDeg = Mathf.Min(filletDeg, halfArcDeg * 0.9f);

            // 내려오는 팔 (a0 변)
            points.Add(Polar(innerRadius + radialArmLength, a0));
            AddRoundedCorner(points,
                Polar(innerRadius + fillet, a0),
                Polar(innerRadius, a0),
                Polar(innerRadius, a0 + filletDeg));

            // 바닥 호 (a0+fillet → a1-fillet)
            const int arcSteps = 6;
            for (int i = 1; i < arcSteps; i++)
                points.Add(Polar(innerRadius, Mathf.Lerp(a0 + filletDeg, a1 - filletDeg, i / (float)arcSteps)));

            // 올라가는 팔 (a1 변)
            AddRoundedCorner(points,
                Polar(innerRadius, a1 - filletDeg),
                Polar(innerRadius, a1),
                Polar(innerRadius + fillet, a1));
            points.Add(Polar(innerRadius + radialArmLength, a1));

            return points;
        }

        /// <summary>바깥 모서리(둥근 ㄱ자): 곧은 변을 따라 내려온 팔 + 둥근 꺾임 + 호를 따라 굽는 팔.</summary>
        private List<Vector3> BuildOuterCorner(float cornerDeg, float arcSign)
        {
            var points = new List<Vector3>();

            float fillet = Mathf.Clamp(cornerRoundness, 0f, radialArmLength * 0.9f);
            float filletDeg = WorldToDeg(fillet, outerRadius);
            float armDeg = Mathf.Max(WorldToDeg(arcArmLength, outerRadius), filletDeg * 1.5f);

            points.Add(Polar(outerRadius - radialArmLength, cornerDeg));
            AddRoundedCorner(points,
                Polar(outerRadius - fillet, cornerDeg),
                Polar(outerRadius, cornerDeg),
                Polar(outerRadius, cornerDeg + arcSign * filletDeg));

            // 호팔: 필렛 끝에서 호를 따라 (곡률 유지를 위해 분할)
            const int arcSteps = 3;
            for (int i = 1; i <= arcSteps; i++)
                points.Add(Polar(outerRadius, cornerDeg + arcSign * Mathf.Lerp(filletDeg, armDeg, i / (float)arcSteps)));

            return points;
        }

        /// <summary>from → to 꺾임을 corner를 제어점으로 한 2차 베지어로 둥글려 잇는다 (from은 호출부가 추가).</summary>
        private static void AddRoundedCorner(List<Vector3> points, Vector3 from, Vector3 corner, Vector3 to)
        {
            points.Add(from);
            const int steps = 5;
            for (int i = 1; i < steps; i++)
            {
                float u = i / (float)steps;
                points.Add(Vector3.Lerp(Vector3.Lerp(from, corner, u), Vector3.Lerp(corner, to, u), u));
            }
            points.Add(to);
        }

        private static float WorldToDeg(float worldLength, float radius)
            => worldLength / Mathf.Max(0.01f, radius) * Mathf.Rad2Deg;

        private Vector3 Polar(float radius, float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * radius, floorY, Mathf.Sin(rad) * radius);
        }

        // ── 색 갱신 ──────────────────────────────────────────

        private void RefreshColors(CombatZoneManager zones)
        {
            var identity = MonsterIdentityManager.Instance;

            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                var owner = zones.GetOwner(zone);
                int ownerId = owner != null ? owner.GetInstanceID() : 0;
                if (_lastOwnerIds[zone] == ownerId) continue;   // 변화 없음
                _lastOwnerIds[zone] = ownerId;

                Color c;
                if (owner != null && identity != null)
                {
                    c = identity.GetColor(owner);
                    c.a = ownerAlpha;
                }
                else
                {
                    c = emptyZoneColor;   // 중립지대 — 구조는 보이되 옅게
                }

                for (int p = 0; p < PiecesPerZone; p++)
                {
                    var lr = _pieces[zone * PiecesPerZone + p];
                    if (lr == null) continue;
                    lr.startColor = c;
                    lr.endColor = c;
                }
            }
        }

        // ── 정렬 ─────────────────────────────────────────────

        /// <summary>
        /// 유닛 스프라이트와 같은 정렬 레이어를 쓰고 그보다 뒤로 보낸다.
        /// 레이어가 다르면 sortingOrder를 아무리 낮춰도 카드 위로 튀어나온다.
        /// </summary>
        private void SyncSortingBehindUnits()
        {
            if (_sortingSynced || _pieces.Count == 0) return;

            var reference = FindAnyUnitSprite();
            if (reference == null) return;   // 유닛이 아직 없다 — 다음 프레임에 다시 시도

            foreach (var line in _pieces)
            {
                if (line == null) continue;
                line.sortingLayerID = reference.sortingLayerID;
                line.sortingOrder = reference.sortingOrder + sortingOrderOffset;
            }
            _sortingSynced = true;
        }

        /// <summary>정렬 기준으로 삼을 유닛 스프라이트 하나. 몬스터 우선, 없으면 파티에서.</summary>
        private static SpriteRenderer FindAnyUnitSprite()
        {
            var combat = CombatManager.Instance;
            if (combat != null && combat.ActiveMonsters != null)
                foreach (var monster in combat.ActiveMonsters)
                    if (monster != null)
                    {
                        var sprite = MonsterIdentityManager.FindVisibleSprite(monster.transform);
                        if (sprite != null) return sprite;
                    }

            var party = PartyManager.Instance != null ? PartyManager.Instance.Party : null;
            if (party != null)
                foreach (var character in party)
                    if (character != null)
                    {
                        var sprite = MonsterIdentityManager.FindVisibleSprite(character.transform);
                        if (sprite != null) return sprite;
                    }

            return null;
        }
    }
}
