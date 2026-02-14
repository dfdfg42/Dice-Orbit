//using System.Collections.Generic;
//using UnityEngine;
//using DiceOrbit.Core;

//namespace DiceOrbit.Data.MonsterAI.Patterns
//{
//    /// <summary>
//    /// MonsterSkill와 가중치를 쌍으로 저장하는 구조체
//    /// </summary>
//    [System.Serializable]
//    public class WeightedSkill
//    {
//        [Tooltip("Skill to use")]
//        public MonsterSkill Skill;

//        [Tooltip("Weight of this skill (higher = more likely to be selected)")]
//        [Range(0.1f, 100f)] 
//        public float Weight = 1f;
//    }

//    /// <summary>
//    /// 가중치 랜덤 패턴 (MonsterSkill 직접 참조 방식)
//    /// </summary>
//    [CreateAssetMenu(fileName = "Weighted Random Pattern", menuName = "Dice Orbit/Monster AI/Pattern (Weighted Random)")]
//    public class WeightedRandomPattern : MonsterAI
//    {
//        [SerializeField] private List<WeightedSkill> weightedSkills = new List<WeightedSkill>();

//        protected override void InitializeRuntimeState()
//        {
//            base.InitializeRuntimeState();

//            // Deep copy weightedSkills list to avoid sharing with original ScriptableObject
//            if (weightedSkills != null && weightedSkills.Count > 0)
//            {
//                var originalSkills = weightedSkills;
//                weightedSkills = new List<WeightedSkill>(originalSkills.Count);

//                foreach (var ws in originalSkills)
//                {
//                    weightedSkills.Add(new WeightedSkill
//                    {
//                        Skill = ws.Skill, // MonsterSkill는 ScriptableObject이므로 참조 공유 OK
//                        Weight = ws.Weight
//                    });
//                }
//            }
//        }

//        public override MonsterSkill GetNextSkill()
//        {
            
//        }
//    }
//}
