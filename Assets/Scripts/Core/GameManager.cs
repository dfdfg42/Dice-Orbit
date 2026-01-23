using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 게임 매니저 - 전체 게임 초기화 및 관리
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Managers")]
        [SerializeField] private OrbitManager orbitManager;
        [SerializeField] private CenterZone centerZone;
        
        [Header("Camera")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float cameraHeight = 20f;
        [SerializeField] private float cameraAngle = 60f;
        
        private static GameManager instance;
        public static GameManager Instance => instance;
        
        private void Awake()
        {
            // 싱글톤 설정
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            // 카메라 설정
            SetupCamera();
        }
        
        private void Start()
        {
            InitializeGame();
        }
        
        /// <summary>
        /// 게임 초기화
        /// </summary>
        private void InitializeGame()
        {
            Debug.Log("Dice Orbit - Game Initialized");
            
            // OrbitManager 자동 찾기
            if (orbitManager == null)
            {
                orbitManager = Object.FindAnyObjectByType<OrbitManager>();
            }
            
            // CenterZone 자동 찾기
            if (centerZone == null)
            {
                centerZone = Object.FindAnyObjectByType<CenterZone>();
            }
            
            if (orbitManager != null)
            {
                Debug.Log($"Orbit generated with {orbitManager.TileCount} tiles");
            }
        }
        
        /// <summary>
        /// 카메라 설정 (Top-down view)
        /// </summary>
        private void SetupCamera()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            
            if (mainCamera != null)
            {
                // 위에서 내려다보는 각도로 설정
                mainCamera.transform.position = new Vector3(0, cameraHeight, -cameraHeight * 0.5f);
                mainCamera.transform.rotation = Quaternion.Euler(cameraAngle, 0, 0);
            }
        }
        
        /// <summary>
        /// 궤도 매니저 접근
        /// </summary>
        public OrbitManager GetOrbitManager()
        {
            return orbitManager;
        }
        
        /// <summary>
        /// 중앙 구역 접근
        /// </summary>
        public CenterZone GetCenterZone()
        {
            return centerZone;
        }
    }
}
