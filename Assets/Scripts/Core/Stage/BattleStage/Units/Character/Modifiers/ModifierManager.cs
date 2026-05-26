using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Modifiers
{
    /// <summary>
    /// 캐릭터에 붙는 모디파이어 컨테이너.
    /// PassiveManager와 나란히 Character에 AddComponent됨.
    /// </summary>
    public class ModifierManager : MonoBehaviour
    {
        private Character _owner;
        private readonly List<CharacterModifier> _modifiers = new();

        public IReadOnlyList<CharacterModifier> Modifiers => _modifiers;
        public int Count => _modifiers.Count;

        public void Initialize(Character character)
        {
            _owner = character;
        }

        public void Add(CharacterModifier mod)
        {
            if (mod == null) return;
            _modifiers.Add(mod);
            mod.OnEquipped(_owner);
        }

        public void Remove(CharacterModifier mod)
        {
            if (mod == null) return;
            mod.OnUnequipped(_owner);
            _modifiers.Remove(mod);
        }

        /// <summary>특정 스킬 컨텍스트에 현재 장착된 모디파이어 효과를 적용합니다.</summary>
        public void ApplyModifiersToContext(ModifiedSkillContext context)
        {
            foreach (var mod in _modifiers)
            {
                if (mod != null)
                {
                    mod.OnRefreshSkill(context);
                    if (context.IsCancelled) break;
                }
            }
        }

        /// <summary>Unit.CollectAdditionalReactors에서 호출됨.</summary>
        public void CollectReactors(List<ICombatReactor> list)
        {
            foreach (var m in _modifiers)
                if (m != null) list.Add(m);
        }
    }
}
