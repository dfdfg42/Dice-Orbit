using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    public interface IPassive : ICombatReactor
    {
        string PassiveName { get; }
        string Description { get; }
        bool IsStackable { get; }
        void Initialize(Unit owner);
        IPassive Clone();
        bool AllowSamePassive(IPassive incoming);
        void OnOwnerSelected(Character c);
        void OnOwnerDeselected();
        string GetDynamicDescription();
    }
}
