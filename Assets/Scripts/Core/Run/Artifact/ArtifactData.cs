using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 유물 에셋: 표시 데이터 + 로직 프로토타입 (스펙 2026-07-21).
    /// effect에 SubclassPicker로 유물 클래스를 고르고 파라미터를 인라인 튜닝한다.
    /// 획득 시 effect.CreateInstance(this)로 런타임 인스턴스가 만들어진다.
    /// </summary>
    [CreateAssetMenu(fileName = "New ArtifactData", menuName = "DiceOrbit/ArtifactData")]
    public class ArtifactData : ScriptableObject
    {
        public string artifactName = "유물 이름";
        [TextArea(2, 4)] public string artifactTooltip = "유물 설명";
        public Sprite artifactIcon;
        [Min(1)] public int shopPrice = 120;

        [Header("효과 — 유물 1개 = 클래스 1개 (Data/Artifacts/)")]
        [SerializeReference, SubclassPicker] public RuntimeArtifact effect;
    }
}
