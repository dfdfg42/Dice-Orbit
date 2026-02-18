using System.Collections;
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
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private Vector3 spriteOffset = Vector3.zero;

        [Header("Animation Sprites")]
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite moveSprite;
        [SerializeField] private Sprite damageSprite;
        [SerializeField] private Sprite skillSprite;

        [Header("Animation Settings")]
        [SerializeField] private float damageDisplayTime = 0.4f;

        [Header("Highlight")]
        [SerializeField] private Color normalColor    = Color.white;
        [SerializeField] private Color highlightColor = Color.yellow;

        private SpriteRenderer spriteRenderer;
        private Camera mainCamera;
        private Coroutine timedReturnCoroutine;

        // ─────────────────────────────────────────────
        // 초기화
        // ─────────────────────────────────────────────
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        private void Start()
        {
            mainCamera = Camera.main;
            spriteRenderer.color = normalColor;
            PlayIdle();
        }

        private void LateUpdate()
        {
            if (billboardToCamera && mainCamera != null)
                transform.rotation = mainCamera.transform.rotation;
        }

        // ─────────────────────────────────────────────
        // 애니메이션 상태
        // ─────────────────────────────────────────────

        /// <summary>Idle 스프라이트로 전환</summary>
        public void PlayIdle()
        {
            SetSprite(idleSprite);
        }

        /// <summary>Move 스프라이트로 전환 (한 칸 이동 시작 시 호출)</summary>
        public void PlayMove()
        {
            SetSprite(moveSprite != null ? moveSprite : idleSprite);
        }

        /// <summary>Damage 스프라이트 표시 후 Idle 복귀</summary>
        public void PlayDamage()
        {
            if (damageSprite == null) return;
            StopTimedReturn();
            SetSprite(damageSprite);
            timedReturnCoroutine = StartCoroutine(ReturnToIdleAfter(damageDisplayTime));
        }

        /// <summary>Skill 스프라이트 표시 후 Idle 복귀</summary>
        public void PlaySkill()
        {
            if (skillSprite == null) return;
            StopTimedReturn();
            SetSprite(skillSprite);
        }

        // ─────────────────────────────────────────────
        // 기존 API (호환성 유지)
        // ─────────────────────────────────────────────

        /// <summary>런타임에 Preset 스프라이트를 일괄 설정</summary>
        public void SetAnimationSprites(Sprite idle, Sprite move, Sprite damage, Sprite skill)
        {
            if (idle   != null) idleSprite   = idle;
            if (move   != null) moveSprite   = move;
            if (damage != null) damageSprite = damage;
            if (skill  != null) skillSprite  = skill;
        }

        public void SetSprite(Sprite sprite)
        {
            if (sprite == null || spriteRenderer == null) return;
            spriteRenderer.sprite = sprite;
        }

        public void SetHighlight(bool highlighted)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = highlighted ? highlightColor : normalColor;
        }

        public void SetColor(Color color)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = color;
        }

        public void SetScale(float scale)
        {
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        // ─────────────────────────────────────────────
        // 내부
        // ─────────────────────────────────────────────

        private void StopTimedReturn()
        {
            if (timedReturnCoroutine != null)
            {
                StopCoroutine(timedReturnCoroutine);
                timedReturnCoroutine = null;
            }
        }

        private IEnumerator ReturnToIdleAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayIdle();
            timedReturnCoroutine = null;
        }
    }
}
