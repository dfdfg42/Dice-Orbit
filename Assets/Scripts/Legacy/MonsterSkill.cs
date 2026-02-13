//using UnityEngine;
//using DiceOrbit.Core;

//namespace DiceOrbit.Data.MonsterAI
//{
//    [CreateAssetMenu(fileName = "New Monster Skill", menuName = "Dice Orbit/Monster AI/Skill")]
//    public class MonsterSkill : ScriptableObject
//    {
//        [Header("Skill Info")]
//        public string SkillName;
//        public IntentType Type;
//        [TextArea] public string Description;

//        [Header("Targeting")]
//        public TargetType TargetType = TargetType.Single;
//        [Tooltip("Radius for Area attacks. 0 = Single Tile, 1 = 3 Tiles (+/-1), etc.")]
//        [Range(0, 5)] public int AreaRadius = 0;

//        [Header("Combat Stats")]
//        public int BaseDamage;
//        [Tooltip("Multiplier based on Monster's Attack stat (e.g., 1.0 = 100% Attack)")]
//        public float DamageMultiplier = 1.0f;
//        public int HitCount = 1;

//        /// <summary>
//        /// Calculates the final damage based on the monster's stats.
//        /// </summary>
//        public int CalculateDamage(MonsterStats stats)
//        {
//            float damage = BaseDamage + (stats.Attack * DamageMultiplier);
//            return Mathf.FloorToInt(damage);
//        }

//        public AttackIntent CreateIntent(MonsterStats stats)
//        {
//            int damage = CalculateDamage(stats);
//            return new AttackIntent(Type, damage, Description)
//            {
//                TargetType = this.TargetType,
//                AreaRadius = this.AreaRadius,
//                HitCount = this.HitCount
//            };
//        }
//    }
//}
