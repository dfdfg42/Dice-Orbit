using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 타일 시각화 컴포넌트
    /// </summary>
    public class TileVisual : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material levelUpMaterial;
        [SerializeField] private Material specialMaterial;
        [SerializeField] private Material highlightMaterial;

        private MeshRenderer meshRenderer;
        private Material originalMaterial;
        private bool isHighlighted = false;
        
        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();

            // Prefab에 이미 메쉬가 있으면 그것을 우선 사용
            if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();

            // 메쉬가 전혀 없는 경우에만 런타임 생성 메쉬 사용
            if (meshRenderer == null)
            {
                Debug.LogWarning($"[TileVisual] MeshRenderer missing on '{name}'. Tile visual updates will be skipped.");
            }
        }
        
        /// <summary>
        /// 타일 타입에 따른 비주얼 설정
        /// </summary>
        public void SetTileType(TileType type)
        {
            if (meshRenderer == null) return;

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
            if (meshRenderer == null) return;
            
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
        /// 호환용 API. 타일 크기 조정은 프리팹 메쉬에서 직접 관리합니다.
        /// </summary>
        public void SetTileSize(float width, float height)
        {
            // Intentionally no-op.
        }
    }
}
