using UnityEngine;
using DiceOrbit.Data;
using DiceOrbit.Systems.Effects;
using System.Collections.Generic;

namespace DiceOrbit.Systems
{
    /// <summary>
    /// 효과 관리 시스템
    /// </summary>
    public class EffectManager
    {
        private static Dictionary<EffectType, IEffect> effectRegistry = new Dictionary<EffectType, IEffect>();
        
        static EffectManager()
        {
            // 효과 등록
            RegisterEffect(EffectType.Damage, new DamageEffect());
            RegisterEffect(EffectType.Heal, new HealEffect());
            RegisterEffect(EffectType.BuffAttack, new BuffAttackEffect());
            RegisterEffect(EffectType.BuffDefense, new BuffDefenseEffect());
        }
        
        /// <summary>
        /// 효과 등록
        /// </summary>
        public static void RegisterEffect(EffectType type, IEffect effect)
        {
            effectRegistry[type] = effect;
        }
        
        /// <summary>
        /// 효과 적용 (캐릭터)
        /// </summary>
        public static void ApplyEffect(EffectData effectData, Core.Character target)
        {
            if (!effectRegistry.ContainsKey(effectData.Type))
            {
                Debug.LogWarning($"Effect {effectData.Type} not registered!");
                return;
            }
            
            var effect = effectRegistry[effectData.Type];
            effect.Apply(target, effectData.Value);
        }
        
        /// <summary>
        /// 효과 적용 (몬스터)
        /// </summary>
        public static void ApplyEffect(EffectData effectData, Core.Monster target)
        {
            if (!effectRegistry.ContainsKey(effectData.Type))
            {
                Debug.LogWarning($"Effect {effectData.Type} not registered!");
                return;
            }
            
            var effect = effectRegistry[effectData.Type];
            effect.Apply(target, effectData.Value);
        }
        
        /// <summary>
        /// 여러 효과 적용 (캐릭터)
        /// </summary>
        public static void ApplyEffects(List<EffectData> effects, Core.Character target)
        {
            foreach (var effect in effects)
            {
                ApplyEffect(effect, target);
            }
        }
        
        /// <summary>
        /// 여러 효과 적용 (몬스터)
        /// </summary>
        public static void ApplyEffects(List<EffectData> effects, Core.Monster target)
        {
            foreach (var effect in effects)
            {
                ApplyEffect(effect, target);
            }
        }
    }
}
