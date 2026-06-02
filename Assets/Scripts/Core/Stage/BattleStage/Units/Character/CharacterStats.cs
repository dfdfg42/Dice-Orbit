using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data
{
    [System.Serializable]
    public class CharacterStats : UnitStats
    {
        private static readonly int[] MaxHpIncreaseByLevel =
        {
            10,10,10,10,10,10,10,10,10,10,
            10,10,10,10,10,10,10,10,10,10,
            5,5,5,5,5,5,5,5,5,5,
            5,5,5,5,5,
            3,3,3,3,3,3,3,3,3,3,
            2,2,2,2,2,
        };

        [Header("Basic Info")]
        public string CharacterName = "Hero";
        public int Level = 1;

        [Header("Skills")]
        public List<ActiveSkillSlot>   ActiveAbilities  = new List<ActiveSkillSlot>();
        public List<CharacterPassiveSkill> PassiveInstances = new List<CharacterPassiveSkill>();

        [Header("Reference")]
        public Core.CharacterPreset SourcePreset;

        [Header("Combat Stats")]
        public int MoveBuff    = 0;
        public int MoveDebuff  = 0;
        public int MoveOnThisTurn = 0;
        public int BindDebuff  = 0;

        [HideInInspector]
        public Data.Modifiers.IModifierManager Modifiers;

        public int ActiveAbilityCount => ActiveAbilities.Count;

        public ActiveSkillSlot GetActiveAbilityByIndex(int index)
        {
            if (index < 0 || index >= ActiveAbilities.Count) return null;
            return ActiveAbilities[index];
        }

        public void LevelUp()
        {
            int hpIncrease = GetMaxHpIncreaseForLevel(Level);
            Level++;
            MaxHP    += hpIncrease;
            CurrentHP += hpIncrease;
            Debug.Log($"{CharacterName} leveled up to {Level}! HP +{hpIncrease} => {MaxHP}");
        }

        public static int GetMaxHpIncreaseForLevel(int level)
        {
            return GetCurveValue(MaxHpIncreaseByLevel, level);
        }

        private static int GetCurveValue(int[] curve, int level)
        {
            if (curve == null || curve.Length == 0) return 0;
            int idx = Mathf.Clamp(Mathf.Max(1, level) - 1, 0, curve.Length - 1);
            return curve[idx];
        }

        public bool canMove() => BindDebuff == 0;
    }
}
