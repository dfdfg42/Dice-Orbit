using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 궤도 전체 시각화 - 가이드 라인 등
    /// </summary>
    public class OrbitVisualizer : MonoBehaviour
    {
        [Header("Orbit Guide")]
        [SerializeField] private bool showOrbitLine = true;
        [SerializeField] private Color orbitLineColor = new Color(0.5f, 0.5f, 1f, 0.5f);
        [SerializeField] private float orbitRadius = 8f;
        [SerializeField] private int lineSegments = 64;
        
        [Header("Materials")]
        [SerializeField] private Material lineMaterial;
        
        private LineRenderer lineRenderer;
        
        private void Awake()
        {
            if (showOrbitLine)
            {
                CreateOrbitGuide();
            }
        }
        
        /// <summary>
        /// 궤도 가이드 라인 생성
        /// </summary>
        private void CreateOrbitGuide()
        {
            GameObject lineObj = new GameObject("Orbit Guide Line");
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;
            
            lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.positionCount = lineSegments + 1;
            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = false;
            
            // 라인 두께
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            
            // 머티리얼 설정
            if (lineMaterial != null)
            {
                lineRenderer.material = lineMaterial;
            }
            else
            {
                // 기본 머티리얼
                Material mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = orbitLineColor;
                lineRenderer.material = mat;
            }
            
            // 원 형태 포지션 설정
            for (int i = 0; i <= lineSegments; i++)
            {
                float angle = i * 2 * Mathf.PI / lineSegments;
                float x = Mathf.Cos(angle) * orbitRadius;
                float z = Mathf.Sin(angle) * orbitRadius;
                lineRenderer.SetPosition(i, new Vector3(x, 0.1f, z));
            }
        }
        
        /// <summary>
        /// 궤도 반지름 설정
        /// </summary>
        public void SetOrbitRadius(float radius)
        {
            orbitRadius = radius;
            
            if (lineRenderer != null)
            {
                for (int i = 0; i <= lineSegments; i++)
                {
                    float angle = i * 2 * Mathf.PI / lineSegments;
                    float x = Mathf.Cos(angle) * orbitRadius;
                    float z = Mathf.Sin(angle) * orbitRadius;
                    lineRenderer.SetPosition(i, new Vector3(x, 0.1f, z));
                }
            }
        }
        
        /// <summary>
        /// 궤도 라인 표시/숨김
        /// </summary>
        public void SetOrbitLineVisible(bool visible)
        {
            showOrbitLine = visible;
            if (lineRenderer != null)
            {
                lineRenderer.enabled = visible;
            }
        }
        
        /// <summary>
        /// Gizmo 그리기
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showOrbitLine) return;
            
            Gizmos.color = orbitLineColor;
            
            Vector3 prevPoint = transform.position + new Vector3(orbitRadius, 0, 0);
            
            for (int i = 1; i <= lineSegments; i++)
            {
                float angle = i * 2 * Mathf.PI / lineSegments;
                float x = Mathf.Cos(angle) * orbitRadius;
                float z = Mathf.Sin(angle) * orbitRadius;
                Vector3 newPoint = transform.position + new Vector3(x, 0.1f, z);
                
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
    }
}
