using System.Collections.Generic;

namespace DiceOrbit.UI
{
    public readonly struct HoverTooltipData
    {
        public readonly string MainText;
        public readonly IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData> Statuses;
        public readonly IReadOnlyList<TooltipKeywordFormatter.KeywordDisplayData> Passives;

        public HoverTooltipData(
            string mainText, 
            IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData> statuses = null,
            IReadOnlyList<TooltipKeywordFormatter.KeywordDisplayData> passives = null)
        {
            MainText = mainText;
            Statuses = statuses;
            Passives = passives;
        }
    }

    public interface IHoverTooltipProvider
    {
        HoverTooltipData GetHoverTooltipData();
    }
}
