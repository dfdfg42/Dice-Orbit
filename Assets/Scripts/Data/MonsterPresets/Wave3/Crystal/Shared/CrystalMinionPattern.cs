using UnityEngine;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave3.Crystal.Shared
{
    /// <summary>
    /// 부하(수정석·수정 파편) 공용 조건부 AI. 평소 availableSkills[0]/[1] 50:50.
    /// 단, 살아있는 수정 핵이 없으면 coreAbsentSkillIndex 스킬만 사용.
    /// </summary>
    [System.Serializable]
    public class CrystalMinionPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        [Tooltip("수정 핵이 없을 때 사용할 스킬 인덱스 (수정석=1 수정창, 수정파편=0 수정비)")]
        [SerializeField] private int coreAbsentSkillIndex = 0;

        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;

            if (CrystalSet.GetCore() == null)
            {
                int idx = Mathf.Clamp(coreAbsentSkillIndex, 0, availableSkills.Count - 1);
                return availableSkills[idx];
            }

            return availableSkills[Random.Range(0, availableSkills.Count)];
        }
    }
}
