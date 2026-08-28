using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data;
using DiceOrbit.Systems.Effects;
using DiceOrbit.UI;
using UnityEngine;

namespace DiceOrbit.Data.Potions
{
    /// <summary>독 포션 — 특정 적에게 중독 중첩 부여 (턴마다 중첩만큼 피해 후 중첩 -1, 방어도 무시).</summary>
    [CreateAssetMenu(fileName = "PoisonPotion", menuName = "DiceOrbit/Potions/Poison Potion")]
    public class PoisonPotion : Potion
    {
        [Header("효과")]
        [Tooltip("부여할 중독 중첩. 턴마다 남은 중첩만큼 피해를 주고 1씩 줄어든다.")]
        [SerializeField] private int stacks = 5;

        private void Reset()
        {
            PotionName = "독 포션";
            Description = "선택한 적에게 중독 5를 부여합니다. 중독된 적은 턴이 시작될 때마다 남은 중독 수치만큼 피해를 받고 수치가 1 줄어듭니다. 이 피해는 방어도를 무시합니다.";
            TargetType = PotionTargetType.Enemy;
            ShopPrice = 50;
            CombatOnly = true;
        }

        public override bool Use(Unit target = null)
        {
            if (!(target is Monster) || !target.IsAlive || target.StatusEffects == null) return false;

            target.StatusEffects.AddEffect(StatusEffectManager.CreateEffect(EffectType.Poison, stacks, -1));
            var data = TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Poison.ToString(), stacks, -1);
            CombatNotifier.NotifyStatus(target, data.Name, data.Color);
            return true;
        }
    }
}
