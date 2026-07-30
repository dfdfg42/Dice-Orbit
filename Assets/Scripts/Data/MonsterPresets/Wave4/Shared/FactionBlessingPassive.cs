using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave4.Shared
{
    /// <summary>
    /// 진영 가호 패시브 (태양의 가호 / 달의 가호 공용).
    /// 매 턴 시작, 홀짝·첫턴 조건이 맞으면 같은 진영 몬스터 전원에게 영구 공격력 +amount(중첩)를 부여한다.
    /// 태양 = triggerOnOddTurn + skipFirstTurn(턴 1 제외 → 3,5,7…), 달 = 해제 + 미제외(2,4,6…).
    /// 영구 공격버프는 기존 BuffAttackStatus(value, -1){IsStackable=true}(=DewPoint 패턴)를 재사용한다.
    /// </summary>
    [System.Serializable]
    public class FactionBlessingPassive : PassiveAbility
    {
        [Header("Blessing Settings")]
        [Tooltip("체크 시 홀수 턴에 발동, 해제 시 짝수 턴에 발동")]
        [SerializeField] private bool triggerOnOddTurn = true;
        [Tooltip("이번 발동으로 같은 진영에 더할 영구 공격력")]
        [SerializeField] private int amount = 3;
        [Tooltip("첫 번째 턴(턴 1)에는 발동하지 않음")]
        [SerializeField] private bool skipFirstTurn = false;

        public FactionBlessingPassive()
        {
            passiveName = "태양의 가호";
            description = "지정된 홀짝 턴마다 같은 진영 전체의 공격력을 영구히 올린다";
            priority = 10;
            isStackable = false;
        }

        public override string GetDynamicDescription()
            => $"{(triggerOnOddTurn ? "홀수" : "짝수")} 턴마다 같은 진영 공격력 +{amount} (영구)";

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction) return;
            if (context.Phase != EventPhase.TurnStart) return;
            if (context.SourceUnit != owner) return;

            var cm = CombatManager.Instance;
            if (cm == null) return;

            bool isOddTurn = (cm.TurnCount % 2) == 1;
            if (isOddTurn != triggerOnOddTurn) return;
            if (skipFirstTurn && cm.TurnCount <= 1) return;

            var myFaction = (owner as Monster)?.Faction ?? MonsterFaction.None;
            if (myFaction == MonsterFaction.None) return;

            var monsters = cm.ActiveMonsters;
            if (monsters == null) return;

            int count = 0;
            foreach (var m in monsters)
            {
                if (m == null || !m.IsAlive || m.StatusEffects == null) continue;
                if (m.Faction != myFaction) continue;
                m.StatusEffects.AddEffect(new BuffAttackStatus(amount, -1) { IsStackable = true });
                count++;
            }
            Debug.Log($"[{PassiveName}] {myFaction} {count}명 공격력 +{amount} (영구) — turn {cm.TurnCount}");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
