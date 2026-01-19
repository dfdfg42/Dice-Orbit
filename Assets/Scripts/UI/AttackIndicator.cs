using UnityEngine;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 공격 인디케이터 타입
    /// </summary>
    public enum AttackIndicatorType
    {
        AreaAttack,     // 범위 공격 (타일 빨간색)
        TargetedAttack  // 타겟팅 공격 (빨간 줄)
    }
    
    /// <summary>
    /// 몬스터 공격 시각화
    /// </summary>
    public class AttackIndicator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Color areaAttackColor = new Color(1f, 0f, 0f, 0.5f); // 반투명 빨강
        [SerializeField] private Color targetLineColor = Color.red;
        [SerializeField] private float lineWidth = 0.1f;
        
        private LineRenderer lineRenderer;
        private AttackIndicatorType currentType;
        
        // 타일 하이라이트용
        private Data.TileData[] highlightedTiles;
        
        // 타겟팅 공격용 실시간 업데이트
        private Transform monsterTransform;
        private Transform targetTransform;
        
        private void Awake()
        {
            // LineRenderer 추가 (타겟팅 공격용)
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = targetLineColor;
            lineRenderer.endColor = targetLineColor;
            lineRenderer.enabled = false;
            lineRenderer.sortingOrder = 100; // 위에 표시
        }
        
        private void Update()
        {
            // 타겟팅 공격 라인 실시간 업데이트
            if (currentType == AttackIndicatorType.TargetedAttack && lineRenderer.enabled)
            {
                UpdateTargetLine();
            }
        }
        
        /// <summary>
        /// 타겟 라인 실시간 업데이트
        /// </summary>
        private void UpdateTargetLine()
        {
            if (monsterTransform != null && targetTransform != null)
            {
                Vector3 startPos = monsterTransform.position + Vector3.up * 0.5f;
                Vector3 endPos = targetTransform.position + Vector3.up * 0.5f;
                
                lineRenderer.SetPosition(0, startPos);
                lineRenderer.SetPosition(1, endPos);


            }

        }
        
        /// <summary>
        /// 범위 공격 미리보기 표시
        /// </summary>
        public void ShowAreaAttack(Data.TileData[] tiles)
        {
            currentType = AttackIndicatorType.AreaAttack;
            highlightedTiles = tiles;
            
            // 타일들 빨간색으로 하이라이트
            foreach (var tile in tiles)
            {
                if (tile != null)
                {
                    tile.Highlight(areaAttackColor);
                }
            }
            
            Debug.Log($"[AttackIndicator] Area attack on {tiles.Length} tiles");
        }
        
        /// <summary>
        /// 타겟팅 공격 미리보기 표시 (몬스터 → 캐릭터)
        /// </summary>
        public void ShowTargetedAttack(Transform monster, Transform target)
        {
            currentType = AttackIndicatorType.TargetedAttack;
            
            Debug.Log($"[AttackIndicator] ShowTargetedAttack called");
            Debug.Log($"  Monster: {(monster != null ? monster.name : "NULL")}");
            Debug.Log($"  Target: {(target != null ? target.name : "NULL")}");
            
            // Transform 저장 (실시간 업데이트용)
            monsterTransform = monster;
            targetTransform = target;
            
            // LineRenderer 활성화
            lineRenderer.enabled = true;
            lineRenderer.positionCount = 2;
            
            // 초기 위치 설정
            UpdateTargetLine();
            
            Debug.Log($"[AttackIndicator] Line enabled: {lineRenderer.enabled}, Type: {currentType}");
        }
        
        /// <summary>
        /// 인디케이터 숨기기
        /// </summary>
        public void Hide()
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
            
            // LineRenderer 비활성화
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
            
            // Transform 참조 초기화
            monsterTransform = null;
            targetTransform = null;
            
            Debug.Log("[AttackIndicator] Hidden");
        }
    }
}
