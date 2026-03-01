using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 타일 위에 떠 있는 공격 의도 말풍선 UI
    /// </summary>
    public class FloatingIntentUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image bgImage;
        [SerializeField] private Image iconImage;

        [Header("Settings")]
        [Tooltip("타일에서 얼마나 위로 띄울지")]
        [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, 0);

        private Transform targetTransform;
        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            // 대상의 위치를 따라가면서 빌보드 기능 수행 (카메라 방향 응시)
            if (targetTransform != null)
            {
                transform.position = targetTransform.position + offset;
            }

            if (mainCamera != null)
            {
                transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                                 mainCamera.transform.rotation * Vector3.up);
            }
        }

        /// <summary>
        /// 의도와 대상을 설정하고 말풍선을 활성화
        /// </summary>
        public void Setup(Transform target, Sprite icon, Color intentColor)
        {
            targetTransform = target;

            if (iconImage != null)
            {
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    // 아이콘이 없으면 기본 이미지에 색상만 입힘
                    iconImage.color = intentColor;
                }
            }

            // 초기 위치 업데이트
            if (targetTransform != null)
            {
                transform.position = targetTransform.position + offset;
            }
        }
    }
}
