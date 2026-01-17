using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 중앙 몬스터 구역
    /// </summary>
    public class CenterZone : MonoBehaviour
    {
        [Header("Center Zone Settings")]
        [SerializeField] private float radius = 3f;
        [SerializeField] private Color zoneColor = new Color(0.8f, 0.2f, 0.2f, 0.3f);
        [SerializeField] private Material zoneMaterial;
        
        [Header("Visual")]
        [SerializeField] private bool showGizmo = true;
        
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        
        private void Awake()
        {
            CreateCenterZoneVisual();
        }
        
        /// <summary>
        /// 중앙 구역 시각화 생성
        /// </summary>
        private void CreateCenterZoneVisual()
        {
            // MeshFilter와 MeshRenderer 추가
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            
            // 원형 메쉬 생성
            meshFilter.mesh = CreateCircleMesh(radius, 32);
            
            // 머티리얼 설정
            if (zoneMaterial != null)
            {
                meshRenderer.material = zoneMaterial;
            }
            else
            {
                // 기본 머티리얼 생성
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = zoneColor;
                mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                
                meshRenderer.material = mat;
            }
        }
        
        /// <summary>
        /// 원형 메쉬 생성
        /// </summary>
        private Mesh CreateCircleMesh(float radius, int segments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Center Zone Circle";
            
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];
            
            // 중심점
            vertices[0] = Vector3.zero;
            
            // 원주 상의 점들
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2 * Mathf.PI / segments;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                vertices[i + 1] = new Vector3(x, 0.05f, z); // 약간 위로 올려서 겹침 방지
            }
            
            // 삼각형 구성
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            
            return mesh;
        }
        
        /// <summary>
        /// 몬스터가 중앙에 위치하는지 확인
        /// </summary>
        public bool IsInCenterZone(Vector3 position)
        {
            float distance = Vector2.Distance(
                new Vector2(position.x, position.z),
                new Vector2(transform.position.x, transform.position.z)
            );
            
            return distance <= radius;
        }
        
        /// <summary>
        /// Gizmo 그리기 (Scene 뷰에서만 보임)
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showGizmo) return;
            
            Gizmos.color = zoneColor;
            
            // 원 그리기
            int segments = 32;
            Vector3 prevPoint = transform.position + new Vector3(radius, 0, 0);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * 2 * Mathf.PI / segments;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                Vector3 newPoint = transform.position + new Vector3(x, 0, z);
                
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
        
        /// <summary>
        /// 중앙 구역 크기 설정
        /// </summary>
        public void SetRadius(float newRadius)
        {
            radius = newRadius;
            if (meshFilter != null)
            {
                meshFilter.mesh = CreateCircleMesh(radius, 32);
            }
        }
    }
}
