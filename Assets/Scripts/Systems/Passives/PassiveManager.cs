using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Systems.Passives
{
    public class PassiveManager : MonoBehaviour
    {
        private Core.Character owner;
        private List<PassiveAbility> activePassives = new List<PassiveAbility>();

        public void Initialize(Core.Character character)
        {
            owner = character;
        }

        public void AddPassive(PassiveAbility passive)
        {
            if (passive == null) return;
            
            // 인스턴스화하여 개별 상태 가질 수 있게 함 (선택사항, ScriptableObject라 공유됨에 주의)
            // 여기서는 SO를 데이터로만 쓰고 상태는 Manager에서 관리하거나, 필요한 경우 Clone
            // 복잡한 상태가 있는 패시브라면 Clone 필요. 지금은 참조만.
             if (!activePassives.Contains(passive))
            {
                activePassives.Add(passive);
                passive.Initialize(owner);
                Debug.Log($"[Passive] {passive.PassiveName} added.");
            }
        }

        public void RemovePassive(PassiveAbility passive)
        {
            if (activePassives.Contains(passive))
            {
                activePassives.Remove(passive);
            }
        }

        // Event Triggers
        public void OnTurnStart()
        {
            foreach (var p in activePassives) p.OnTurnStart(owner);
        }

        public void OnMove(int distance)
        {
            foreach (var p in activePassives) p.OnMove(owner, distance);
        }

        public void OnBeforeAttack(Core.Monster target, ref int damage)
        {
            foreach (var p in activePassives) p.OnBeforeAttack(owner, target, ref damage);
        }

        public void OnAfterAttack(Core.Monster target)
        {
            foreach (var p in activePassives) p.OnAfterAttack(owner, target);
        }
        
        public void OnDamageTaken(int damage)
        {
            foreach (var p in activePassives) p.OnDamageTaken(owner, damage);
        }
    }
}
