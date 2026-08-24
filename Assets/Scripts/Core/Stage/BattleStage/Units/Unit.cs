using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.UI;
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
            Debug.Log($"{name} Start Turn");

            var context = new Pipeline.TurnEventContext(this, this, Pipeline.EventPhase.TurnStart);
            Pipeline.CombatPipeline.Instance?.Process(context);
        }



        /// <summary>
        /// 턴 종료 처리
        /// </summary>
        public virtual void OnEndTurn()
        {
            Debug.Log($"[Unit] End Turn");

            var context = new Pipeline.TurnEventContext(this, this, Pipeline.EventPhase.TurnEnd);

            if (Pipeline.CombatPipeline.Instance != null)
            {
                Pipeline.CombatPipeline.Instance.Process(context);
            }
        }
        //패시브와 현재 상태이상에서 Reactor 수집
        public virtual void CollectReactors(System.Collections.Generic.List<DiceOrbit.Core.Pipeline.ICombatReactor> reactors)
        {
            reactors.Add(Stats);
            foreach (var passive in passives.ActivePassives)
            {
                if (passive is ICombatReactor reactor)
                {
                    reactors.Add(passive);
                }
            }
            reactors.Add(statusEffects);
        }
        /// <summary>
        /// 데미지 처리
        /// </summary>
        public virtual int TakeDamage(int damage)
        {
            int actualDamage = Stats.TakeDamage(damage);
            if (actualDamage > 0)
            {
                // 데미지 숫자는 앞선 버블(수호·협공 등)이 다 사라진 뒤에 뜬다 — 원인(패시브) 먼저, 결과(피해) 나중.
                // 버블이 없으면 즉시 뜨고, 데미지끼리는 줄 서지 않아 동시 타격은 동시에 보인다.
                int shown = actualDamage;
                UI.FloatingPopupQueue.EnqueueAfterBubbles(transform, 1.6f, pos => FloatingLabelPopup.CreateDamage(shown, pos));
            }
            return actualDamage;
        }

        public virtual void Heal(int value)
        {
            int hpBefore = Stats.CurrentHP;
            Stats.Heal(value);
        }

        protected virtual void HandleDeath()
        {
            Debug.Log($"{name} has died.");
            DiceOrbit.Visuals.VfxService.PlayOn(DiceOrbit.Visuals.VfxTags.Death, this);
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



