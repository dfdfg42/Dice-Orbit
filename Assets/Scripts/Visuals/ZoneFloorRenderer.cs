using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 구역 경계를 검은 선으로 표시한다. 부채꼴을 통째로 두르지 않고 **경계선만** 긋는 이유 —
    /// 구역마다 테두리를 두르면 인접 구역이 같은 변을 공유해 선이 두 번 겹치고, 안쪽·바깥 호까지
    /// 더해져 판이 선으로 뒤덮인다. 원을 넷으로 자르는 선 네 개면 구분에 충분하다.
    ///
    /// "누구의 구역인가"는 몬스터 발밑 정체성 색 원(MonsterIdentityManager)이 말해 준다 —
    /// 이 클래스는 '어디까지가 한 구역인가'만 담당하고 소유자 표현에는 관여하지 않는다.
    /// </summary>
    public class ZoneFloorRenderer : MonoBehaviour
    {
        public static ZoneFloorRenderer Instance { get; private set; }

        [Header("테두리 모양")]
        [Tooltip("안쪽 반지름 — 중앙부는 비워 둔다")]
        [SerializeField] private float innerRadius = 2.2f;
        [Tooltip("바깥 반지름 — 씬 궤도 반지름 13, 타일 바깥선 13.75")]
        [SerializeField] private float outerRadius = 15.5f;
        [Tooltip("선 높이. 타일 윗면(0.1)보다 위여야 타일에 가려 끊기지 않는다. 가는 선이라 색면과 달리 정보를 가리지 않는다.")]
        [SerializeField] private float floorY = 0.28f;

        [Header("선")]
        [SerializeField] private Color lineColor = new Color(0.05f, 0.05f, 0.07f, 0.85f);
        [SerializeField] private float lineWidth = 0.14f;
        [Tooltip("유닛 스프라이트 대비 렌더 순서. 음수여야 카드 뒤로 간다 — 정렬 레이어는 유닛 것을 따라간다.")]
        [SerializeField] private int sortingOrderOffset = -100;

        private readonly List<LineRenderer> _dividers = new List<LineRenderer>();
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
            // 경계선이 타일 틈이 아니라 타일 정중앙을 관통한 채 굳는다.
            if (!zones.IsGeometryReady) return;

            if (_dividers.Count != zones.ZoneCount) BuildDividers(zones);
            SyncSortingBehindUnits();
        }

        /// <summary>구역 경계마다 방사형 선 하나. 경계는 구역 수만큼이므로 선도 그만큼이고 겹치지 않는다.</summary>
        private void BuildDividers(CombatZoneManager zones)
        {
            foreach (var line in _dividers)
                if (line != null) Destroy(line.gameObject);
            _dividers.Clear();

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");

            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                // 각 구역의 시작 각도 = 그 구역과 이전 구역의 경계. 구역마다 하나씩이면 경계 전부를 덮는다.
                zones.GetZoneAngularRangeDeg(zone, out float startDeg, out _);
                float rad = startDeg * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

                var go = new GameObject($"_ZoneDivider_{zone}");
                go.transform.SetParent(transform, false);

                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, dir * innerRadius + Vector3.up * floorY);
                lr.SetPosition(1, dir * outerRadius + Vector3.up * floorY);
                lr.startWidth = lineWidth;
                lr.endWidth = lineWidth;
                lr.startColor = lineColor;
                lr.endColor = lineColor;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.material = new Material(shader);

                _dividers.Add(lr);
            }

            _sortingSynced = false;   // 새 선들은 유닛 정렬을 다시 따라가야 한다
        }

        /// <summary>
        /// 유닛 스프라이트와 같은 정렬 레이어를 쓰고 그보다 뒤로 보낸다.
        /// 레이어가 다르면 sortingOrder를 아무리 낮춰도 카드 위로 튀어나온다.
        /// </summary>
        private void SyncSortingBehindUnits()
        {
            if (_sortingSynced || _dividers.Count == 0) return;

            var reference = FindAnyUnitSprite();
            if (reference == null) return;   // 유닛이 아직 없다 — 다음 프레임에 다시 시도

            foreach (var line in _dividers)
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
