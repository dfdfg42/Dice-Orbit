using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Skills.Modules
{
    public abstract class SkillActionModule : ScriptableObject
    {
        [TextArea] public string TooltipDescription;

        public abstract void Execute(Character source, GameObject target, int diceValue);

        public virtual string GetTooltipDescription()
        {
            return TooltipDescription;
        }
    }
}
