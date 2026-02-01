using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "PositioningPassive", menuName = "DiceOrbit/Passives/Positioning")]
    public class PositioningPassive : PassiveAbility
    {
        private int lastTurnSeen = -1;
        private int movedThisTurn = 0;
        private bool bonusConsumed = false;

        private int GetTurnCount()
        {
            return Core.TurnManager.Instance != null ? Core.TurnManager.Instance.TurnCount : 0;
        }

        public override void OnMove(Core.Character owner, int distance)
        {
            int turnCount = GetTurnCount();
            if (turnCount != lastTurnSeen)
            {
                lastTurnSeen = turnCount;
                movedThisTurn = 0;
                bonusConsumed = false;
            }

            movedThisTurn += distance;
        }

        public override void OnBeforeAttack(Core.Character owner, Core.Monster target, ref int damage)
        {
            int turnCount = GetTurnCount();
            if (turnCount != lastTurnSeen)
            {
                lastTurnSeen = turnCount;
                movedThisTurn = 0;
                bonusConsumed = false;
            }

            if (!bonusConsumed && movedThisTurn >= 5)
            {
                int bonus = Mathf.RoundToInt(damage * 0.05f);
                damage += bonus;
                bonusConsumed = true;
                Debug.Log($"[Passive][Positioning] Bonus applied: +{bonus} damage");
            }
        }
    }
}
