using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data;
using DiceOrbit.Systems.Effects;
using DiceOrbit.UI;
using UnityEngine;

namespace DiceOrbit.Data.Potions
{
    /// <summary>독 포션 — 특정 적에게 2턴 독 부여 (매턴 최대체력 5% 피해).</summary>
    [CreateAssetMenu(fileName = "PoisonPotion", menuName = "DiceOrbit/Potions/Poison Potion")]
    public class PoisonPotion : Potion
    {
        [Header("효과")]
        [SerializeField] private int percentPerTurn = 5;
        [SerializeField] private int turns = 2;

        private void Reset()
        {
            PotionName = "독 포션";
            Description = "선택한 적에게 2턴 동안 독을 부여합니다. 독은 턴마다 최대 체력의 5%만큼 피해를 줍니다.";
            TargetType = PotionTargetType.Enemy;
            ShopPrice = 50;
            CombatOnly = true;
        }

        public override bool Use(Unit target = null)
        {
            if (!(target is Monster) || !target.IsAlive || target.StatusEffects == null) return false;

            target.StatusEffects.AddEffect(StatusEffectManager.CreateEffect(EffectType.Poison, percentPerTurn, turns));
            var data = TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Poison.ToString(), percentPerTurn, turns);
            CombatNotifier.NotifyStatus(target, data.Name, data.Color);
            return true;
        }
    }
}
