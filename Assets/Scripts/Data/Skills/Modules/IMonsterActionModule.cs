using DiceOrbit.Core;

namespace DiceOrbit.Data.Skills.Modules
{
    public interface IMonsterActionModule
    {
        void Execute(Monster source, int diceValue);
    }
}
