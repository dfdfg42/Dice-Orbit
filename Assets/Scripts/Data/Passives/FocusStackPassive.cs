using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "FocusStackPassive", menuName = "DiceOrbit/Passives/Focus Stack")]
    public class FocusStackPassive : PassiveAbility
    {
        private int lastTurnSeen = -1;
        private int stacks = 0;

        private int GetTurnCount()
        {
            return Core.TurnManager.Instance != null ? Core.TurnManager.Instance.TurnCount : 0;
        }

        public override void OnTurnStart(Core.Character owner)
        {
            int turnCount = GetTurnCount();
            if (turnCount != lastTurnSeen)
            {
                lastTurnSeen = turnCount;
                stacks += 1;
                Debug.Log($"[Passive][Focus] Stack +1 (Stacks: {stacks})");
            }
        }

        public override void OnBeforeAttack(Core.Character owner, Core.Monster target, ref int damage)
        {
            if (stacks <= 0) return;

            float bonusRate = 0.05f * stacks;
            int bonus = Mathf.RoundToInt(damage * bonusRate);
            damage += bonus;
            Debug.Log($"[Passive][Focus] Consumed {stacks} stack(s): +{bonus} damage");
            stacks = 0;
        }
    }
}
