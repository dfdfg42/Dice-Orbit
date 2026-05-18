using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Passives;
using DiceOrbit.Core.Pipeline;
using System.Linq;

namespace DiceOrbit.Systems.Passives
{
    [System.Serializable]
    public class PassiveManager : MonoBehaviour, ICombatReactor
    {
        private Unit owner;
        private List<IPassive> activePassives = new();
        public IReadOnlyList<IPassive> ActivePassives => activePassives;

        public void Initialize(Unit unit)
        {
            owner = unit;
        }

        public void AddPassive(IPassive passive)
        {
            if (passive == null) return;

            var existingSame = activePassives.Find(p => p.GetType() == passive.GetType());
            if (existingSame != null && !existingSame.AllowSamePassive(passive))
                return;

            activePassives.Add(passive);
        }

        public void RemovePassive(IPassive passive)
        {
            if (passive == null) return;
            activePassives.Remove(passive);
        }

        // ICombatReactor Implementation
        // PassiveManager가 Reactor로서 파이프라인에 등록되면, 자신이 관리하는 모든 패시브에게 전파
        public int Priority => 0; // 매니저 자체의 우선순위

        public void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // Priority 순서대로 정렬해서 실행
            var sortedPassives = activePassives.OrderByDescending(p => p.Priority).ToList();

            
            foreach (var passive in sortedPassives)
            {
                passive.OnReact(trigger, context);
            }
        }

        public void BroadcastOwnerMoved(TileData newTile)
        {
            foreach (var passive in activePassives)
            {
                if (passive is CharacterPassiveSkill cps)
                    cps.OnOwnerMoved(newTile);
            }
        }

        private void OnDestroy()
        {
            // [Serializable]이므로 Destroy 불필요
            activePassives.Clear();
        }
    }
}
