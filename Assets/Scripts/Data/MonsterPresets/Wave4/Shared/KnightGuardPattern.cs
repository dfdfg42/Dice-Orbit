using UnityEngine;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave4.Shared
{
    /// <summary>
    /// 기사 호위 패턴 (사제 공용). 지정한 이름의 기사가 살아있으면 index 0(디버프/지원),
    /// 죽으면 index 1(공격)만 사용한다. CrystalStonePattern 조건부 분기 미러.
    /// </summary>
    [System.Serializable]
    public class KnightGuardPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        [Tooltip("이 이름의 몬스터가 살아있는지로 분기 (예: 태양의 기사)")]
        [SerializeField] private string knightName = "";

        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            int idx = IsKnightAlive() ? 0 : 1;
            if (idx >= availableSkills.Count) idx = 0;
            return availableSkills[idx];
        }

        private bool IsKnightAlive()
        {
            var cm = CombatManager.Instance;
            if (cm == null) return false;
            var monsters = cm.GetAliveMonsters();
            if (monsters == null) return false;
            return monsters.Any(m => m != null && m.IsAlive && m.Stats != null && m.Stats.MonsterName == knightName);
        }
    }
}
