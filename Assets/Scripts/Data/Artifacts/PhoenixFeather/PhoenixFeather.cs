using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>불사조 깃털 — 점감 부활 HP +N%p.</summary>
    [System.Serializable]
    public class PhoenixFeather : RuntimeArtifact
    {
        public float percent = 15f;
        public override float ReviveHpBonusPercent => percent;
    }
}
