using UnityEngine;
using System.Collections.Generic;

namespace DiceOrbit.Core.Pipeline
{
    public enum ActionType
    {
        Attack,
        Heal,
        Utility, // Buff, Debuff only, Move, etc.
        None,
        Skill,
        Move,
        OnArrive,
        OnTreaverse,
    }

    /// <summary>
    /// 효과 정보 구조체 (버프/디버프 등)
    /// </summary>
    public struct ActionEffectInfo
    {
        public DiceOrbit.Data.EffectType Type;
        public int Value;
        public int Duration;

        public ActionEffectInfo(DiceOrbit.Data.EffectType type, int value, int duration)
        {
            Type = type;
            Value = value;
            Duration = duration;
        }
    }

    /// <summary>
    /// 전투에서 발생하는 하나의 '행위' 단위
    /// </summary>
    public class CombatAction
    {
        public string Name;
        public ActionType Type;
        public float BaseValue; // 기본 데미지 또는 힐량

        public bool IgnoreDefense;
        public bool IsCritical;

        // 확장 태그 (예: "Fire", "Melee", "Projectile") - 패시브 조건 체크용
        public HashSet<string> Tags = new HashSet<string>();

        // 적용할 상태이상 목록
        public List<ActionEffectInfo> Effects = new List<ActionEffectInfo>();

        public CombatAction(string name, ActionType type, float baseValue)
        {
            Name = name;
            Type = type;
            BaseValue = baseValue;
        }

        public void AddTag(string tag)
        {
            Tags.Add(tag);
        }

        public bool HasTag(string tag)
        {
            return Tags.Contains(tag);
        }

        public void AddEffect(DiceOrbit.Data.EffectType type, int value, int duration)
        {
            Effects.Add(new ActionEffectInfo(type, value, duration));
        }
    }
}
