using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 2D 스프라이트 기반 캐릭터 비주얼
    /// 3D 공간에서 2D 스프라이트 표시 (Billboard 방식)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterSpriteVisual : MonoBehaviour
    {
        [Header("Sprite Settings")]
        [SerializeField] private Sprite characterSprite;
        [SerializeField] private bool billboardToCamera = true; // 항상 카메라를 향함
        [SerializeField] private Vector3 spriteOffset = Vector3.zero;
        
        [Header("Highlight")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color highlightColor = Color.yellow;
        
        private SpriteRenderer spriteRenderer;
        private Camera mainCamera;
        
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }
        
        private void Start()
        {
            mainCamera = Camera.main;
            
            // 스프라이트 설정
            if (characterSprite != null)
            {
                spriteRenderer.sprite = characterSprite;
            }
            
            spriteRenderer.color = normalColor;
            
            // 크기 조정 로직 삭제 (Character/Monster 클래스에서 제어)
            // transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        }
        
        private void LateUpdate()
        {
            // Billboard: 항상 카메라를 향하도록
            if (billboardToCamera && mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }
        
        /// <summary>
        /// 스프라이트 변경
        /// </summary>
        public void SetSprite(Sprite sprite)
        {
            characterSprite = sprite;
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }
        
        /// <summary>
        /// 하이라이트 설정
        /// </summary>
        public void SetHighlight(bool highlighted)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = highlighted ? highlightColor : normalColor;
            }
        }
        
        /// <summary>
        /// 색상 변경
        /// </summary>
        public void SetColor(Color color)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }
        /// <summary>
        /// 크기 설정 (외부 제어)
        /// </summary>
        public void SetScale(float scale)
        {
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
