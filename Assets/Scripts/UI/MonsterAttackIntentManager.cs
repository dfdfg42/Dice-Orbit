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
        [SerializeField] private Color targetLineColor = Color.red;
        [SerializeField] private float lineWidth = 0.1f;
        [SerializeField] private int parabolaSegments = 24;
        [SerializeField] private float parabolaHeightMultiplier = 0.2f;
        [SerializeField] private float minParabolaHeight = 0.4f;
        [SerializeField] private float maxParabolaHeight = 2.0f;
        [SerializeField] private float dashWorldLength = 0.45f;
        [SerializeField] private float dashScrollSpeed = 1.75f;
        [SerializeField] private float fadeEdgeRatio = 0.14f;
        [SerializeField] private float dashPixelShiftInterval = 0.05f;

        // 다중 몬스터 Intent 관리
        private Dictionary<Core.Monster, Data.AttackIntent> registeredIntents 
            = new Dictionary<Core.Monster, Data.AttackIntent>();

        // 각 몬스터별 LineRenderer (타겟팅 공격용)
        private Dictionary<Core.Monster, LineRenderer> monsterLineRenderers 
            = new Dictionary<Core.Monster, LineRenderer>();

        // 몬스터별 타일 추적 (타일 하이라이트 관리)
        private Dictionary<Core.Monster, List<Data.TileData>> monsterTiles 
            = new Dictionary<Core.Monster, List<Data.TileData>>();

        // 타일 하이라이트용 (모든 몬스터의 타일 병합)
        private List<Data.TileData> highlightedTiles;

        private bool isShowing = false; // Show 상태 플래그
        private Texture2D dashedLineTexture;
        private Color[] dashPixels;
        private float dashPixelTimer = 0f;

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
            CreateDashTexture();

            // 임시로 1초마다 RefreshAttackIntent 호출
            InvokeRepeating(nameof(RefreshAttackIntent), 1f, 1f);
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
                    .SelectMany(kvp => kvp.Value ?? new List<Data.TileData>())
                    .Where(t => t != null)
                    .Distinct()
                    .ToHashSet();

                // 해당 몬스터만 사용하던 타일만 언하이라이트 및 플로팅 풍선 파괴
                foreach (var tile in tiles)
                {
                    if (tile != null && !otherMonsterTiles.Contains(tile))
                    {
                        tile.ClearHighlight();
                        
                        // 이 타일 위의 플로팅 말풍선 제거 (생성될 때의 위치 또는 1.5f 오프셋 적용 위치 모두 체크)
                        var uisToRemove = activeFloatingUIs.Where(ui => ui != null && 
                            (Vector3.Distance(ui.transform.position, tile.transform.position) < 0.1f || 
                             Vector3.Distance(ui.transform.position, tile.transform.position + new Vector3(0, 1.5f, 0)) < 0.1f)).ToList();
                        foreach(var ui in uisToRemove)
                        {
                            activeFloatingUIs.Remove(ui);
                            Destroy(ui);
                        }
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
            AnimateDashLines();
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
                        UpdateParabolaLine(line, startPos, endPos);
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
            if (intent.TargetType == Data.TargetType.Tiles)
            {
                ShowTileAttackForMonster(intent.TargetTiles.ToList(), monster, intent);
                return;
            }
            else if (intent.TargetType == Data.TargetType.Characters)
            {
                var targets = intent.Targets;
                if (targets == null || targets.Count == 0) return;
                if (targets.Count == 1)
                {
                    ShowTargetedAttackForMonster(monster, targets[0]);
                }
                else
                {
                    // 다중 타겟인 경우, 첫 번째 타겟만 시각화 (추후 개선 가능)
                    ShowTargetedAttackForMonster(monster, targets[0]);
                }
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

            // 몬스터별 타일 저장
            monsterTiles[monster] = tiles;

            // highlightedTiles 재계산
            RecalculateHighlightedTiles();

            // 타일 하이라이트 및 플로팅 풍선 생성
            foreach (var tile in tiles)
            {
                if (tile != null)
                {
                    tile.Highlight(tileAttackColor);

                    // 플로팅 말풍선 생성
                    if (floatingIntentUIPrefab != null)
                    {
                        var floatingUIObj = Instantiate(floatingIntentUIPrefab, tile.transform.position, Quaternion.identity);
                        var floatingUI = floatingUIObj.GetComponent<FloatingIntentUI>();
                        
                        if (floatingUI != null)
                        {
                            // 인텐트 색상 결정 (기존 방식 유지)
                            Color colorToUse = Color.white;
                            if (intent.Icon == null)
                            {
                                colorToUse = intent.Type == Data.IntentType.Defend ? Color.blue : tileAttackColor;
                            }
                            
                            // 타깃을 Tile의 Transform으로 설정
                            floatingUI.Setup(tile.transform, intent.Icon, colorToUse);
                            activeFloatingUIs.Add(floatingUIObj);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 타겟팅 공격 시각화 (몬스터별 LineRenderer)
        /// </summary>
        private void ShowTargetedAttackForMonster(Core.Monster monster, Core.Unit target)
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
            Vector3 startPos = monster.transform.position + Vector3.up * 0.5f;
            Vector3 endPos = target.transform.position + Vector3.up * 0.5f;
            UpdateParabolaLine(line, startPos, endPos);
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
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 0;
            line.numCornerVertices = 2;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            line.material = new Material(shader);
            line.material.color = Color.white;
            if (dashedLineTexture != null)
            {
                line.material.mainTexture = dashedLineTexture;
            }

            line.colorGradient = CreateFadedGradient();
            line.sortingOrder = 100;
            line.enabled = false;

            return line;
        }

        private void UpdateParabolaLine(LineRenderer line, Vector3 startPos, Vector3 endPos)
        {
            if (line == null) return;
            line.colorGradient = CreateFadedGradient();

            int segments = Mathf.Max(4, parabolaSegments);
            line.positionCount = segments + 1;

            float distance = Vector3.Distance(startPos, endPos);
            float height = Mathf.Clamp(distance * parabolaHeightMultiplier, minParabolaHeight, maxParabolaHeight);

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(startPos, endPos, t);
                float arc = 4f * t * (1f - t); // 0->1->0 parabola profile
                p.y += arc * height;
                line.SetPosition(i, p);
            }

            if (line.material != null && line.material.mainTexture != null)
            {
                float repeatX = Mathf.Max(1f, distance / Mathf.Max(0.05f, dashWorldLength));
                line.material.mainTextureScale = new Vector2(repeatX, 1f);
            }
        }

        private void AnimateDashLines()
        {
            float scroll = -(Time.time * dashScrollSpeed);

            foreach (var line in monsterLineRenderers.Values)
            {
                if (line == null || !line.enabled) continue;
                if (line.material == null || line.material.mainTexture == null) continue;

                line.material.mainTextureOffset = new Vector2(scroll, 0f);
                if (line.material.HasProperty("_MainTex"))
                {
                    line.material.SetTextureOffset("_MainTex", new Vector2(scroll, 0f));
                }
                if (line.material.HasProperty("_BaseMap"))
                {
                    line.material.SetTextureOffset("_BaseMap", new Vector2(scroll, 0f));
                }
            }

            // Shader가 UV 오프셋을 무시하는 경우에도 점선이 움직이도록 텍스처 자체를 순환시킴.
            AnimateDashTexturePixels();
        }

        private Gradient CreateFadedGradient()
        {
            float edge = Mathf.Clamp01(fadeEdgeRatio);

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(targetLineColor, 0f),
                    new GradientColorKey(targetLineColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, edge),
                    new GradientAlphaKey(1f, 1f - edge),
                    new GradientAlphaKey(0f, 1f)
                }
            );

            return gradient;
        }

        private void CreateDashTexture()
        {
            if (dashedLineTexture != null) return;

            // 8-on / 8-off pattern (repeat in U)
            const int width = 16;
            dashedLineTexture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
                name = "AttackLineDashTex"
            };
            dashPixels = new Color[width];

            for (int x = 0; x < width; x++)
            {
                bool on = x < width / 2;
                dashPixels[x] = on ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
            dashedLineTexture.SetPixels(dashPixels);
            dashedLineTexture.Apply(false, false);
        }

        private void AnimateDashTexturePixels()
        {
            if (dashedLineTexture == null || dashPixels == null || dashPixels.Length <= 1) return;
            if (dashPixelShiftInterval <= 0f) return;

            dashPixelTimer += Time.deltaTime;
            if (dashPixelTimer < dashPixelShiftInterval) return;
            dashPixelTimer = 0f;

            Color last = dashPixels[dashPixels.Length - 1];
            for (int i = dashPixels.Length - 1; i > 0; i--)
            {
                dashPixels[i] = dashPixels[i - 1];
            }
            dashPixels[0] = last;

            dashedLineTexture.SetPixels(dashPixels);
            dashedLineTexture.Apply(false, false);
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
                .ToList();
        }

        // 플로팅 UI 프리팹 보관
        [Header("Floating Tile UI")]
        [SerializeField] private GameObject floatingIntentUIPrefab;
        
        // 생성된 플로팅 UI 인스턴스 관리
        private List<GameObject> activeFloatingUIs = new List<GameObject>();

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

            // 모든 플로팅 풍선 제거
            foreach (var floatingUI in activeFloatingUIs)
            {
                if (floatingUI != null) Destroy(floatingUI);
            }
            activeFloatingUIs.Clear();

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
