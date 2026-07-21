using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Artifacts
{
    /// <summary>단골 도장 — 상점 가격 -N%.</summary>
    [System.Serializable]
    public class RegularStamp : RuntimeArtifact
    {
        public float percent = 20f;
        public override float ShopDiscountPercent => percent;
    }
}
