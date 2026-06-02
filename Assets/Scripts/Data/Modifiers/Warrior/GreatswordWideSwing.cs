using System.Linq;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.CharacterActives;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Modifiers.Warrior
{
    /// <summary>
    /// [전사 시그니처] 그레이트소드 대상 수 +1.
    /// 여러 번 장착할수록 누적 적용됨.
    /// </summary>
    [System.Serializable]
    public class GreatswordWideSwing : CharacterModifier
    {
        public override ModifierCategory Category => ModifierCategory.Signature;
        public override string ModifierName => "광역 참격";
        public override string Description => "그레이트소드 대상 수 +1";

        public override void OnRefreshSkill(Core.Pipeline.CharacterModfierContext context)
        {
            // 이 모디파이어는 전사 대검 스킬에 한해서만 시그니처 기믹을 동작시킴 (Downcasting)
            if (context is Core.Pipeline.WarriorGreatswordModifiedContext gsContext)
            {
                // 타겟을 다중 공격으로 바꿈
                gsContext.TargetCount += 1;
                gsContext.TargetType = CharacterSkillTargetType.MultiEnemy;
            }
        }

        public override string GetSkillLine(CharacterActiveSkill skill)
        {
            if (skill is not WarriorGreatswordActive) return string.Empty;
            return "[광역 참격] 대상 수 +1";
        }

        protected override void OnAttackWithActive(CombatContext context)
        {
            context.OutputValue = 1000;
            Debug.Log($"[광역 참격] 공격력 +10000");
        }
    }
}
