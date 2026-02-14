using UnityEngine;

namespace DiceOrbit.Data
{
    [CreateAssetMenu(fileName = "MonsterSkill", menuName = "Scriptable Objects/MonsterSkill")]
    public class MonsterSkill : ScriptableObject
    {
        public SkillData skillData; // ❌ get-only property
    }
}

