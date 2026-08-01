using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared
{
    /// <summary>[결정화] 턴 종료 시 살아있는 수정 핵에게 수정 중첩 1 공급. 수정석·수정 파편 공용.</summary>
    [System.Serializable]
    public class CrystallizePassive : PassiveAbility
    {
        public CrystallizePassive()
        {
            passiveName = "결정화";
            description = "턴 종료 시 수정 핵의 수정 중첩 1 증가";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            CrystalSet.EnsureWaveHook();
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPostAction || context.Phase != EventPhase.TurnEnd || context.SourceUnit != owner) return;

            var core = CrystalSet.GetCore();
            if (core != null) CrystalSet.AddStack(core, 1);
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
