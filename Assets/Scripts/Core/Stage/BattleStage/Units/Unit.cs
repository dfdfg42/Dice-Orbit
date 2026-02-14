using UnityEngine;
using DiceOrbit.Data;
using DiceOrbit.UI;

namespace DiceOrbit.Core
{
    /// <summary>
    /// ���� ������ �⺻ Ŭ���� (�÷��̾�, ����)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public abstract class Unit : MonoBehaviour 
    {
        [Header("Visual")]
        [SerializeField] protected Color highlightColor = Color.yellow;
        protected SpriteRenderer spriteRenderer;
        protected Color originalColor;
        protected Camera mainCamera;

        [Header("Systems")]
        [SerializeField] protected Systems.Passives.PassiveManager passives;
        [SerializeField] protected Systems.Effects.StatusEffectManager statusEffects;

        // Abstract ������Ƽ - �ڽ� Ŭ�������� �ݵ�� ����
        public abstract UnitStats Stats { get; }

        public bool IsAlive => Stats.IsAlive;
        public Systems.Passives.PassiveManager Passives => passives;
        public Systems.Effects.StatusEffectManager StatusEffects => statusEffects;

        protected virtual void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogWarning("SpriteRenderer not found! Add SpriteRenderer component.");
            }

            mainCamera = Camera.main;
        }

        /// <summary>
        /// �ý��� �ʱ�ȭ (�ڽ� Ŭ�������� override)
        /// </summary>
        protected virtual void InitializeSystems()
        {
            // Systems �⺻ �ʱ�ȭ - �ڽĿ��� override
        }

        protected virtual void LateUpdate()
        {
            // Billboard: �׻� ī�޶� ���ϵ���
            if (mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }

        /// <summary>
        /// �� ���� ó�� (Pipeline)
        /// </summary>
        public virtual void OnStartTurn()
        {
            Debug.Log($"[Unit] Start Turn");

            var action = new Pipeline.CombatAction("Turn Start", Pipeline.ActionType.TurnStart, 0);
            var context = new Pipeline.CombatContext(this, this, action);

            if (Pipeline.CombatPipeline.Instance != null)
            {
                Pipeline.CombatPipeline.Instance.Process(context);
            }
        }

        /// <summary>
        /// ������ ó��
        /// </summary>
        public virtual void TakeDamage(int damage)
        {
            int hpBefore = Stats.CurrentHP;
            Stats.TakeDamage(damage);
            int actualDamage = Mathf.Max(0, hpBefore - Stats.CurrentHP);

            if (actualDamage > 0)
            {
                DamagePopup.Create(actualDamage, transform.position + Vector3.up * 1.6f);
            }
        }

        /// <summary>
        /// ���콺 ȣ�� �� ���̶���Ʈ
        /// </summary>
        protected virtual void OnMouseEnter()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = highlightColor;
            }
        }

        /// <summary>
        /// ���콺 ���� �� ���� ����
        /// </summary>
        protected virtual void OnMouseExit()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }

        /// <summary>
        /// ��������Ʈ�� ���� ������Ʈ
        /// </summary>
        protected void UpdateVisuals(Sprite sprite, Color spriteColor)
        {
            if (spriteRenderer == null) return;

            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
            spriteRenderer.color = spriteColor;
            originalColor = spriteRenderer.color;
        }
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public abstract class Unit<TStats> : Unit where TStats : UnitStats
    {
        [SerializeField]
        protected TStats stat;

        // Unit ���������� �׻� UnitStats
        public override UnitStats Stats => stat;
    }
}



