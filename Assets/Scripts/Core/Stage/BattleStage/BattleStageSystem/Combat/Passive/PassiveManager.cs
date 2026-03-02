using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Passives;
using DiceOrbit.Core.Pipeline;
using System.Linq;

namespace DiceOrbit.Systems.Passives
{
    [System.Serializable]
    public class PassiveManager : MonoBehaviour, ICombatReactor
    {
        private Unit owner;
        [SerializeField] private List<PassiveAbility> activePassives = new();
        public IReadOnlyList<PassiveAbility> ActivePassives => activePassives;

        public void Initialize(Unit unit)
        {
            owner = unit;
        }

        public void AddPassive(PassiveAbility passive)
        {
            if (passive == null) return;

            // 중첩 가능 여부 체크
            var existingSame = activePassives.Find(p => p.GetType() == passive.GetType());
            if (existingSame != null)
            {
                // 같은 타입이 이미 존재할 때
                if (!existingSame.AllowSamePassive(passive))
                {
                    // 중첩 불가능하면 추가하지 않음
                    return;
                }
            }

            // 패시브 추가
            activePassives.Add(passive);
        }

        public void RemovePassive(PassiveAbility passive)
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

        private void OnDestroy()
        {
            // [Serializable]이므로 Destroy 불필요
            activePassives.Clear();
        }
    }
}
