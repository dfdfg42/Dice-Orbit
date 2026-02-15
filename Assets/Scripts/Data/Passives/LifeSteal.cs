using UnityEngine;
using DiceOrbit.Data.Passives;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Passives.Impl
{
    [CreateAssetMenu(menuName = "DiceOrbit/Passives/LifeSteal")]
    public class LifeSteal : PassiveAbility, IIntValueReceiver
    {
        [SerializeField] private int stealAmount;

        public void SetValue(int value)
        {
            stealAmount = value;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (trigger == CombatTrigger.OnHit)
            {
                if (context.SourceUnit == owner)
                {
                    owner.Heal(stealAmount);
                }
            }
        }
    }
}
