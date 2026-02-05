using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    [CreateAssetMenu(fileName = "BattleCryFirstAlly", menuName = "Dice Orbit/Passives/Battle Cry (First Ally)")]
    public class BattleCryFirstAllyPassive : PassiveAbility
    {
        [Header("Settings")]
        [SerializeField] private float bonusPercent = 0.05f;

        private Core.Character owner;

        public override void Initialize(Core.Character owner)
        {
            this.owner = owner;
            base.Initialize(owner);
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;

            if (context.Action.Type != Core.Pipeline.ActionType.Attack)
            {
                return;
            }

            if (context.SourceUnit is Core.Character source)
            {
                if (Core.PartyManager.Instance == null || !Core.PartyManager.Instance.Party.Contains(source))
                {
                    return;
                }

                if (Core.PartyManager.Instance.TryConsumeTeamFirstAction())
                {
                    context.OutputValue *= (1f + bonusPercent);
                    Debug.Log($"[BattleCry] First ally action bonus applied (+{bonusPercent * 100f:0.#}%)");
                }
            }
        }
    }
}