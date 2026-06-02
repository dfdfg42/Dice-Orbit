using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Skills;

namespace DiceOrbit.Data.Modifiers
{
    public interface IModifierManager
    {
        void Initialize(Character character);
        void Add(CharacterModifier mod);
        void Remove(CharacterModifier mod);
        void CollectReactors(List<ICombatReactor> list);
    }

    /// <summary>
    /// 캐릭터에 붙는 모디파이어 컨테이너.
    /// </summary>
    public class ModifierManager<T> : IModifierManager where T : CharacterModfierContext
    {
        private Character _owner;
        private CharacterActiveSkill _skill;
        private readonly List<CharacterModifier> _modifiers = new();
        protected T _context;

        public IReadOnlyList<CharacterModifier> Modifiers => _modifiers;
        public int Count => _modifiers.Count;

        public void Initialize(Character character)
        {
            _owner = character;
            _context = (T)System.Activator.CreateInstance(typeof(T), _owner, _skill);
        }

        public void Add(CharacterModifier mod)
        {
            mod.owner = _owner;
            if (mod == null) return;
            _modifiers.Add(mod);
            RefreshContext();
        }

        public void Remove(CharacterModifier mod)
        {
            if (mod == null) return;
            _modifiers.Remove(mod);
            RefreshContext();
        }

        /// <summary>스킬 컨텍스트에 현재 장착된 모디파이어 효과를 적용합니다.</summary>
        public void RefreshContext()
        {
            _context = (T)System.Activator.CreateInstance(typeof(T), _owner, _skill);
            foreach (var mod in _modifiers)
            {
                if (mod != null)
                {
                    mod.OnRefreshSkill(_context);
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
