using UnityEngine;
using DiceOrbit.Data.Passives;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Passives.Impl
{
    [CreateAssetMenu(menuName = "DiceOrbit/Passives/GainTempArmor")]
    public class GainTempArmor : PassiveAbility, IIntValueReceiver
    {
        [SerializeField] private int ArmorAmount;

        public void SetValue(int value)
        {
            ArmorAmount = value;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context.Target == owner &&
                context.Action.Type == ActionType.Attack &&
                trigger == CombatTrigger.OnHit &&
                context.IsEffected == true)
            {
                owner.Stats.TempArmor += ArmorAmount;
            }
        }

        public override string Description()
        {
            return $"공격으로 피해를 입을 시 피해량의 {ArmorAmount}만큼 생명력 회복";
        }
    }
}
