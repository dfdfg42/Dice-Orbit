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
        };

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
            var pool = new List<System.Func<CharacterModifier>>(Factories);
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
