using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "BattleCryPassive", menuName = "DiceOrbit/Passives/Battle Cry")]
    public class BattleCryPassive : PassiveAbility
    {
        private static int lastTurnSeen = -1;
        private static bool usedThisTurn = false;

        private int GetTurnCount()
        {
            return Core.TurnManager.Instance != null ? Core.TurnManager.Instance.TurnCount : 0;
        }

        public override void OnBeforeAttack(Core.Character owner, Core.Monster target, ref int damage)
        {
            int turnCount = GetTurnCount();
            if (turnCount != lastTurnSeen)
            {
                lastTurnSeen = turnCount;
                usedThisTurn = false;
            }

            if (!usedThisTurn)
            {
                int bonus = Mathf.RoundToInt(damage * 0.05f);
                damage += bonus;
                usedThisTurn = true;
                Debug.Log($"[Passive][BattleCry] First action bonus applied: +{bonus} damage");
            }
        }
    }
}
