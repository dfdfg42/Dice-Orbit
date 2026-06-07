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
        [SerializeField] private string moveStateName = ""; // 한 칸 이동 시 처음부터 재생할 Move 상태 이름 (비우면 IsMoving bool만 사용)
        [SerializeField] private string movingBool = "IsMoving";
        [SerializeField] private string aimingBool = "IsAiming";
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private string deadBool = "IsDead";

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

        /// <summary>
        /// 한 칸 이동용 Move 애니메이션을 '처음(0프레임)부터' 재생하고, 그 재생 길이(초)를 반환한다.
        /// moveStateName 이 지정돼 있고 해당 상태가 존재하면 animator.Play 로 강제 재시작한다.
        /// 반환값(>0)을 한 칸 이동 시간과 동기화하면 애니메이션이 끝난 뒤에 다음 칸으로 넘어간다.
        /// (지정 안 됐거나 상태가 없으면 IsMoving bool 만 켜고 0 을 반환 → 호출부가 기본 이동 시간을 사용)
        /// </summary>
        public float PlayMoveStep()
        {
            SetBoolSafe(aimingBool, false);
            SetBoolSafe(movingBool, true);

            if (animator != null && !string.IsNullOrWhiteSpace(moveStateName))
            {
                int hash = Animator.StringToHash(moveStateName);
                if (animator.HasState(0, hash))
                {
                    animator.Play(hash, 0, 0f);
                    animator.Update(0f); // 상태를 즉시 진입시켜 길이를 바로 읽을 수 있게 한다
                    return animator.GetCurrentAnimatorStateInfo(0).length;
                }
            }
            return 0f;
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
