using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 연금술사 [시약 준비]로 설치되는 타일. 연금술사 본인이 통과하거나 그 위에서
    /// 턴을 종료하면 패시브 스택을 1 쌓고 자기 자신을 소모한다.
    /// </summary>
    public class ReagentTile : TileAttribute
    {
        private readonly ReagentPrepPassive ownerPassive;
        private readonly Core.Character alchemist;

        public ReagentTile(ReagentPrepPassive passive, Core.Character alchemist)
            : base(TileAttributeType.Reagent, 0, -1, false)
        {
            this.ownerPassive = passive;
            this.alchemist = alchemist;
        }

        public override void OnTraverse(Core.Character character) => TryConsume(character);

        public override void OnEndTurn(Core.Character character) => TryConsume(character);

        private void TryConsume(Core.Character character)
        {
            if (character == null || alchemist == null) return;
            if (character != alchemist) return;

            ownerPassive?.AddReagentStack();
            Owner?.RemoveAttribute(this);
        }

        public override string GetDescription()
        {
            return "연금술사가 지나가거나 이 타일에서 턴을 마치면, 이번 전투 동안 주는 피해가 증가합니다. 효과는 중첩됩니다.";
        }
    }
}
