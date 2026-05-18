using UnityEngine;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;

namespace DiceOrbit.Systems.Artifact
{
    public abstract class RuntimeArtifact : ICombatReactor
    {
        public ArtifactData data;
        public virtual int Priority => 11;

        public RuntimeArtifact(ArtifactData data)
        {
            this.data = data;
        }

        public abstract void OnReact(CombatTrigger trigger, CombatContext context);
    }
}
