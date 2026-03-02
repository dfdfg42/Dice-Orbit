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
        private Unit owner;

        // Restore activeEffects
        private Dictionary<EffectType, StatusEffect> activeEffects = new Dictionary<EffectType, StatusEffect>();

        public int Priority => 10;

        public void Initialize(Unit unit)
        {
            owner = unit;
        }

        public void AddEffect(EffectType type, int value, int duration)
        {
            if (activeEffects.ContainsKey(type))
            {
                var existing = activeEffects[type];
                existing.AddStack(value); 
                existing.RefreshDuration(duration);
            }
            else
            {
                var newEffect = CreateEffect(type, value, duration);
                newEffect.SetOwner(owner); 
                activeEffects.Add(type, newEffect);
            }
            string name = "Unknown";
            if (owner is Character c) name = c.Stats.CharacterName;
            else if (owner is Monster m) name = m.Stats.MonsterName;

            Debug.Log($"[Status] Added {type} to {name}");
        }

        public void RemoveEffect(EffectType type)
        {
             if (activeEffects.ContainsKey(type))
            {
                activeEffects.Remove(type);
            }
        }

        
        public int GetEffectValue(EffectType type)
        {
            if (activeEffects.TryGetValue(type, out var effect) && effect != null)
            {
                return effect.Value;
            }
            return 0;
        }

        public bool HasEffect(EffectType type)
        {
            return activeEffects.ContainsKey(type);
        }

        // ICombatReactor Implementation
        public void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 각 효과의 반응 로직 실행 (StatusEffect가 스스로 Duration 관리)
            foreach (var effect in activeEffects.Values.ToList())
            {
                effect.OnReact(trigger, context);
            }

            // 턴 시작 시, 반응 처리 후 만료된 효과 정리
            if (context.Action.Type==ActionType.OnStartTurn && context.SourceUnit == owner)
            {
                CleanupExpiredEffects();
            }
        }

        private void CleanupExpiredEffects()
        {
            var keys = activeEffects.Keys.ToList();
            foreach (var key in keys)
            {
                var effect = activeEffects[key];
                // -1은 영구 지속이므로 제거하지 않음
                if (effect.Duration != -1 && effect.Duration <= 0)
                {
                    RemoveEffect(key);
                    Debug.Log($"[Status] {key} expired.");
                }
            }
        }

        private StatusEffect CreateEffect(EffectType type, int value, int duration)
        {
            switch (type)
            {
                case EffectType.BuffAttack:
                    return new BuffAttackStatus(value, duration);

                // 추후 BuffDefense, Dot 등 추가

                default:
                    // 기본(아직 구현 안된 타입)은 StatusEffect 사용
                    // (단, 기본 StatusEffect는 특별한 로직 없이 지속시간만 깎임)
                    return new StatusEffect(type, value, duration);
            }
        }
    }
}

