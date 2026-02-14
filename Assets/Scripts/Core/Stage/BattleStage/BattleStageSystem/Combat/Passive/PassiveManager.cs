using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Passives;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Systems.Passives
{
    public class PassiveManager : MonoBehaviour, ICombatReactor
    {
        private object owner;
        private List<PassiveAbility> activePassives = new List<PassiveAbility>();
        private readonly Dictionary<PassiveAbility, PassiveAbility> runtimePassiveMap = new Dictionary<PassiveAbility, PassiveAbility>();
        public IReadOnlyList<PassiveAbility> ActivePassives => activePassives;

        public void Initialize(Character character)
        {
            owner = character;
        }
        
        public void Initialize(Monster monster)
        {
            owner = monster;
        }

        public void AddPassive(PassiveAbility passive)
        {
            if (passive == null) return;
            if (!runtimePassiveMap.ContainsKey(passive))
            {
                var runtimePassive = ScriptableObject.Instantiate(passive);
                runtimePassive.name = passive.name;

                activePassives.Add(runtimePassive);
                runtimePassiveMap[passive] = runtimePassive;

                if (owner is Character c) runtimePassive.Initialize(c);
                else if (owner is Monster m) runtimePassive.Initialize(m);
                // Trigger OnPassiveAdded reactor?
                Debug.Log($"[PassiveManager] Added {runtimePassive.PassiveName}");
            }
        }

        public void RemovePassive(PassiveAbility passive)
        {
            if (passive == null) return;

            if (runtimePassiveMap.TryGetValue(passive, out var runtimePassive))
            {
                activePassives.Remove(runtimePassive);
                runtimePassiveMap.Remove(passive);
                if (runtimePassive != null)
                {
                    Destroy(runtimePassive);
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
            foreach (var passive in activePassives)
            {
                passive.OnReact(trigger, context);
            }
        }

        private void OnDestroy()
        {
            foreach (var passive in activePassives)
            {
                if (passive != null)
                {
                    Destroy(passive);
                }
            }
            activePassives.Clear();
            runtimePassiveMap.Clear();
        }
    }
}
