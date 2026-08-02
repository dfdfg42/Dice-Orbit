using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers
{
    /// <summary>
    /// 보상 등에서 제시할 수 있는 모디파이어 목록 레지스트리.
    /// 모디파이어는 [Serializable] 일반 클래스이므로 팩토리 함수로 매번 새 인스턴스를 만든다.
    /// 새 모디파이어를 추가하려면 Factories 배열에 한 줄 추가.
    /// </summary>
    public static class ModifierRegistry
    {
        private static readonly System.Func<CharacterModifier>[] Factories =
        {
            () => new Generic.SharpBladeModifier(),
            () => new Generic.BerserkModifier(),
            () => new Generic.GiantStrengthModifier(),
            () => new Warrior.GreatswordWideSwing(),
            () => new Alchemist.AlchemistExtraReagent(),
            () => new Rogue.RoguePositioningBoost(),
            () => new Mage.MageFocusBoost(),
        };

        // 팩토리를 한 번씩만 돌려 얻은 타입명 캐시 — Exists가 매번 인스턴스를 만들지 않게 한다.
        private static string[] _ids;

        private static string[] Ids
        {
            get
            {
                if (_ids == null)
                {
                    _ids = new string[Factories.Length];
                    for (int i = 0; i < Factories.Length; i++) _ids[i] = Factories[i]().GetType().Name;
                }
                return _ids;
            }
        }

        /// <summary>
        /// 세이브 ID(클래스 타입명)로 모디파이어 새 인스턴스 생성. 없으면 null.
        /// 캐릭터마다 독립 인스턴스가 필요하므로 매번 새로 만든다.
        /// </summary>
        public static CharacterModifier Create(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var ids = Ids;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == id) return Factories[i]();
            return null;
        }

        /// <summary>Validate 단계용 — 인스턴스를 만들지 않는다.</summary>
        public static bool Exists(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var known in Ids)
                if (known == id) return true;
            return false;
        }

        /// <summary>전체 모디파이어를 새 인스턴스로 생성.</summary>
        public static List<CharacterModifier> CreateAll()
        {
            var list = new List<CharacterModifier>(Factories.Length);
            foreach (var f in Factories) list.Add(f());
            return list;
        }

        /// <summary>중복 없이 무작위 count개를 새 인스턴스로 반환.</summary>
        public static List<CharacterModifier> GetRandomChoices(int count)
        {
            return GetRandomChoicesFor(null, count);
        }

        /// <summary>
        /// 해당 캐릭터에게 줄 수 있는(CanApplyTo) 모디파이어만 골라 무작위 count개 반환.
        /// character가 null이면 전체 풀에서 뽑는다.
        /// </summary>
        public static List<CharacterModifier> GetRandomChoicesFor(Core.Character character, int count)
        {
            // 캐릭터에 적용 가능한 팩토리만 필터
            var pool = new List<System.Func<CharacterModifier>>();
            foreach (var f in Factories)
            {
                if (character == null) { pool.Add(f); continue; }
                if (f().CanApplyTo(character)) pool.Add(f);
            }

            // Fisher-Yates 셔플
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            int n = Mathf.Clamp(count, 0, pool.Count);
            var result = new List<CharacterModifier>(n);
            for (int i = 0; i < n; i++) result.Add(pool[i]());
            return result;
        }
    }
}
