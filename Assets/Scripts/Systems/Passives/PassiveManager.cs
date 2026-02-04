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
            if (!activePassives.Contains(passive))
            {
                activePassives.Add(passive);
                activePassives.Add(passive);
                if (owner is Character c) passive.Initialize(c);
                // Trigger OnPassiveAdded reactor?
                Debug.Log($"[PassiveManager] Added {passive.PassiveName}");
            }
        }

        public void RemovePassive(PassiveAbility passive)
        {
            if (activePassives.Contains(passive))
            {
                activePassives.Remove(passive);
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
    }
}
