using UnityEngine;
using DiceOrbit.Visuals;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 타일 타입 열거형
    /// </summary>
    public enum TileType
    {
        Normal,      // 일반 타일
        LevelUp,     // 레벨업 타일 (시작점)
        Special      // 특수 타일 (추후 확장용)
    }

    /// <summary>
    /// 개별 타일 데이터
    /// </summary>
    public class TileData : MonoBehaviour
    {
        [Header("Tile Properties")]
        [SerializeField] private int tileIndex;
        [SerializeField] private TileType tileType = TileType.Normal;
        
        [Header("Connections")]
        [SerializeField] private TileData nextTile;
        [SerializeField] private TileData previousTile;
        
        [Header("Visual")]
        [SerializeField] private TileVisual tileVisual;
        
        // Properties
        public int TileIndex => tileIndex;
        public TileType Type => tileType;
        public TileData NextTile => nextTile;
        public TileData PreviousTile => previousTile;
        public Vector3 Position => transform.position;
        
        /// <summary>
        /// 타일 초기화
        /// </summary>
        public void Initialize(int index, TileType type, TileVisual visual = null)
        {
            tileIndex = index;
            tileType = type;
            tileVisual = visual;
            
            if (tileVisual != null)
            {
                tileVisual.SetTileType(type);
            }
        }
        
        /// <summary>
        /// 연결 설정 (다음 타일, 이전 타일)
        /// </summary>
        public void SetConnections(TileData next, TileData previous)
        {
            nextTile = next;
            previousTile = previous;
        }
        
        /// <summary>
        /// 타일 하이라이트 (선택, 공격 대상 등)
        /// </summary>
        public void Highlight(Color color)
        {
            if (tileVisual != null)
            {
                tileVisual.SetHighlight(true, color);
            }
        }
        
        /// <summary>
        /// 하이라이트 해제
        /// </summary>
        public void ClearHighlight()
        {
            if (tileVisual != null)
            {
                tileVisual.SetHighlight(false, Color.white);
            }
        }
    }
}
