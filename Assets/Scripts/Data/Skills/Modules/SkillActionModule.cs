using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Skills.Modules
{
    public abstract class SkillActionModule : ScriptableObject
    {
        public abstract void Execute(Character source, GameObject target, int diceValue);
    }
}
