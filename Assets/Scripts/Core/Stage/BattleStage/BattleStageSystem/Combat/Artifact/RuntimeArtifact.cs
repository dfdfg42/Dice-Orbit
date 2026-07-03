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

        // OnReact는 ICombatReactor의 기본 디스패치(DIM)를 사용. 자식은 OnAttack 등 훅을 구현.
    }
}
