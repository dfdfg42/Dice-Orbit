using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        [SerializeField] private Color targetLineColor = Color.red;
        [SerializeField] private float lineWidth = 0.1f;

        // 다중 몬스터 Intent 관리
        private Dictionary<Core.Monster, Data.AttackIntent> registeredIntents 
            = new Dictionary<Core.Monster, Data.AttackIntent>();

        // 각 몬스터별 LineRenderer (타겟팅 공격용)
        private Dictionary<Core.Monster, LineRenderer> monsterLineRenderers 
            = new Dictionary<Core.Monster, LineRenderer>();

        // 몬스터별 타일 추적 (타일 하이라이트 관리)
        private Dictionary<Core.Monster, Data.TileData[]> monsterTiles 
            = new Dictionary<Core.Monster, Data.TileData[]>();

        // 타일 하이라이트용 (모든 몬스터의 타일 병합)
        private Data.TileData[] highlightedTiles;

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
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // 생성된 LineRenderer들 정리
            foreach (var line in monsterLineRenderers.Values)
            {
                if (line != null && line.gameObject != null)
                {
                    Destroy(line.gameObject);
                }
            }
            monsterLineRenderers.Clear();
            monsterTiles.Clear();
        }

        /// <summary>
        /// 몬스터의 AttackIntent 등록
        /// </summary>
        public void AddAttackIntent(Core.Monster monster, Data.AttackIntent intent)
        {
            if (monster == null || intent == null) return;
            
            registeredIntents[monster] = intent;
            
            // 이미 Show 상태면 즉시 시각화
            if (isShowing)
            {
                ShowIntentForMonster(monster, intent);
            }
            
            Debug.Log($"[AttackIndicator] Intent registered for {monster.name}");
        }

        /// <summary>
        /// 몬스터의 AttackIntent 제거
        /// </summary>
        public void RemoveAttackIntent(Core.Monster monster)
        {
            if (monster == null) return;

            registeredIntents.Remove(monster);

            // 해당 몬스터의 LineRenderer 제거
            if (monsterLineRenderers.TryGetValue(monster, out var line))
            {
                if (line != null && line.gameObject != null)
                {
                    Destroy(line.gameObject);
                }
                monsterLineRenderers.Remove(monster);
            }

            // 해당 몬스터의 타일 제거
            if (monsterTiles.TryGetValue(monster, out var tiles))
            {
                // 다른 몬스터가 사용 중인 타일 확인
                var otherMonsterTiles = monsterTiles
                    .Where(kvp => kvp.Key != monster && kvp.Key != null)
                    .SelectMany(kvp => kvp.Value ?? System.Array.Empty<Data.TileData>())
                    .Where(t => t != null)
                    .Distinct()
                    .ToHashSet();

                // 해당 몬스터만 사용하던 타일만 언하이라이트
                foreach (var tile in tiles)
                {
                    if (tile != null && !otherMonsterTiles.Contains(tile))
                    {
                        tile.ClearHighlight();
                    }
                }

                monsterTiles.Remove(monster);

                // highlightedTiles 재계산
                RecalculateHighlightedTiles();
            }

            Debug.Log($"[AttackIndicator] Intent removed for {monster.name}");
        }

        private void Update()
        {
            // 모든 타겟팅 공격 라인 실시간 업데이트 (항상 실행)
            UpdateAllTargetLines();
        }

        /// <summary>
        /// 모든 타겟 라인 실시간 업데이트
        /// </summary>
        private void UpdateAllTargetLines()
        {
            foreach (var kvp in registeredIntents)
            {
                var monster = kvp.Key;
                var intent = kvp.Value;

                if (monster == null || intent == null) continue;

                // LineRenderer가 있고 활성화되어 있는 경우만 업데이트
                if (monsterLineRenderers.TryGetValue(monster, out var line) && line != null && line.enabled)
                {
                    var targets = intent.Targets;
                    if (targets != null && targets.Count > 0 && targets[0] != null)
                    {
                        Vector3 startPos = monster.transform.position + Vector3.up * 0.5f;
                        Vector3 endPos = targets[0].transform.position + Vector3.up * 0.5f;

                        line.SetPosition(0, startPos);
                        line.SetPosition(1, endPos);
                    }
                }
            }
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
            if (intent.TargetTiles != null && intent.TargetTiles.Length > 0)
            {
                ShowTileAttackForMonster(intent.TargetTiles, monster);
                return;
            }

            // 타겟 기반 공격
            var targets = intent.Targets;
            if (targets == null || targets.Count == 0) return;

            if (intent.TargetType == Data.TargetType.Single && targets.Count == 1)
            {
                ShowTargetedAttackForMonster(monster, targets[0]);
            }
            else if (intent.TargetType == Data.TargetType.All)
            {
                var targetTiles = targets
                    .Where(c => c != null && c.CurrentTile != null)
                    .Select(c => c.CurrentTile)
                    .Distinct()
                    .ToArray();

                if (targetTiles.Length > 0)
                {
                    ShowTileAttackForMonster(targetTiles, monster);
                }
            }
        }

        /// <summary>
        /// 타일 공격 시각화 (타일 하이라이트)
        /// </summary>
        private void ShowTileAttackForMonster(Data.TileData[] tiles, Core.Monster monster)
        {
            if (tiles == null || tiles.Length == 0 || monster == null) return;

            // 몬스터별 타일 저장
            monsterTiles[monster] = tiles;

            // highlightedTiles 재계산
            RecalculateHighlightedTiles();

            // 타일 하이라이트
            foreach (var tile in tiles)
            {
                if (tile != null)
                {
                    tile.Highlight(tileAttackColor);
                }
            }
        }

        /// <summary>
        /// 타겟팅 공격 시각화 (몬스터별 LineRenderer)
        /// </summary>
        private void ShowTargetedAttackForMonster(Core.Monster monster, Core.Character target)
        {
            if (monster == null || target == null) return;

            // 몬스터별 LineRenderer 생성 또는 재사용
            if (!monsterLineRenderers.TryGetValue(monster, out var line) || line == null)
            {
                line = CreateLineRenderer();
                monsterLineRenderers[monster] = line;
            }

            // LineRenderer 설정
            line.enabled = true;
            line.positionCount = 2;

            Vector3 startPos = monster.transform.position + Vector3.up * 0.5f;
            Vector3 endPos = target.transform.position + Vector3.up * 0.5f;

            line.SetPosition(0, startPos);
            line.SetPosition(1, endPos);
        }

        /// <summary>
        /// LineRenderer 생성 (재사용 가능한 설정)
        /// </summary>
        private LineRenderer CreateLineRenderer()
        {
            var go = new GameObject("AttackLine");
            go.transform.SetParent(transform);

            var line = go.AddComponent<LineRenderer>();
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = targetLineColor;
            line.endColor = targetLineColor;
            line.sortingOrder = 100;
            line.enabled = false;

            return line;
        }

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
                .ToArray();
        }

        /// <summary>
        /// 시각화만 초기화 (데이터는 유지)
        /// </summary>
        private void ClearVisualization()
        {
            // 타일 하이라이트 제거
            if (highlightedTiles != null)
            {
                foreach (var tile in highlightedTiles)
                {
                    if (tile != null)
                    {
                        tile.ClearHighlight();
                    }
                }
                highlightedTiles = null;
            }

            // 모든 LineRenderer 비활성화
            foreach (var line in monsterLineRenderers.Values)
            {
                if (line != null)
                {
                    line.enabled = false;
                }
            }

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
