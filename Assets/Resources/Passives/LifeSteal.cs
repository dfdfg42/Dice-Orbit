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
            if (context.SourceUnit == owner && context.Action.Type == ActionType.Attack &&trigger == CombatTrigger.OnHit)
            {
                owner.Heal(stealAmount);
   
            }
        }

        public override string Description()
        {
            return $"공격 시 피해량의 {stealAmount}만큼 생명력 회복";
        }
    }
}
