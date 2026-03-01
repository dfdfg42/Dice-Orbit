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
        private static DamagePopup configuredPrefab;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI damageText;
        
        [Header("Animation")]
        [SerializeField] private float moveSpeed = 2f;
        //[SerializeField] private float fadeSpeed = 1f; //fadespeed 옵션을 현재 사용하고 있지 않아 주석 처리했습니다.
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
            if (configuredPrefab == null)
            {
                return CreateFallback(damage, worldPosition, isCritical);
            }
            
            GameObject instance = Instantiate(configuredPrefab.gameObject, worldPosition, Quaternion.identity);
            DamagePopup popup = instance.GetComponent<DamagePopup>();
            
            if (popup != null)
            {
                popup.Show(damage, worldPosition, isCritical);
            }
            
            return popup;
        }

        public static void ConfigurePrefab(DamagePopup prefab)
        {
            configuredPrefab = prefab;
        }

        private static DamagePopup CreateFallback(int damage, Vector3 worldPosition, bool isCritical)
        {
            Debug.Log("DamagePopup prefab not configured. Using fallback popup.");

            var go = new GameObject("DamagePopup_Fallback");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasGroup>();

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120f, 40f);
            rect.localScale = Vector3.one * 0.01f;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36;
            tmp.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }

            var popup = go.AddComponent<DamagePopup>();
            popup.damageText = tmp;
            popup.Show(damage, worldPosition, isCritical);
            return popup;
        }
    }
}
