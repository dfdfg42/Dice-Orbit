using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Passives;

namespace DiceOrbit.Data
{
    [System.Serializable]
    public class CharacterStats : UnitStats
    {
        [Header("Basic Info")]
        public string CharacterName = "Hero";

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

        // (캐릭터 레벨/HP 성장 커브는 철거됨 — 성장은 전부 모디파이어로, 기획 REV05)

        public bool canMove() => BindDebuff == 0;
    }
}
