using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Skills.Modules
{
    /// <summary>
    /// 모듈형 스킬 동작의 기본 클래스
    /// </summary>
    public abstract class SkillActionModule : ScriptableObject
    {
        [TextArea]
        public string Description;
        
        /// <summary>
        /// 스킬 실행 로직
        /// </summary>
        /// <param name="source">스킬 사용자</param>
        /// <param name="target">타겟 (GameObject)</param>
        /// <param name="diceValue">사용된 주사위 값</param>
        public abstract void Execute(Character source, GameObject target, int diceValue);
    }
}
