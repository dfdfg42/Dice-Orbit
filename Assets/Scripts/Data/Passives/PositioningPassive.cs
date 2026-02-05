using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "PositioningPassive", menuName = "Dice Orbit/Passives/Positioning")]
    public class PositioningPassive : PassiveAbility
    {
        [Header("Settings")]
        [SerializeField] private int requiredTiles = 5;
        [SerializeField] private float bonusPercent = 0.05f;

        private Core.Character owner;
        private int movedThisTurn = 0;
        private bool bonusReady = false;

        public override void Initialize(Core.Character owner)
        {
            this.owner = owner;
            base.Initialize(owner);
            movedThisTurn = 0;
            bonusReady = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context.SourceUnit != owner) return;

            if (trigger == CombatTrigger.OnTurnStart)
            {
                movedThisTurn = 0;
                bonusReady = false;
            }

            if (trigger == CombatTrigger.OnPostAction && context.Action.HasTag("Move"))
            {
                movedThisTurn += Mathf.RoundToInt(context.Action.BaseValue);
                if (movedThisTurn >= requiredTiles)
                {
                    bonusReady = true;
                }
            }

            if (trigger == CombatTrigger.OnCalculateOutput && bonusReady && context.Action.Type == Core.Pipeline.ActionType.Attack)
            {
                context.OutputValue *= (1f + bonusPercent);
                bonusReady = false;
                Debug.Log($"[Positioning] Bonus applied (+{bonusPercent * 100f:0.#}%) after moving {movedThisTurn} tiles");
            }
        }
    }
}