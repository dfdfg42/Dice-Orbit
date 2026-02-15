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
        [SerializeField] private Dictionary<PassiveType, HashSet<PassiveAbility>> activePassives = new();
        public IReadOnlyDictionary<PassiveType, HashSet<PassiveAbility>> ActivePassives => activePassives;

        public void Initialize(Unit unit)
        {
            owner = unit;
        }

        public void AddPassive(PassiveAbility passive)
        {
            if (passive == null) return;
            
            // 해당 타입의 패시브가 이미 존재하고, 하나 이상 활성화되어 있다면
            if (activePassives.TryGetValue(passive.type, out var existingSet) && existingSet.Count > 0)
            {
                // 중첩 허용 여부 체크 후 추가
                var first = existingSet.First();
                if (first.AllowSameSkill(passive))
                {
                    existingSet.Add(passive);
                }
                else
                {
                    // passive는 사용되지 않으므로 파괴 (이미 인스턴스화되어 넘어왔으므로)
                    Destroy(passive);
                }
            }
            else
            {
                // 키가 없으면 새로 생성
                if (!activePassives.ContainsKey(passive.type))
                {
                    activePassives[passive.type] = new HashSet<PassiveAbility>();
                }
                activePassives[passive.type].Add(passive);
            }
        }

        public void RemovePassive(PassiveAbility passive)
        {
            if (passive == null) return;
            
            // 해당 타입 존재 여부 확인
            if (activePassives.TryGetValue(passive.type, out var existingSet))
            {
                // Set에서 해당 객체를 찾아 제거하고, 성공 시 파괴
                if (existingSet.Remove(passive))
                {
                    Destroy(passive);
                }
            }
        }

        // ICombatReactor Implementation
        // PassiveManager가 Reactor로서 파이프라인에 등록되면, 자신이 관리하는 모든 패시브에게 전파
        public int Priority => 0; // 매니저 자체의 우선순위

        public void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 자신이 가진 모든 패시브를 실행
            // 주의: 여기서 패시브들의 Priority 정렬이 필요할 수도 있음.
            // 일단은 리스트 순서대로 실행
            foreach (var passiveList in activePassives.Values)
            {
                foreach (var passive in passiveList)
                {
                    passive.OnReact(trigger, context);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var passiveList in activePassives.Values)
            {
                foreach (var passive in passiveList)
                {
                    Destroy(passive);
                }
            }
            activePassives.Clear();
        }
    }
}
