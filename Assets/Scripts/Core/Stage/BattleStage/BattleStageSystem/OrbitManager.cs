using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;
using DiceOrbit.Visuals;
using System.Collections.Generic;
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
        [SerializeField] private Material attackIntentMaterial;
        
        [Header("Visual Settings")]
        [SerializeField] private float tileWidth = 1.5f;
        [SerializeField] private float tileHeight = 0.2f;

    [Header("Character Formation")]
    [SerializeField] private float formationSpacing = 1.3f;
    [SerializeField] private float formationYOffset = 1.5f;
    [SerializeField] private float formationForwardOffset = 1.0f;
        
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
                
                // 모든 타일은 일반 타입 — 0번(시작) 타일도 겉모습은 일반이고, 치유 속성만 붙는다 (2026-08-28 결정)
                TileType tileType = TileType.Normal;
                
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
                if (index == 0)
                {
                    // 시작(0번) 타일 = 치유 속성 (지나가면 소량 회복, 기획 REV05). 타입은 일반 — 효과만 붙는다.
                    tileData.AddAttribute(new StartHealTile(TileAttributeType.ScoutHeal, 5, -1));
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
            var highlightField = typeof(TileVisual).GetField("highlightMaterial",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (normalField != null) normalField.SetValue(visual, normalMaterial);
            if (levelUpField != null) levelUpField.SetValue(visual, levelUpMaterial);
            if (specialField != null) specialField.SetValue(visual, specialMaterial);
            if (highlightField != null) highlightField.SetValue(visual, attackIntentMaterial);
            
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
        
        /// <summary>
        /// 이 캐릭터가 주사위 눈 diceValue로 이동할 때 밟는 타일들(순서대로, 도착 타일 포함)을 path에 채운다.
        /// 이동 버프/디버프를 반영한 유효 걸음 수를 쓴다. 이동 루틴·행동 예고·지나간 구역 수집이 공유하는 단일 출처.
        /// 타일 연결이 끊겨 있으면 거기까지만 담는다.
        /// </summary>
        public static void BuildMovePath(Character character, int diceValue, List<TileData> path)
        {
            path.Clear();
            if (character == null || character.CurrentTile == null || character.Stats == null) return;

            int steps = EffectiveSteps(character, diceValue);
            var tile = character.CurrentTile;
            for (int i = 0; i < steps; i++)
            {
                if (tile.NextTile == null) break;
                tile = tile.NextTile;
                path.Add(tile);
            }
        }

        /// <summary>주사위 눈에 이동 버프/디버프를 반영한 실제 걸음 수 (0 이상).</summary>
        public static int EffectiveSteps(Character character, int diceValue)
            => Mathf.Max(diceValue + character.Stats.MoveBuff - character.Stats.MoveDebuff, 0);

        public System.Collections.IEnumerator MoveRoutine(Character character, int steps)
        {
            var tilePath = new List<TileData>();
            BuildMovePath(character, steps, tilePath);

            if (tilePath.Count < EffectiveSteps(character, steps))
                Debug.LogError($"NextTile is null at step {tilePath.Count}! Tiles may not be connected properly.");

            if (tilePath.Count > 0)
            {
                // 타일을 하나씩 이동하는 코루틴의 종료를 기다림
                yield return character.MoveStepByStep(tilePath);
            }
            else
            {
                Debug.LogWarning("No valid path found");
                yield return null;
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
        
        public List<Character> GetCharactersOnTile(TileData tile)
        {
            var characters = new List<Character>();
            foreach (var character in PartyManager.Instance?.Party)
            {
                if (character.CurrentTile == tile)
                {
                    characters.Add(character);
                }
            }
            return characters;
        }

        public void RefreshCharactersOnTile(TileData tile)
        {
            if (tile == null) return;

            var characters = GetCharactersOnTile(tile);
            if (characters == null || characters.Count == 0) return;

            characters.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

            Vector3 sideAxis = ResolveFormationSideAxis();
            float centerIndex = (characters.Count - 1) * 0.5f;
            Vector3 anchor = tile.Position + new Vector3(0f, formationYOffset, formationForwardOffset);

            for (int i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character == null) continue;

                float offset = (i - centerIndex) * formationSpacing;
                character.transform.position = anchor + sideAxis * offset;
            }
        }

        private static Vector3 ResolveFormationSideAxis()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 side = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
                if (side.sqrMagnitude > 0.0001f)
                {
                    return side;
                }
            }

            return Vector3.right;
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
