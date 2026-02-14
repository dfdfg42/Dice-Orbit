using UnityEngine;
using System.Collections.Generic;
namespace DiceOrbit.Data
{
    public class CharacterSkillData : SkillData
    {
        [Header("Requirements")]
        public DiceRequirement Requirement = new DiceRequirement();

        /// <summary>
        /// 주사위 값으로 스킬 사용 가능한지 확인
        /// </summary>
        public bool CanUse(int diceValue)
        {
            return Requirement.CanUse(diceValue);
        }
    }
    }

