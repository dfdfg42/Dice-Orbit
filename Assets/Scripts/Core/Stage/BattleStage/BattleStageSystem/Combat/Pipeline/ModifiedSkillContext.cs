using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using DiceOrbit.Visuals;

namespace DiceOrbit.Core.Pipeline
{
    public class ModifiedSkillContext
    {
        public Character SourceCharacter { get; }
        public CharacterActiveSkill OriginalSkill { get; }
        public int SkillLevel { get; }

        public CharacterSkillTargetType TargetType { get; set; }
        public int TargetCount { get; set; }
        public TilePreviewStyle PreviewStyle { get; set; }
        public bool IsCancelled { get; set; }

        public ModifiedSkillContext(Character source, CharacterActiveSkill skill, int skillLevel = 1)
        {
            SourceCharacter = source;
            OriginalSkill = skill;
            SkillLevel = skillLevel;

            TargetType = skill.TargetType;
            TargetCount = skill.TargetCount;
            PreviewStyle = skill.PreviewStyle;
        }
    }

    public class WarriorGreatswordModifiedContext : ModifiedSkillContext
    {
        public int BaseDamageMultiplier { get; set; }

        public WarriorGreatswordModifiedContext(Character source, CharacterActiveSkill skill, int skillLevel, int baseMultiplier) 
            : base(source, skill, skillLevel)
        {
            BaseDamageMultiplier = baseMultiplier;
        }
    }
}
