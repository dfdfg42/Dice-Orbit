using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>포근한 침낭 — 휴식 회복량 +N%p.</summary>
    [System.Serializable]
    public class CozyBedroll : RuntimeArtifact
    {
        public float percent = 20f;
        public override float RestHealBonusPercent => percent;
    }
}
