using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Monsters
{
    /// <summary>
    /// 몬스터 사망 시 실행되는 효과의 기본 클래스
    /// PassiveAbility와 동일한 패턴을 따릅니다.
    /// </summary>
    [System.Serializable]
    public abstract class DeathEffect
    {
        [SerializeField] protected string effectName = "";
        [SerializeField] [TextArea] protected string description = "";
        
        public virtual string EffectName => effectName;
        public virtual string Description => description;
        
        /// <summary>
        /// 사망 효과 실행
        /// </summary>
        /// <param name="deadMonster">죽은 몬스터</param>
        public abstract void Execute(Monster deadMonster);
    }
}
