using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Modifiers
{
    public interface IModifierManager
    {
        void Initialize(Character character);
        void Add(CharacterModifier mod);
        void Remove(CharacterModifier mod);
        void CollectReactors(List<ICombatReactor> list);
        void ApplyTo(CharacterModfierContext context);
        IReadOnlyList<CharacterModifier> Modifiers { get; }
    }

    /// <summary>
    /// 캐릭터에 붙는 모디파이어 컨테이너.
    /// 컨텍스트는 스킬이 필요할 때마다 새로 생성(GenerateContext)되고,
    /// 여기서 장착된 모디파이어들의 OnRefreshSkill을 그 위에 덧칠한다("도화지" 패턴).
    /// </summary>
    public class ModifierManager : IModifierManager
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
            mod.owner = _owner;
            _modifiers.Add(mod);
        }

        public void Remove(CharacterModifier mod)
        {
            if (mod == null) return;
            _modifiers.Remove(mod);
        }

        /// <summary>주어진 컨텍스트(빈 도화지)에 장착된 모디파이어 효과를 일괄 적용한다.</summary>
        public void ApplyTo(CharacterModfierContext context)
        {
            if (context == null) return;
            foreach (var mod in _modifiers)
            {
                if (mod == null) continue;
                mod.OnRefreshSkill(context);
                if (context.IsCancelled) break;
            }
        }

        /// <summary>Character.CollectReactors에서 호출됨 (Generic 모디파이어의 파이프라인 훅).</summary>
        public void CollectReactors(List<ICombatReactor> list)
        {
            foreach (var m in _modifiers)
                if (m != null) list.Add(m);
        }
    }
}
