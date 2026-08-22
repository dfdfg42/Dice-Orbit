using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 구역 경계를 전사 패시브 브래킷(PassiveRangeIndicator)과 같은 형태 언어로 표시한다 —
    /// 경계선 전체를 긋지 않고 **가장자리 짧은 눈금(틱)만** 남긴다. 궤도 안쪽 끝과 바깥쪽 끝에
    /// 짧게 찍힌 눈금 한 쌍이면 "여기서 구역이 갈린다"가 읽히고, 판 가운데를 가로지르는
    /// 긴 선이 없어 유닛·타일 정보를 전혀 건드리지 않는다.
    ///
    /// "누구의 구역인가"는 몬스터 발밑 정체성 색 원(MonsterIdentityManager)이 말해 준다 —
    /// 이 클래스는 '어디서 구역이 갈리는가'만 담당하고 소유자 표현에는 관여하지 않는다.
    /// </summary>
    public class ZoneFloorRenderer : MonoBehaviour
    {
        public static ZoneFloorRenderer Instance { get; private set; }

        [Header("눈금 모양 (전사 브래킷과 같은 형태 언어)")]
        [Tooltip("안쪽 눈금이 시작하는 반지름 — 타일 안쪽선(12.25)보다 살짝 안")]
        [SerializeField] private float innerRadius = 11.4f;
        [Tooltip("바깥 눈금이 끝나는 반지름 — 타일 바깥선(13.75)보다 살짝 밖")]
        [SerializeField] private float outerRadius = 14.6f;
        [Tooltip("눈금 하나의 길이 (경계 방향으로)")]
        [SerializeField] private float tickLength = 0.9f;
        [Tooltip("눈금 높이. 타일 윗면(0.1)보다 위여야 가려지지 않는다")]
        [SerializeField] private float floorY = 0.28f;

        [Header("선")]
        [SerializeField] private Color lineColor = new Color(0.05f, 0.05f, 0.07f, 0.85f);
        [SerializeField] private float lineWidth = 0.18f;
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

            if (_dividers.Count != zones.ZoneCount * 2) BuildDividers(zones);
            SyncSortingBehindUnits();
        }

        /// <summary>
        /// 구역 경계마다 눈금 한 쌍 — 궤도 안쪽 끝과 바깥쪽 끝에 짧게. 경계선 전체를 긋지 않는다.
        /// 경계는 구역 수만큼이므로 겹칠 일이 없다.
        /// </summary>
        private void BuildDividers(CombatZoneManager zones)
        {
            foreach (var line in _dividers)
                if (line != null) Destroy(line.gameObject);
            _dividers.Clear();

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");

            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                // 각 구역의 시작 각도 = 그 구역과 이전 구역의 경계.
                zones.GetZoneAngularRangeDeg(zone, out float startDeg, out _);
                float rad = startDeg * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

                // 안쪽 눈금: innerRadius에서 바깥으로 / 바깥 눈금: outerRadius에서 안으로
                _dividers.Add(CreateTick(shader, $"_ZoneTickIn_{zone}",  dir, innerRadius, innerRadius + tickLength));
                _dividers.Add(CreateTick(shader, $"_ZoneTickOut_{zone}", dir, outerRadius - tickLength, outerRadius));
            }

            _sortingSynced = false;   // 새 선들은 유닛 정렬을 다시 따라가야 한다
        }

        private LineRenderer CreateTick(Shader shader, string name, Vector3 dir, float fromRadius, float toRadius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, dir * fromRadius + Vector3.up * floorY);
            lr.SetPosition(1, dir * toRadius + Vector3.up * floorY);
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(shader);
            return lr;
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
