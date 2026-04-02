using System.Collections;
using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 캐릭터 비주얼 컨트롤러 (Animator 기반)
    /// 3D 공간에서 2D 스프라이트 표시 (Billboard 방식)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public class CharacterSpriteVisual : MonoBehaviour
    {
        [Header("Sprite Settings")]
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private Vector3 spriteOffset = Vector3.zero;

        [Header("Legacy Sprite Fields (Migration)")]
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite moveSprite;
        [SerializeField] private Sprite damageSprite;
        [SerializeField] private Sprite skillSprite;

        [Header("Animator")]
        [SerializeField] private Animator animator;
        [SerializeField] private string movingBool = "IsMoving";
        [SerializeField] private string aimingBool = "IsAiming";
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private string deadBool = "IsDead";

        [Header("Highlight")]
        [SerializeField] private Color normalColor    = Color.white;
        [SerializeField] private Color highlightColor = Color.yellow;

        private SpriteRenderer spriteRenderer;
        private Camera mainCamera;

        // ─────────────────────────────────────────────
        // 초기화
        // ─────────────────────────────────────────────
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            if (animator == null)
                animator = GetComponent<Animator>();
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
            SetBoolSafe(movingBool, false);
            SetBoolSafe(aimingBool, false);
        }

        /// <summary>Move 스프라이트로 전환 (한 칸 이동 시작 시 호출)</summary>
        public void PlayMove()
        {
            SetBoolSafe(movingBool, true);
            SetBoolSafe(aimingBool, false);
        }

        /// <summary>피격 애니메이션 트리거</summary>
        public void PlayDamage()
        {
            SetBoolSafe(movingBool, false);
            SetTriggerSafe(hitTrigger);
        }

        /// <summary>공격 애니메이션 트리거</summary>
        public void PlaySkill()
        {
            SetBoolSafe(aimingBool, false);
            SetBoolSafe(movingBool, false);
            SetTriggerSafe(attackTrigger);
        }

        public void PlayDeath()
        {
            SetBoolSafe(movingBool, false);
            SetBoolSafe(aimingBool, false);
            SetBoolSafe(deadBool, true);
            SetTriggerSafe(deathTrigger);
        }

        public void SetAiming(bool aiming)
        {
            SetBoolSafe(aimingBool, aiming);
            if (aiming)
            {
                SetBoolSafe(movingBool, false);
            }
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

        private void SetTriggerSafe(string trigger)
        {
            if (animator == null || string.IsNullOrWhiteSpace(trigger)) return;
            animator.SetTrigger(trigger);
        }

        private void SetBoolSafe(string param, bool value)
        {
            if (animator == null || string.IsNullOrWhiteSpace(param)) return;
            animator.SetBool(param, value);
        }

        private IEnumerator ReturnToIdleAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayIdle();
        }

        private void OnValidate()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }
    }
}
