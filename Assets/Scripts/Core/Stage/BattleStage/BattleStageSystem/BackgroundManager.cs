using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Data.Waves;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 배틀 스테이지 배경 관리자 via WaveManager
    /// </summary>
    public class BackgroundManager : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        
        [Header("Settings")]
        // [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private bool isFloor = true; // 3D 바닥으로 사용할지 여부 (X축 90도 회전)
        [SerializeField] private Vector3 floorOffset = new Vector3(0, -1f, 0); // 바닥 높이 (궤도보다 낮게)
        [SerializeField] private float floorScale = 5f; // 바닥 크기 배율
        [SerializeField] private int sortingOrder = -100; // 가장 뒤에 그리기

        private void Start()
        {
            if (backgroundRenderer == null)
            {
                backgroundRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            // 초기 설정
            if (backgroundRenderer != null)
            {
                backgroundRenderer.sortingOrder = sortingOrder;

                if (isFloor)
                {
                    backgroundRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    backgroundRenderer.transform.position = floorOffset;
                    backgroundRenderer.transform.localScale = new Vector3(floorScale, floorScale, 1f);
                }
            }

            // WaveManager 이벤트 구독
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnWaveStart += OnWaveStart;
                
                // 이미 웨이브가 진행 중이라면 초기화
                if (WaveManager.Instance.IsWaveActive)
                {
                    OnWaveStart(WaveManager.Instance.CurrentWave);
                }
            }
        }

        private void OnDestroy()
        {
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnWaveStart -= OnWaveStart;
            }
        }

        private void OnWaveStart(int waveIndex)
        {
            var waveDef = WaveManager.Instance.GetWaveDefinition(waveIndex);
            if (waveDef != null && waveDef.BackgroundSprite != null)
            {
                SetBackground(waveDef.BackgroundSprite);
            }
        }

        public void SetBackground(Sprite sprite)
        {
            if (backgroundRenderer == null) return;

            // TODO: Fade Effect if needed
            backgroundRenderer.sprite = sprite;
            
            // Fit to screen height logic could be added here if needed
            // For now, assume sprite is large enough or pre-scaled
        }
    }
}
