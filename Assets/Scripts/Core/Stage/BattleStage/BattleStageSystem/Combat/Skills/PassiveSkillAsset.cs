using UnityEngine;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data.Skills
{
    [CreateAssetMenu(fileName = "New Passive Skill", menuName = "Dice Orbit/Skills/Passive Skill")]
    public class PassiveSkillAsset : SkillAsset
    {
        public override CharacterSkillType Type => CharacterSkillType.Passive;

        [Header("Passive Binding")]
        public CharacterPassive PassiveTemplate;
    }
}
