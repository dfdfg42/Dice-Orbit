using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 구역을 전사 패시브 브래킷(PassiveRangeIndicator)과 같은 형태 언어로 표시한다 —
    /// 부채꼴(타일 띠를 덮는 고리 조각)의 네 모서리에 ㄱ자로 꺾인 브래킷을 찍는다.
    /// 안쪽 모서리 2개 + 바깥 모서리 2개의 꺾임이 모두 보이므로 부채꼴 실루엣이 읽히고,
    /// 변 전체를 긋지 않으니 판이 선으로 뒤덮이지 않는다.
    ///
    /// 브래킷 색 = 그 구역 주인 몬스터의 정체성 색 (발밑 원과 같은 체계).
    /// 주인이 없는 중립지대는 옅은 회색 — 사분면 구조는 항상 보인다.
    /// 이웃 구역과 경계 팔이 겹치지 않도록 각도를 살짝 안쪽으로 들여 그린다.
    /// </summary>
    public class ZoneFloorRenderer : MonoBehaviour
    {
        public static ZoneFloorRenderer Instance { get; private set; }

        [Header("부채꼴 범위 (타일 띠 12.25~13.75를 감쌈)")]
        [Tooltip("안쪽 모서리 반지름")]
        [SerializeField] private float innerRadius = 11.4f;
        [Tooltip("바깥 모서리 반지름")]
        [SerializeField] private float outerRadius = 14.6f;
        [Tooltip("브래킷 높이. 타일 윗면(0.1)보다 위여야 가려지지 않는다")]
        [SerializeField] private float floorY = 0.28f;

        [Header("브래킷 모양 (ㄱ자)")]
        [Tooltip("반지름 방향 팔 길이")]
        [SerializeField] private float radialArmLength = 0.9f;
        [Tooltip("호 방향 팔 길이 (도)")]
        [SerializeField] private float arcArmDeg = 7f;
        [Tooltip("이웃 구역과 겹치지 않게 경계에서 안쪽으로 들이는 각도 (도)")]
        [SerializeField] private float angularInsetDeg = 1.5f;
        [Tooltip("모서리 꺾임을 둥글리는 반경. 0이면 각진 ㄱ자")]
        [SerializeField] private float cornerRoundness = 0.4f;
        [SerializeField] private float lineWidth = 0.18f;

        [Header("색")]
        [Range(0f, 1f)]
        [SerializeField] private float ownerAlpha = 0.9f;
        [Tooltip("주인 없는 중립지대의 브래킷 색")]
        [SerializeField] private Color emptyZoneColor = new Color(0.55f, 0.55f, 0.6f, 0.35f);
        [Tooltip("유닛 스프라이트 대비 렌더 순서. 음수여야 카드 뒤로 간다")]
        [SerializeField] private int sortingOrderOffset = -100;

        // 구역당 브래킷 4개 (안쪽 시작/끝, 바깥 시작/끝 모서리)
        private const int BracketsPerZone = 4;
        private readonly List<LineRenderer> _brackets = new List<LineRenderer>();
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

            if (_brackets.Count != zones.ZoneCount * BracketsPerZone) BuildBrackets(zones);
            SyncSortingBehindUnits();
            RefreshColors(zones);
        }

        // ── 생성 ─────────────────────────────────────────────

        private void BuildBrackets(CombatZoneManager zones)
        {
            foreach (var line in _brackets)
                if (line != null) Destroy(line.gameObject);
            _brackets.Clear();

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");

            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                zones.GetZoneAngularRangeDeg(zone, out float startDeg, out float endDeg);
                float a0 = startDeg + angularInsetDeg;   // 이웃과 팔이 겹치지 않게 들임
                float a1 = endDeg - angularInsetDeg;

                // 모서리 4개: (반지름, 각도, 반지름팔 방향 +밖/-안, 호팔 방향 +끝쪽/-시작쪽)
                _brackets.Add(CreateBracket(shader, $"_Z{zone}_InStart",  innerRadius, a0, +1, +1));
                _brackets.Add(CreateBracket(shader, $"_Z{zone}_InEnd",    innerRadius, a1, +1, -1));
                _brackets.Add(CreateBracket(shader, $"_Z{zone}_OutStart", outerRadius, a0, -1, +1));
                _brackets.Add(CreateBracket(shader, $"_Z{zone}_OutEnd",   outerRadius, a1, -1, -1));
            }

            _lastOwnerIds = new int[zones.ZoneCount];
            for (int i = 0; i < _lastOwnerIds.Length; i++) _lastOwnerIds[i] = int.MinValue;   // 첫 프레임에 반드시 칠하도록
            _sortingSynced = false;   // 새 선들은 유닛 정렬을 다시 따라가야 한다
        }

        /// <summary>
        /// 모서리 1개의 브래킷. 한 팔은 반지름 방향(부채꼴의 곧은 변), 다른 팔은 호를 따라 굽고,
        /// 두 팔이 만나는 꺾임은 베지어 필렛으로 둥글린다 — 각진 ㄱ자가 아니라 둥근 모서리.
        /// 안쪽 모서리는 팔이 벌어져 V 느낌, 바깥 모서리는 ㄱ 느낌이 되며 넷이 부채꼴 실루엣을 만든다.
        /// </summary>
        private LineRenderer CreateBracket(Shader shader, string name, float cornerRadius, float cornerDeg,
            float radialSign, float arcSign)
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

            lr.positionCount = 0;
            var points = BuildRoundedBracket(cornerRadius, cornerDeg, radialSign, arcSign);
            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());

            return lr;
        }

        /// <summary>반지름팔 끝 → (필렛 곡선) → 호팔 끝. 필렛은 모서리를 제어점으로 한 2차 베지어.</summary>
        private List<Vector3> BuildRoundedBracket(float cornerRadius, float cornerDeg, float radialSign, float arcSign)
        {
            var points = new List<Vector3>();

            // 필렛 크기는 팔 길이를 넘지 않게 자른다
            float fillet = Mathf.Clamp(cornerRoundness, 0f, radialArmLength * 0.9f);
            float filletDeg = Mathf.Min(fillet / Mathf.Max(0.01f, cornerRadius) * Mathf.Rad2Deg, arcArmDeg * 0.9f);

            // 반지름팔: 끝에서 필렛 시작점까지
            points.Add(Polar(cornerRadius + radialSign * radialArmLength, cornerDeg));
            var filletFrom = Polar(cornerRadius + radialSign * fillet, cornerDeg);
            var corner     = Polar(cornerRadius, cornerDeg);
            var filletTo   = Polar(cornerRadius, cornerDeg + arcSign * filletDeg);
            points.Add(filletFrom);

            // 둥근 꺾임: 모서리를 제어점으로 한 2차 베지어 (필렛 시작 → 끝)
            const int filletSteps = 5;
            for (int i = 1; i < filletSteps; i++)
            {
                float u = i / (float)filletSteps;
                Vector3 a = Vector3.Lerp(filletFrom, corner, u);
                Vector3 b = Vector3.Lerp(corner, filletTo, u);
                points.Add(Vector3.Lerp(a, b, u));
            }
            points.Add(filletTo);

            // 호팔: 필렛 끝에서 호를 따라 끝까지 (곡률 유지를 위해 분할)
            const int arcSteps = 3;
            for (int i = 1; i <= arcSteps; i++)
            {
                float deg = Mathf.Lerp(filletDeg, arcArmDeg, i / (float)arcSteps);
                points.Add(Polar(cornerRadius, cornerDeg + arcSign * deg));
            }

            return points;
        }

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

                for (int b = 0; b < BracketsPerZone; b++)
                {
                    var lr = _brackets[zone * BracketsPerZone + b];
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
            if (_sortingSynced || _brackets.Count == 0) return;

            var reference = FindAnyUnitSprite();
            if (reference == null) return;   // 유닛이 아직 없다 — 다음 프레임에 다시 시도

            foreach (var line in _brackets)
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
