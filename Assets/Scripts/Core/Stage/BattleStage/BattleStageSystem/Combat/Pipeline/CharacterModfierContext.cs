using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using DiceOrbit.Visuals;

namespace DiceOrbit.Core.Pipeline
{
    public class CharacterModfierContext
    {
        public Character SourceCharacter { get; }
        public int SkillLevel { get; }

        public CharacterSkillTargetType TargetType { get; set; }
        public int TargetCount { get; set; }
        public TilePreviewStyle PreviewStyle { get; set; }
        public bool IsCancelled { get; set; }

        public CharacterModfierContext(Character source, CharacterActiveSkill skill)
        {
            SourceCharacter = source;
            SkillLevel = 1;

            if (skill != null)
            {
                TargetType = skill.TargetType;
                TargetCount = skill.TargetCount;
                PreviewStyle = skill.PreviewStyle;
            }
        }
    }

    public class WarriorGreatswordModifiedContext : CharacterModfierContext
    {
        public int BaseDamageMultiplier { get; set; }

        public WarriorGreatswordModifiedContext(Character source, CharacterActiveSkill skill)
            : base(source, skill)
        {
            BaseDamageMultiplier = 1;
        }
    }
}
