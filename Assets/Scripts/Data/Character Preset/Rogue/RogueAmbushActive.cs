using DiceOrbit.Core;
using DiceOrbit.Data.Skills;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [급습] 낮은 눈으로 짧게 파고든 턴에, 자기 구역의 주인에게 급소 일격을 넣는다.
    /// 제자리형이라 낮은 눈 게이트(3 이하)와 한 몸이며, 협공(같은 구역 아군)이면 더 깊이 파고든다.
    /// </summary>
    [System.Serializable]
    public class RogueAmbushActive : CharacterActiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("피해 = 공격력 x 배율")]
        [SerializeField] private float multiplier = 2f;

        // 협공 보너스를 여기에 두지 않는 이유: 같은 조건을 패시브 「협공」이 이미 보상하고 있어
        // 양쪽에 넣으면 파이프라인에서 곱해져 한 조건이 두 번 계산된다(피해가 폭주한다).
        public override int CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue)
        {
            int attack = source != null && source.Stats != null ? source.Stats.Attack : 0;
            return Mathf.Max(1, Mathf.RoundToInt(attack * Mathf.Max(0.1f, multiplier)));
        }

        public override string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue)
            => $"예상 피해: {CalculateRawDamage(source, ability, diceValue)}";

        public override string GetDynamicDescription()
            => $"자기 구역 몬스터에게 공격력 x{multiplier:0.##} 피해";
    }
}
