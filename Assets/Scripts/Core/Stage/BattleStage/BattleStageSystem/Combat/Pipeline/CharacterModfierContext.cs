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

    // (WarriorGreatswordModifiedContext는 2026-08-28 공용 모디파이어 전면 교체로 폐기 —
    //  시그니처 모디파이어가 사라져 전용 도화지가 더 이상 필요 없다)
}
