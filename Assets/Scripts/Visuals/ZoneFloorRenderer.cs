using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 구역 경계를 검은 테두리로만 그린다. 면을 칠하지 않는 이유 —
    /// 넓은 색면은 타일과 유닛 카드를 덮어 정보를 가린다. 경계만 있으면 판이 깨끗하고,
    /// "누구의 구역인가"는 몬스터 발밑 정체성 색 원(MonsterIdentityManager)이 이미 말해 준다.
    /// 즉 이 클래스는 '어디까지가 한 구역인가'만 담당하고 소유자 표현에는 관여하지 않는다.
    /// </summary>
    public class ZoneFloorRenderer : MonoBehaviour
    {
        public static ZoneFloorRenderer Instance { get; private set; }

        [Header("테두리 모양")]
        [Tooltip("안쪽 반지름 — 중앙부는 비워 둔다")]
        [SerializeField] private float innerRadius = 2.2f;
        [Tooltip("바깥 반지름 — 씬 궤도 반지름 13, 타일 바깥선 13.75")]
        [SerializeField] private float outerRadius = 15.5f;
        [Tooltip("테두리 높이. 타일 윗면(0.1)보다 낮게 둬서 타일이 선을 가리게 한다")]
        [SerializeField] private float floorY = 0.02f;
        [Tooltip("호를 몇 조각으로 그릴지")]
        [SerializeField] private int segmentsPerZone = 24;

        [Header("선")]
        [SerializeField] private Color lineColor = new Color(0.05f, 0.05f, 0.07f, 0.85f);
        [SerializeField] private float lineWidth = 0.14f;
        [Tooltip("유닛 스프라이트 대비 렌더 순서. 음수여야 카드 뒤로 간다 — 정렬 레이어는 유닛 것을 따라간다.")]
        [SerializeField] private int sortingOrderOffset = -100;

        private readonly List<LineRenderer> _outlines = new List<LineRenderer>();
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

            if (_outlines.Count != zones.ZoneCount) BuildOutlines(zones);
            SyncSortingBehindUnits();
        }

        private void BuildOutlines(CombatZoneManager zones)
        {
            foreach (var line in _outlines)
                if (line != null) Destroy(line.gameObject);
            _outlines.Clear();

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            int segments = Mathf.Max(4, segmentsPerZone);

            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                zones.GetZoneAngularRangeDeg(zone, out float startDeg, out float endDeg);

                var go = new GameObject($"_ZoneOutline_{zone}");
                go.transform.SetParent(transform, false);

                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;                       // 부채꼴을 닫힌 테두리로
                lr.startWidth = lineWidth;
                lr.endWidth = lineWidth;
                lr.startColor = lineColor;
                lr.endColor = lineColor;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.material = new Material(shader);
                lr.SetPositions(BuildSectorOutline(innerRadius, outerRadius, startDeg, endDeg, segments, floorY, out int count));
                lr.positionCount = count;

                _outlines.Add(lr);
            }

            _sortingSynced = false;   // 새 선들은 유닛 정렬을 다시 따라가야 한다
        }

        /// <summary>부채꼴 둘레: 바깥 호를 따라간 뒤 안쪽 호를 되짚어 닫는다.</summary>
        private static Vector3[] BuildSectorOutline(float inner, float outer, float startDeg, float endDeg,
            int segments, float y, out int count)
        {
            count = (segments + 1) * 2;
            var points = new Vector3[count];

            for (int i = 0; i <= segments; i++)
            {
                float rad = Mathf.Lerp(startDeg, endDeg, i / (float)segments) * Mathf.Deg2Rad;
                points[i] = new Vector3(Mathf.Cos(rad) * outer, y, Mathf.Sin(rad) * outer);
            }
            for (int i = 0; i <= segments; i++)
            {
                float rad = Mathf.Lerp(endDeg, startDeg, i / (float)segments) * Mathf.Deg2Rad;
                points[segments + 1 + i] = new Vector3(Mathf.Cos(rad) * inner, y, Mathf.Sin(rad) * inner);
            }
            return points;
        }

        /// <summary>
        /// 유닛 스프라이트와 같은 정렬 레이어를 쓰고 그보다 뒤로 보낸다.
        /// 레이어가 다르면 sortingOrder를 아무리 낮춰도 카드 위로 튀어나온다.
        /// </summary>
        private void SyncSortingBehindUnits()
        {
            if (_sortingSynced || _outlines.Count == 0) return;

            var reference = FindAnyUnitSprite();
            if (reference == null) return;   // 유닛이 아직 없다 — 다음 프레임에 다시 시도

            foreach (var line in _outlines)
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
