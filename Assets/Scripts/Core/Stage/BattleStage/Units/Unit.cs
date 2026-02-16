using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.UI;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 전투 유닛의 기본 클래스 (플레이어, 몬스터)
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

        // Abstract 프로퍼티 - 자식 클래스에서 반드시 구현
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
        /// 시스템 초기화 (자식 클래스에서 override)
        /// </summary>
        protected virtual void InitializeSystems()
        {
            // Systems 기본 초기화 - 자식에서 override
        }

        protected virtual void LateUpdate()
        {
            // Billboard: 항상 카메라 향하도록
            if (mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }

        /// <summary>
        /// 턴 시작 처리 (Pipeline)
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

        //Unit 리엑터는 여기서 전부 수집. 일단은 패시브에서만 수집하게 했음. 나중에 수정 반드시 해야 함
        //CombatPipeline의 CollectReactors도 수정해야 함
        public void CollectReactors(System.Collections.Generic.List<DiceOrbit.Core.Pipeline.ICombatReactor> reactors)
        {
            foreach (var passiveList in passives.ActivePassives.Values)
            {
                foreach (var passive in passiveList)
                {
                    if (passive is ICombatReactor reactor)
                    {
                        reactors.Add(passive);
                    }
                }
            }
        }
        /// <summary>
        /// 데미지 처리
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

        public virtual void Heal(int value)
        {
            int hpBefore = Stats.CurrentHP;
            Stats.Heal(value);
        }

        /// <summary>
        /// 마우스 호버 시 하이라이트
        /// </summary>
        protected virtual void OnMouseEnter()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = highlightColor;
            }
        }

        /// <summary>
        /// 마우스 벗어날 때 원래 색상
        /// </summary>
        protected virtual void OnMouseExit()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }

        /// <summary>
        /// 스프라이트와 색상 업데이트
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

        // Unit 제네릭에서 항상 UnitStats 반환
        public override UnitStats Stats => stat;
    }
}



