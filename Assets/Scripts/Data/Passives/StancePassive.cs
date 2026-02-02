using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "StancePassive", menuName = "DiceOrbit/Passives/Stance")]
    public class StancePassive : PassiveAbility
    {
        private int lastTurnSeen = -1;
        private int movedThisTurn = 0;
        private bool bonusConsumed = false;

        private int GetTurnCount()
        {
            return Core.CombatManager.Instance != null ? Core.CombatManager.Instance.TurnCount : 0;
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

            if (!bonusConsumed && movedThisTurn <= 3)
            {
                int bonus = Mathf.RoundToInt(damage * 0.10f);
                damage += bonus;
                bonusConsumed = true;
                Debug.Log($"[Passive][Stance] Bonus applied: +{bonus} damage");
            }
        }
    }
}
