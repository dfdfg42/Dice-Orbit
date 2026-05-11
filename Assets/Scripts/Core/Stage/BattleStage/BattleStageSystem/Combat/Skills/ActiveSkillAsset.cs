using UnityEngine;
using DiceOrbit.Visuals;

namespace DiceOrbit.Data.Skills
{
    [CreateAssetMenu(fileName = "New Active Skill", menuName = "Dice Orbit/Skills/Active Skill")]
    public class ActiveSkillAsset : SkillAsset
    {
        public override CharacterSkillType Type => CharacterSkillType.Active;

        [Header("Active Binding")]
        public CharacterActiveTemplate ActiveTemplate;
        public CharacterSkillTargetType TargetType = CharacterSkillTargetType.OneEnemy;

        [Header("Tile Targeting Preview")]
        public TilePreviewStyle PreviewStyle = TilePreviewStyle.Neutral;
    }
}
