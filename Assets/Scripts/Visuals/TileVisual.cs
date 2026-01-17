using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 타일 시각화 컴포넌트
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class TileVisual : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material levelUpMaterial;
        [SerializeField] private Material specialMaterial;
        [SerializeField] private Material highlightMaterial;
        
        [Header("Settings")]
        [SerializeField] private float tileWidth = 1.5f;
        [SerializeField] private float tileHeight = 0.2f;
        
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private Material originalMaterial;
        private bool isHighlighted = false;
        
        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();
            
            // 기본 육면체 메쉬 생성
            CreateTileMesh();
        }
        
        /// <summary>
        /// 타일 타입에 따른 비주얼 설정
        /// </summary>
        public void SetTileType(TileType type)
        {
            Material mat = type switch
            {
                TileType.Normal => normalMaterial,
                TileType.LevelUp => levelUpMaterial,
                TileType.Special => specialMaterial,
                _ => normalMaterial
            };
            
            originalMaterial = mat;
            if (!isHighlighted)
            {
                meshRenderer.material = mat;
            }
        }
        
        /// <summary>
        /// 타일 하이라이트 설정
        /// </summary>
        public void SetHighlight(bool highlight, Color color)
        {
            isHighlighted = highlight;
            
            if (highlight)
            {
                if (highlightMaterial != null)
                {
                    Material highlightInstance = new Material(highlightMaterial);
                    highlightInstance.color = color;
                    meshRenderer.material = highlightInstance;
                }
                else
                {
                    Material coloredMat = new Material(originalMaterial);
                    coloredMat.color = color;
                    meshRenderer.material = coloredMat;
                }
            }
            else
            {
                meshRenderer.material = originalMaterial;
            }
        }
        
        /// <summary>
        /// 타일 메쉬 생성 (육면체)
        /// </summary>
        private void CreateTileMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Tile Mesh";
            
            // 간단한 육면체 메쉬 (추후 커스터마이징 가능)
            Vector3[] vertices = new Vector3[]
            {
                // Bottom face
                new Vector3(-tileWidth/2, 0, -tileWidth/2),
                new Vector3(tileWidth/2, 0, -tileWidth/2),
                new Vector3(tileWidth/2, 0, tileWidth/2),
                new Vector3(-tileWidth/2, 0, tileWidth/2),
                // Top face
                new Vector3(-tileWidth/2, tileHeight, -tileWidth/2),
                new Vector3(tileWidth/2, tileHeight, -tileWidth/2),
                new Vector3(tileWidth/2, tileHeight, tileWidth/2),
                new Vector3(-tileWidth/2, tileHeight, tileWidth/2),
            };
            
            int[] triangles = new int[]
            {
                // Top
                4, 5, 6, 4, 6, 7,
                // Bottom
                0, 2, 1, 0, 3, 2,
                // Front
                0, 1, 5, 0, 5, 4,
                // Back
                3, 7, 6, 3, 6, 2,
                // Left
                0, 4, 7, 0, 7, 3,
                // Right
                1, 2, 6, 1, 6, 5
            };
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            
            meshFilter.mesh = mesh;
        }
        
        /// <summary>
        /// 타일 크기 설정
        /// </summary>
        public void SetTileSize(float width, float height)
        {
            tileWidth = width;
            tileHeight = height;
            CreateTileMesh();
        }
    }
}
