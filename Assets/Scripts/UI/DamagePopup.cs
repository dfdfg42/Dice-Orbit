using UnityEngine;
using TMPro;
using System.Collections;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 데미지 팝업 애니메이션
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TextMeshProUGUI damageText;
        
        [Header("Animation")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float fadeSpeed = 1f;
        [SerializeField] private float lifetime = 1.5f;
        
        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color criticalColor = Color.yellow;
        
        private CanvasGroup canvasGroup;
        private Camera mainCamera;
        
        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            mainCamera = Camera.main;
        }
        
        /// <summary>
        /// 데미지 팝업 표시
        /// </summary>
        public void Show(int damage, Vector3 worldPosition, bool isCritical = false)
        {
            // 텍스트 설정
            if (damageText != null)
            {
                damageText.text = damage.ToString();
                damageText.color = isCritical ? criticalColor : normalColor;
                
                if (isCritical)
                {
                    damageText.fontSize += 10;
                }
            }
            
            // 위치 설정
            transform.position = worldPosition;
            
            // 애니메이션 시작
            StartCoroutine(AnimatePopup());
        }
        
        /// <summary>
        /// 팝업 애니메이션
        /// </summary>
        private IEnumerator AnimatePopup()
        {
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            
            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                
                // 위로 이동
                transform.position = startPos + Vector3.up * (moveSpeed * elapsed);
                
                // 페이드 아웃
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f - (elapsed / lifetime);
                }
                
                // 카메라를 향하도록 (Billboard)
                if (mainCamera != null)
                {
                    transform.rotation = mainCamera.transform.rotation;
                }
                
                yield return null;
            }
            
            // 제거
            Destroy(gameObject);
        }
        
        /// <summary>
        /// 정적 생성 메서드
        /// </summary>
        public static DamagePopup Create(int damage, Vector3 worldPosition, bool isCritical = false)
        {
            // 프리팹에서 생성 (Resources 폴더 사용)
            GameObject prefab = Resources.Load<GameObject>("Prefabs/DamagePopup");
            
            if (prefab == null)
            {
                Debug.LogWarning("DamagePopup prefab not found in Resources/Prefabs/");
                return null;
            }
            
            GameObject instance = Instantiate(prefab, worldPosition, Quaternion.identity);
            DamagePopup popup = instance.GetComponent<DamagePopup>();
            
            if (popup != null)
            {
                popup.Show(damage, worldPosition, isCritical);
            }
            
            return popup;
        }
    }
}
