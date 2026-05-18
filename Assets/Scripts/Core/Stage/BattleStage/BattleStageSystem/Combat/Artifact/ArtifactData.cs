using UnityEngine;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;

namespace DiceOrbit.Systems.Artifact
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "New ArtifactData", menuName = "DiceOrbit/ArtifactData")]
    public class ArtifactData : ScriptableObject
    {
        public string artifactName = "유물 이름";
        public string artifactTooltip = "유물 설명";
        public Sprite atifactIcon;
    }
}
