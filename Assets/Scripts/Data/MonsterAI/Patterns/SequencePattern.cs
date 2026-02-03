using UnityEngine;
using System.Collections.Generic;

namespace DiceOrbit.Data.MonsterAI.Patterns
{
    /// <summary>
    /// 순차 패턴 (스킬 목록을 순서대로 사용)
    /// </summary>
    [CreateAssetMenu(fileName = "SequencePattern", menuName = "DiceOrbit/Monster/Pattern/Sequence")]
    public class SequencePattern : MonsterPattern
    {
        // 몬스터마다 현재 인덱스를 저장해야 하므로, 
        // Runtime 상태는 Monster 클래스 내에 저장하거나 Dictionary로 관리해야 함.
        // 여기서는 간단히 Monster의 상태 저장이 필요하므로, 
        // MonsterPattern은 Stateless여야 하지만 상태 추적을 위해 Dictionary를 사용.
        
        private Dictionary<int, int> monsterSequenceIndices = new Dictionary<int, int>();

        public override void Initialize(Core.Monster monster)
        {
            if (monster == null) return;
            monsterSequenceIndices[monster.GetInstanceID()] = 0;
        }

        public override SkillData GetNextSkill(Core.Monster monster, List<SkillData> availableSkills)
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            
            int id = monster.GetInstanceID();
            if (!monsterSequenceIndices.ContainsKey(id))
            {
                monsterSequenceIndices[id] = 0;
            }
            
            int index = monsterSequenceIndices[id];
            var skill = availableSkills[index % availableSkills.Count];
            
            // Update index for next turn
            monsterSequenceIndices[id]++;
            
            return skill;
        }
    }
}
