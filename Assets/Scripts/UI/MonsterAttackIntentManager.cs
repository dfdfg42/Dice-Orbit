using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 몬스터 공격 시각화
    /// </summary>
    public class MonsterAttackIntentManager : MonoBehaviour
    {
        public static MonsterAttackIntentManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private Color tileAttackColor = new Color(1f, 0f, 0f, 0.5f); // 반투명 빨강
        [SerializeField] private Color targetLineColor = Color.red;                  // 정체성 색 폴백
        // 조준선 폭·화살촉 등 형태는 DashedArcLine이 단일 소스로 소유(DefaultWidth).
        [SerializeField] private int parabolaSegments = 24;
        [SerializeField] private float parabolaHeightMultiplier = 0.2f;
        [SerializeField] private float minParabolaHeight = 0.4f;
        [SerializeField] private float maxParabolaHeight = 2.0f;
        [SerializeField] private float dashWorldLength = 0.45f;
        [SerializeField] private float fadeEdgeRatio = 0.14f;

        // 다중 몬스터 Intent 관리
        private Dictionary<Core.Monster, Data.AttackIntent> registeredIntents
            = new Dictionary<Core.Monster, Data.AttackIntent>();

        // 몬스터별 조준 아크 (대상마다 1개 — DashedArcLine 공용 헬퍼, 아크+화살촉)
        private struct ArcPair
        {
            public GameObject Root;
            public LineRenderer Arc;
            public LineRenderer Arrow;
        }
        private readonly Dictionary<Core.Monster, List<ArcPair>> monsterLines
            = new Dictionary<Core.Monster, List<ArcPair>>();

        // 몬스터별 타일 추적 (타일 하이라이트 관리)
        private Dictionary<Core.Monster, List<Data.TileData>> monsterTiles 
            = new Dictionary<Core.Monster, List<Data.TileData>>();

        // 타일 하이라이트용 (모든 몬스터의 타일 병합)
        private List<Data.TileData> highlightedTiles;

        private bool isShowing = false; // Show 상태 플래그

        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[AttackIndicator] Duplicate instance detected. Destroying.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // 임시로 1초마다 RefreshAttackIntent 호출
            InvokeRepeating(nameof(RefreshAttackIntent), 1f, 1f);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // 생성된 조준 아크 정리
            foreach (var pairs in monsterLines.Values)
                DestroyPairs(pairs);
            monsterLines.Clear();
            monsterTiles.Clear();
        }

        private static void DestroyPairs(List<ArcPair> pairs)
        {
            if (pairs == null) return;
            foreach (var p in pairs)
                if (p.Root != null) Destroy(p.Root);
            pairs.Clear();
        }

        /*
         * 플레이어 및 몬스터 행동 종료 후 새로고침하는 것이 정석이지만, 행동 큐 시스템을 추가할 때 작업하는 게 좋아 보임
         * 지금은 임시로 1초마다 갱신하도록 처리함
         */
        public void RefreshAttackIntent()
        {
            if (!isShowing) return; // Show 상태가 아닐 때는 시각화가 없으므로 갱신할 필요 없음

            // hashmap 순회하며 삭제를 위해 LINQ 사용 (모든 등록된 개체 갱신)
            var monsters = registeredIntents.Keys.ToList();

            foreach (var monster in monsters)
            {
                if (monster == null || !registeredIntents.TryGetValue(monster, out var intent) || intent == null)
                {
                    RemoveAttackIntent(monster);
                    continue;
                }

                RemoveAttackIntent(monster);
                AddAttackIntent(monster, intent);
            }
        }

        /// <summary>
        /// 몬스터의 AttackIntent 등록
        /// </summary>
        public void AddAttackIntent(Core.Monster monster, Data.AttackIntent intent)
        {
            if (monster == null || intent == null) return;

            intent.RefreshTargets();
            registeredIntents[monster] = intent;
            
            // 이미 Show 상태면 즉시 시각화
            if (isShowing)
            {
                ShowIntentForMonster(monster, intent);
            }
            
            //Debug.Log($"[AttackIndicator] Intent registered for {monster.name}");
        }

        /// <summary>
        /// 몬스터의 AttackIntent 제거
        /// </summary>
        public void RemoveAttackIntent(Core.Monster monster)
        {
            if (monster == null) return;

            registeredIntents.Remove(monster);

            // 해당 몬스터의 조준 아크 제거
            if (monsterLines.TryGetValue(monster, out var pairs))
            {
                DestroyPairs(pairs);
                monsterLines.Remove(monster);
            }

            // 플로팅 UI 제거는 이 몬스터의 것만 따로 완벽하게 제거
            if (activeFloatingUIs.TryGetValue(monster, out var uiList))
            {
                if (uiList != null)
                {
                    foreach (var ui in uiList)
                    {
                        if (ui != null) Destroy(ui);
                    }
                }
                activeFloatingUIs.Remove(monster);
            }

            // 해당 몬스터의 타일 제거 후 남은 몬스터 색으로 오버레이 재구성
            if (monsterTiles.ContainsKey(monster))
            {
                monsterTiles.Remove(monster);
                RecalculateHighlightedTiles();
                RebuildTileOverlays();
            }

            //Debug.Log($"[AttackIndicator] Intent removed for {monster.name}");
        }

        private void Update()
        {
            // 모든 타겟팅 공격 라인 실시간 업데이트 (항상 실행 — 점선 흐름은 DashedArcLine 드라이버가 담당)
            UpdateAllTargetLines();
        }

        /// <summary>
        /// 모든 타겟 라인 실시간 업데이트 (대상마다 아크 1개, 몬스터 정체성 색)
        /// </summary>
        private void UpdateAllTargetLines()
        {
            foreach (var kvp in registeredIntents)
            {
                var monster = kvp.Key;
                var intent = kvp.Value;

                if (monster == null || intent == null) continue;
                if (!monsterLines.TryGetValue(monster, out var pairs) || pairs == null || pairs.Count == 0) continue;

                var targets = intent.Targets;
                if (targets == null) continue;

                Color color = ResolveMonsterLineColor(monster);
                Vector3 startPos = monster.transform.position + Vector3.up * 0.5f;

                for (int i = 0; i < pairs.Count; i++)
                {
                    var pair = pairs[i];
                    if (pair.Arc == null || !pair.Arc.enabled) continue;

                    if (i >= targets.Count || targets[i] == null) continue;

                    Vector3 endPos = targets[i].transform.position + Vector3.up * 0.5f;
                    Visuals.DashedArcLine.SetArcWithArrow(pair.Arc, pair.Arrow, startPos, endPos, color,
                        parabolaSegments, parabolaHeightMultiplier, minParabolaHeight, maxParabolaHeight,
                        dashWorldLength, fadeEdgeRatio);
                }
            }
        }

        /// <summary>조준선 색 = 몬스터 정체성 색 (타일 오버레이 밴드와 일치 → 누가 누굴 노리는지 색으로 매칭).</summary>
        private Color ResolveMonsterLineColor(Core.Monster monster)
        {
            var identity = Visuals.MonsterIdentityManager.Instance;
            return identity != null ? identity.GetColor(monster) : targetLineColor;
        }

        /// <summary>
        /// 등록된 모든 Intent 시각화
        /// </summary>
        public void Show()
        {
            // 기존 시각화 초기화
            ClearVisualization();
            isShowing = true; // 플래그 활성화
            
            // 각 몬스터별로 시각화
            foreach (var kvp in registeredIntents)
            {
                ShowIntentForMonster(kvp.Key, kvp.Value);
            }

            Debug.Log($"[AttackIndicator] Showing {registeredIntents.Count} monster intents");
        }

        /// <summary>
        /// 특정 몬스터의 Intent 시각화
        /// </summary>
        private void ShowIntentForMonster(Core.Monster monster, Data.AttackIntent intent)
        {
            if (monster == null || intent == null) return;

            // 타일 기반 공격
            if (intent.TargetType == Data.TargetType.Tiles)
            {
                ShowTileAttackForMonster(intent.TargetTiles.ToList(), monster, intent);
                return;
            }
            else if (intent.TargetType == Data.TargetType.Characters)
            {
                var targets = intent.Targets;
                if (targets == null || targets.Count == 0) return;
                ShowTargetedAttackForMonster(monster, targets);   // 다중 타겟 전부 아크 표시
            }
            else if (intent.TargetType == Data.TargetType.Self || intent.TargetType == Data.TargetType.None)
            {
                // 타겟이 존재하지 않는 버프/대기/특수 효과이므로 
                // 월드 캔버스에 몬스터 머리 위 말풍선만 남기고 별도의 선(Line)/타일 이펙트(Tile)를 그리지 않음.
                return;
            }
            else
            {
                Debug.LogError($"[AttackIndicator] Unknown TargetType for {monster.name}: {intent.TargetType}");
            }
        }

        /// <summary>
        /// 타일 공격 시각화 (타일 하이라이트 및 풍선 띄우기)
        /// </summary>
        private void ShowTileAttackForMonster(List<Data.TileData> tiles, Core.Monster monster, Data.AttackIntent intent)
        {
            if (tiles == null || tiles.Count == 0 || monster == null) return;

            Visuals.MonsterIdentityManager.EnsureInstance();
            Color monsterColor = Visuals.MonsterIdentityManager.Instance.GetColor(monster);

            // 몬스터별 타일 저장
            monsterTiles[monster] = tiles;

            if (!activeFloatingUIs.ContainsKey(monster))
            {
                activeFloatingUIs[monster] = new List<GameObject>();
            }

            // highlightedTiles 재계산
            RecalculateHighlightedTiles();

            // 플로팅 말풍선 생성 (타일 색상 표시는 파이 오버레이가 담당)
            foreach (var tile in tiles)
            {
                if (tile == null) continue;

                if (floatingIntentUIPrefab != null)
                {
                    var floatingUIObj = Instantiate(floatingIntentUIPrefab, tile.transform.position, Quaternion.identity);
                    var floatingUI = floatingUIObj.GetComponent<FloatingIntentUI>();

                    if (floatingUI != null)
                    {
                        // 아이콘이 없으면 몬스터 정체성 색(방어 의도는 파랑)으로 말풍선 표시
                        Color colorToUse = Color.white;
                        if (intent.Icon == null)
                        {
                            colorToUse = intent.Type == Data.IntentType.Defend ? Color.blue : monsterColor;
                        }

                        floatingUI.Setup(tile.transform, intent.Icon, colorToUse);
                        activeFloatingUIs[monster].Add(floatingUIObj);
                    }
                }
            }

            // 모든 몬스터의 타일 색상을 합쳐 파이 오버레이 재구성
            RebuildTileOverlays();
        }

        /// <summary>
        /// 타겟팅 공격 시각화 — 대상마다 조준 아크 1개 (몬스터 정체성 색 + 화살촉).
        /// </summary>
        private void ShowTargetedAttackForMonster(Core.Monster monster, IReadOnlyList<Core.Unit> targets)
        {
            if (monster == null || targets == null || targets.Count == 0) return;

            if (!monsterLines.TryGetValue(monster, out var pairs) || pairs == null)
            {
                pairs = new List<ArcPair>();
                monsterLines[monster] = pairs;
            }
            DestroyPairs(pairs);   // 대상 수가 바뀔 수 있으므로 재생성

            Color color = ResolveMonsterLineColor(monster);
            Vector3 startPos = monster.transform.position + Vector3.up * 0.5f;

            foreach (var target in targets)
            {
                if (target == null) continue;

                var root = new GameObject("_AttackArc");
                root.transform.SetParent(transform, false);
                var arc   = Visuals.DashedArcLine.CreateArc(root.transform);
                var arrow = Visuals.DashedArcLine.CreateArrow(root.transform);

                Vector3 endPos = target.transform.position + Vector3.up * 0.5f;
                Visuals.DashedArcLine.SetArcWithArrow(arc, arrow, startPos, endPos, color,
                    parabolaSegments, parabolaHeightMultiplier, minParabolaHeight, maxParabolaHeight,
                    dashWorldLength, fadeEdgeRatio);
                Visuals.DashedArcLine.SetVisible(arc, arrow, true);

                pairs.Add(new ArcPair { Root = root, Arc = arc, Arrow = arrow });
            }
        }

        // (점선 텍스처/애니메이션/포물선/그라디언트는 DashedArcLine 공용 헬퍼로 이전 — 2026-07)

        /// <summary>
        /// highlightedTiles 재계산 (monsterTiles 기반)
        /// </summary>
        private void RecalculateHighlightedTiles()
        {
            highlightedTiles = monsterTiles.Values
                .Where(tiles => tiles != null)
                .SelectMany(tiles => tiles)
                .Where(tile => tile != null)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 현재 (몬스터 → 공격 타일) 정보를 (타일 → 몬스터 색상 목록) 으로 변환해
        /// 파이 색상 오버레이를 재구성한다. 한 타일에 N개 색이 모이면 N등분된다.
        /// </summary>
        private void RebuildTileOverlays()
        {
            Visuals.MonsterIdentityManager.EnsureInstance();
            Visuals.MonsterTileColorOverlayManager.EnsureInstance();

            var map = new Dictionary<Data.TileData, List<Color>>();
            // 몬스터를 안정된 순서로 순회 → 한 타일의 파이 조각 색 순서가 턴마다 바뀌지 않도록 고정
            var orderedMonsters = monsterTiles.Keys
                .Where(m => m != null)
                .OrderBy(m => m.GetInstanceID())
                .ToList();

            foreach (var monster in orderedMonsters)
            {
                var tiles = monsterTiles[monster];
                if (tiles == null) continue;

                Color col = Visuals.MonsterIdentityManager.Instance.GetColor(monster);
                foreach (var tile in tiles)
                {
                    if (tile == null) continue;
                    if (!map.TryGetValue(tile, out var listC))
                    {
                        listC = new List<Color>();
                        map[tile] = listC;
                    }
                    listC.Add(col);
                }
            }

            Visuals.MonsterTileColorOverlayManager.Instance.Rebuild(map);
        }

        // 플로팅 UI 프리팹 보관
        [Header("Floating Tile UI")]
        [SerializeField] private GameObject floatingIntentUIPrefab;
        
        // 생성된 플로팅 UI 인스턴스 관리
        private Dictionary<Core.Monster, List<GameObject>> activeFloatingUIs = new Dictionary<Core.Monster, List<GameObject>>();

        /// <summary>
        /// 시각화만 초기화 (데이터는 유지)
        /// </summary>
        private void ClearVisualization()
        {
            // 타일 색상 오버레이 제거
            Visuals.MonsterTileColorOverlayManager.Instance?.Clear();
            highlightedTiles = null;

            // 모든 플로팅 풍선 제거
            foreach (var uiList in activeFloatingUIs.Values)
            {
                if (uiList != null)
                {
                    foreach (var ui in uiList)
                    {
                        if (ui != null) Destroy(ui);
                    }
                }
            }
            activeFloatingUIs.Clear();

            // 모든 조준 아크 제거
            foreach (var pairs in monsterLines.Values)
                DestroyPairs(pairs);
            monsterLines.Clear();

            // 몬스터별 타일 추적 초기화
            monsterTiles.Clear();
        }

        /// <summary>
        /// 인디케이터 숨기기 (시각화만 숨김, 데이터는 유지)
        /// </summary>
        public void Hide()
        {
            ClearVisualization();
            isShowing = false; // 플래그 비활성화
        }
    }
}
