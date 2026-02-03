using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 데미지 효과
    /// </summary>
    public class DamageEffect : IEffect
    {
        public string EffectName => "Damage";
        public string Description => "Deal damage to target";
        
        public void Apply(Core.Character target, int value)
        {
            if (CombatPipeline.Instance != null)
            {
                var action = new CombatAction(EffectName, ActionType.Attack, value);
                action.AddTag("Effect");
                var context = new CombatContext(null, target, action);
                CombatPipeline.Instance.Process(context);
            }
            else
            {
                target.Stats.TakeDamage(value);
            }
            Debug.Log($"{EffectName}: {target.Stats.CharacterName} takes {value} damage");
        }
        
        public void Apply(Core.Monster target, int value)
        {
            if (CombatPipeline.Instance != null)
            {
                var action = new CombatAction(EffectName, ActionType.Attack, value);
                action.AddTag("Effect");
                var context = new CombatContext(null, target, action);
                CombatPipeline.Instance.Process(context);
            }
            else
            {
                target.Stats.TakeDamage(value);
            }
            Debug.Log($"{EffectName}: {target.Stats.MonsterName} takes {value} damage");
        }
    }
    
    /// <summary>
    /// 회복 효과
    /// </summary>
    public class HealEffect : IEffect
    {
        public string EffectName => "Heal";
        public string Description => "Restore HP to target";
        
        public void Apply(Core.Character target, int value)
        {
            if (CombatPipeline.Instance != null)
            {
                var action = new CombatAction(EffectName, ActionType.Heal, value);
                action.AddTag("Effect");
                var context = new CombatContext(null, target, action);
                CombatPipeline.Instance.Process(context);
            }
            else
            {
                target.Stats.Heal(value);
            }
            Debug.Log($"{EffectName}: {target.Stats.CharacterName} heals {value} HP");
        }
        
        public void Apply(Core.Monster target, int value)
        {
            if (CombatPipeline.Instance != null)
            {
                var action = new CombatAction(EffectName, ActionType.Heal, value);
                action.AddTag("Effect");
                var context = new CombatContext(null, target, action);
                CombatPipeline.Instance.Process(context);
            }
            else
            {
                target.Stats.Heal(value);
            }
            Debug.Log($"{EffectName}: {target.Stats.MonsterName} heals {value} HP");
        }
    }
    
    /// <summary>
    /// 공격력 버프 효과
    /// </summary>
    public class BuffAttackEffect : IEffect
    {
        public string EffectName => "Attack Buff";
        public string Description => "Increase attack power";
        
        public void Apply(Core.Character target, int value)
        {
            target.Stats.Attack += value;
            Debug.Log($"{EffectName}: {target.Stats.CharacterName} gains +{value} ATK");
        }
        
        public void Apply(Core.Monster target, int value)
        {
            target.Stats.Attack += value;
            Debug.Log($"{EffectName}: {target.Stats.MonsterName} gains +{value} ATK");
        }
    }
    
    /// <summary>
    /// 방어력 버프 효과
    /// </summary>
    public class BuffDefenseEffect : IEffect
    {
        public string EffectName => "Defense Buff";
        public string Description => "Increase defense";
        
        public void Apply(Core.Character target, int value)
        {
            target.Stats.Defense += value;
            Debug.Log($"{EffectName}: {target.Stats.CharacterName} gains +{value} DEF");
        }
        
        public void Apply(Core.Monster target, int value)
        {
            target.Stats.Defense += value;
            Debug.Log($"{EffectName}: {target.Stats.MonsterName} gains +{value} DEF");
        }
    }
}
