using DiceOrbit.Core.Combo;

namespace DiceOrbit.Core.Tutorial
{
    /// <summary>
    /// 실제 콤보 이벤트를 튜토리얼 진행 조건으로 바꾼다.
    /// 이동만 했거나 강화 공격이 끊긴 경우에는 단계를 완료하지 않는다.
    /// </summary>
    public sealed class TutorialCombatProgress
    {
        private readonly Character warrior;
        private readonly Character rogue;

        public bool WarriorStage1Done { get; private set; }
        public bool RogueStage1Done { get; private set; }
        public bool WarriorStage2Done { get; private set; }
        public bool WarriorFinished { get; private set; }

        public TutorialCombatProgress(Character warrior, Character rogue)
        {
            this.warrior = warrior;
            this.rogue = rogue;
        }

        public void Observe(Character character, int stage, ComboOutcome outcome)
        {
            if (character == warrior && outcome == ComboOutcome.Advanced && stage == 1)
                WarriorStage1Done = true;

            if (character == rogue && outcome == ComboOutcome.Advanced && stage == 1)
                RogueStage1Done = true;

            if (character == warrior && outcome == ComboOutcome.Advanced && stage == 2)
                WarriorStage2Done = true;

            if (character == warrior && outcome == ComboOutcome.Finished && stage == 0)
                WarriorFinished = true;
        }
    }
}
