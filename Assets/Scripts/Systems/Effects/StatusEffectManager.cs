using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Systems.Effects
{
    public class StatusEffectManager : MonoBehaviour, ICombatReactor
    {
        private Character owner;
        private Dictionary<EffectType, StatusEffect> activeEffects = new Dictionary<EffectType, StatusEffect>();

        public int Priority => 10;

        public void Initialize(Character character)
        {
            owner = character;
        }

        public void AddEffect(EffectType type, int value, int duration)
        {
            if (activeEffects.ContainsKey(type))
            {
                var existing = activeEffects[type];
                existing.AddStack(value); // or refresh logic
                existing.RefreshDuration(duration);
            }
            else
            {
                var newEffect = new StatusEffect(type, value, duration);
                newEffect.SetOwner(owner);
                activeEffects.Add(type, newEffect);
            }
            Debug.Log($"[Status] Added {type} to {owner.Stats.CharacterName}");
        }

        public void RemoveEffect(EffectType type)
        {
             if (activeEffects.ContainsKey(type))
            {
                activeEffects.Remove(type);
            }
        }

        // ICombatReactor Implementation
        public void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 턴 시작 시 지속시간 관리 (별도 로직 필요하지만 여기서 처리 가능)
            if (trigger == CombatTrigger.OnTurnStart && context.SourceUnit == owner)
            {
                HandleTurnStart();
            }

            // 각 효과의 반응 로직 실행
            foreach (var effect in activeEffects.Values.ToList())
            {
                effect.ProcessReaction(trigger, context);
            }
        }

        private void HandleTurnStart()
        {
            var keys = activeEffects.Keys.ToList();
            foreach (var key in keys)
            {
                var effect = activeEffects[key];
                
                // 도트 데미지 처리 (간략화)
                if (effect.Type == EffectType.Dot)
                {
                    if (CombatPipeline.Instance != null)
                    {
                        var action = new CombatAction("DoT", Core.Pipeline.ActionType.Attack, effect.Value);
                        action.AddTag("Dot");
                        var context = new CombatContext(owner, owner, action);
                        CombatPipeline.Instance.Process(context);
                    }
                    else
                    {
                        owner.Stats.TakeDamage(effect.Value);
                    }
                    Debug.Log($"[Status] Dot Damage: {effect.Value}");
                }

                if (effect.Duration > 0)
                {
                    effect.Duration--;
                    if (effect.Duration <= 0)
                    {
                        RemoveEffect(key);
                        Debug.Log($"[Status] {key} expired.");
                    }
                }
            }
        }
    }
}
