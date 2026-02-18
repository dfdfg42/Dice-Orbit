using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;
using DiceOrbit.Visuals;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 궤도 시스템 관리자 - 원형 타일 배치 및 관리
    /// </summary>
    public class OrbitManager : MonoBehaviour
    {
        [Header("Orbit Settings")]
        [SerializeField] private int tileCount = 20;
        [SerializeField] private float orbitRadius = 8f;
        [SerializeField] private bool clockwise = true;
        
        [Header("Tile Prefab")]
        [SerializeField] private GameObject tilePrefab;
        
        [Header("Materials")]
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material levelUpMaterial;
        [SerializeField] private Material specialMaterial;
        
        [Header("Visual Settings")]
        [SerializeField] private float tileWidth = 1.5f;
        [SerializeField] private float tileHeight = 0.2f;
        
        // Runtime Data
        private List<TileData> tiles = new List<TileData>();
        private Transform tilesParent;
        
        public List<TileData> Tiles => tiles;
        public int TileCount => tileCount;
        public TileData LevelUpTile => tiles.Count > 0 ? tiles[0] : null;
        
        private void Awake()
        {
            // 타일 부모 오브젝트 생성
            tilesParent = new GameObject("Tiles").transform;
            tilesParent.SetParent(transform);
        }
        
        private void Start()
        {
            GenerateOrbit();
        }
        
        /// <summary>
        /// 궤도 타일 생성
        /// </summary>
        public void GenerateOrbit()
        {
            ClearOrbit();
            
            float angleStep = 360f / tileCount;
            
            for (int i = 0; i < tileCount; i++)
            {
                // 각도 계산 (라디안)
                float angle = i * angleStep * Mathf.Deg2Rad;
                
                // 원형 배치 좌표
                float x = Mathf.Cos(angle) * orbitRadius;
                float z = Mathf.Sin(angle) * orbitRadius;
                Vector3 position = new Vector3(x, 0, z);
                
                // 타일 타입 결정 (0번은 레벨업 타일)
                TileType tileType = (i == 0) ? TileType.LevelUp : TileType.Normal;
                
                // 타일 생성
                GameObject tileObj = CreateTile(position, i, tileType);
                tileObj.name = $"Tile_{i}_{tileType}";
                
                // 회전 (타일이 중심을 향하도록)
                tileObj.transform.LookAt(Vector3.zero);
                //tileObj.transform.Rotate(0, 0, 0); //회전 그런데 원래 누워있는 프리팹이라 그대로 회전필요없음
            }
            
            // 타일 연결 설정
            ConnectTiles();
        }
        
        /// <summary>
        /// 타일 생성
        /// </summary>
        private GameObject CreateTile(Vector3 position, int index, TileType type)
        {
            GameObject tileObj;
            
            // Prefab이 있으면 사용, 없으면 새로 생성
            if (tilePrefab != null)
            {
                tileObj = Instantiate(tilePrefab, position, Quaternion.identity, tilesParent);
            }
            else
            {
                tileObj = new GameObject();
                tileObj.transform.SetParent(tilesParent);
                tileObj.transform.position = position;
            }
            
            // TileData 컴포넌트 추가 또는 가져오기
            TileData tileData = tileObj.GetComponent<TileData>();
            if (tileData == null)
            {
                tileData = tileObj.AddComponent<TileData>();
                if (type == TileType.LevelUp)
                {
                    var levelUpAttribute = new treavse_LevelUP(TileAttributeType.LevelUp, 1, -1);
                    tileData.AddAttribute(levelUpAttribute);
                }
            }
            
            // TileVisual 컴포넌트 추가 또는 가져오기
            TileVisual tileVisual = tileObj.GetComponent<TileVisual>();
            if (tileVisual == null)
            {
                tileVisual = tileObj.AddComponent<TileVisual>();
            }
            
            // 머티리얼 설정
            SetupTileMaterials(tileVisual, type);
            
            // 타일 크기 설정
            tileVisual.SetTileSize(tileWidth, tileHeight);
            
            // 초기화
            tileData.Initialize(index, type, tileVisual);
            
            // 리스트에 추가
            tiles.Add(tileData);
            
            return tileObj;
        }
        
        /// <summary>
        /// 타일 머티리얼 설정
        /// </summary>
        private void SetupTileMaterials(TileVisual visual, TileType type)
        {
            // TileVisual의 inspector에서 설정할 수 있도록 reflection 사용
            var normalField = typeof(TileVisual).GetField("normalMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var levelUpField = typeof(TileVisual).GetField("levelUpMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var specialField = typeof(TileVisual).GetField("specialMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (normalField != null) normalField.SetValue(visual, normalMaterial);
            if (levelUpField != null) levelUpField.SetValue(visual, levelUpMaterial);
            if (specialField != null) specialField.SetValue(visual, specialMaterial);
            
            visual.SetTileType(type);
        }
        
        /// <summary>
        /// 타일 연결 (순환 링크드 리스트)
        /// </summary>
        private void ConnectTiles()
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                int nextIndex = (i + 1) % tiles.Count;
                int prevIndex = (i - 1 + tiles.Count) % tiles.Count;
                
                if (clockwise)
                {
                    tiles[i].SetConnections(tiles[nextIndex], tiles[prevIndex]);
                }
                else
                {
                    tiles[i].SetConnections(tiles[prevIndex], tiles[nextIndex]);
                }
            }
        }
        
        public void Move(Character character, int steps)
        {
            var currentTile = character.CurrentTile;
            // 타일 경로 계산
            var tilePath = new List<TileData>();
            TileData currentStep = currentTile;

            for (int i = 0; i < steps; i++)
            {
                if (currentStep.NextTile == null)
                {
                    Debug.LogError($"NextTile is null at step {i}! Tiles may not be connected properly.");
                    break;
                }
                currentStep = currentStep.NextTile;
                tilePath.Add(currentStep);
            }

            if (tilePath.Count > 0)
            {
                // 마지막 타일로 currentTile 업데이트
                currentTile = tilePath[tilePath.Count - 1];

                // 타일을 하나씩 이동
                StartCoroutine(character.MoveStepByStep(tilePath));
            }
            else
            {
                Debug.LogWarning("No valid path found");
            }
        }
        /// <summary>
        /// 궤도 초기화
        /// </summary>
        public void ClearOrbit()
        {
            tiles.Clear();
            
            if (tilesParent != null)
            {
                foreach (Transform child in tilesParent)
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
        }
        
        /// <summary>
        /// 특정 인덱스의 타일 가져오기
        /// </summary>
        public TileData GetTile(int index)
        {
            if (index >= 0 && index < tiles.Count)
                return tiles[index];
            return null;
        }
        
        /// <summary>
        /// 시작점으로부터 N칸 이동한 타일 가져오기
        /// </summary>
        public TileData GetTileFromStart(int steps)
        {
            if (tiles.Count == 0) return null;
            
            int index = steps % tiles.Count;
            if (index < 0) index += tiles.Count;
            
            return tiles[index];
        }

        // Editor에서 실시간 갱신을 위한 메서드
        private void OnValidate()
        {
            if (Application.isPlaying && tilesParent != null)
            {
                GenerateOrbit();
            }
        }
    }
}
