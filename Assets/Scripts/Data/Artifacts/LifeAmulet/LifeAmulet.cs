using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>생명의 부적 — 전투 시작 시 파티 전원 +N 회복.</summary>
    [System.Serializable]
    public class LifeAmulet : RuntimeArtifact
    {
        public int amount = 5;
        public override int BattleStartHeal => amount;
    }
}
