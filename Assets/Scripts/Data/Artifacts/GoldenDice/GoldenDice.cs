using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>황금 주사위 — 전투 보상 골드 +N.</summary>
    [System.Serializable]
    public class GoldenDice : RuntimeArtifact
    {
        public int amount = 25;
        public override int BattleGoldBonus => amount;
    }
}
