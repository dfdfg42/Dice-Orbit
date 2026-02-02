using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data;

namespace DiceOrbit.Systems.Effects
{
    public class StatusEffectManager : MonoBehaviour
    {
        private Core.Character owner;
        private Dictionary<StatusEffectType, StatusEffect> activeEffects = new Dictionary<StatusEffectType, StatusEffect>();

        public void Initialize(Core.Character character)
        {
            owner = character;
        }

        /// <summary>
        /// 상태 이상 부여
        /// </summary>
        public void AddEffect(StatusEffectType type, int value, int duration)
        {
            if (activeEffects.ContainsKey(type))
            {
                var existing = activeEffects[type];
                if (existing.IsStackable)
                {
                    existing.AddStack(value);
                    existing.RefreshDuration(duration);
                    Debug.Log($"[Status] {type} stacked to {existing.Value}, duration refreshed to {duration}");
                }
                else
                {
                    existing.RefreshDuration(duration); // 비중첩은 지속시간만 갱신 (선택사항)
                    Debug.Log($"[Status] {type} duration refreshed to {duration}");
                }
            }
            else
            {
                var newEffect = new StatusEffect(type, value, duration, IsStackableType(type));
                activeEffects.Add(type, newEffect);
                Debug.Log($"[Status] {type} added (Value: {value}, Duration: {duration})");
            }
            
            // 즉시 적용 로직이 필요하면 추가 (예: 최대 체력 증가 등)
        }

        /// <summary>
        /// 턴 시작 시 처리 (지속시간 감소, 도트 데미지 등)
        /// </summary>
        public void OnTurnStart()
        {
            var keys = activeEffects.Keys.ToList();
            foreach (var key in keys)
            {
                var effect = activeEffects[key];
                
                // 도트 데미지/힐 처리
                ProcessDoT(effect);

                // 지속시간 감소
                if (effect.Duration > 0)
                {
                    effect.Duration--;
                    if (effect.Duration <= 0)
                    {
                        RemoveEffect(key);
                    }
                }
            }
        }

        private void ProcessDoT(StatusEffect effect)
        {
            if (owner == null) return;

            switch (effect.Type)
            {
                case StatusEffectType.Poison:
                    owner.Stats.TakeDamage(effect.Value);
                    Debug.Log($"[Status] Poison damage: {effect.Value}");
                    break;
                case StatusEffectType.Burn:
                    owner.Stats.TakeDamage(effect.Value);
                    Debug.Log($"[Status] Burn damage: {effect.Value}");
                    break;
                case StatusEffectType.Regeneration:
                    owner.Stats.Heal(effect.Value);
                    Debug.Log($"[Status] Regeneration: {effect.Value}");
                    break;
            }
        }

        public void RemoveEffect(StatusEffectType type)
        {
            if (activeEffects.ContainsKey(type))
            {
                activeEffects.Remove(type);
                Debug.Log($"[Status] {type} expired/removed");
            }
        }

        public bool HasEffect(StatusEffectType type)
        {
            return activeEffects.ContainsKey(type);
        }

        public int GetEffectValue(StatusEffectType type)
        {
            if (activeEffects.ContainsKey(type))
                return activeEffects[type].Value;
            return 0;
        }

        // 스택형인지 판단 (설정)
        private bool IsStackableType(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison:
                case StatusEffectType.Burn:
                    return true;
                default:
                    return false; // 나머지는 기본적으로 비중첩 (갱신)
            }
        }
    }
}
