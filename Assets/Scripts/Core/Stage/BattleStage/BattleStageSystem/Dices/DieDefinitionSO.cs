using UnityEngine;
using DiceOrbit.Core;   // SubclassPickerAttribute

namespace DiceOrbit.Data
{
    /// <summary>주사위 등급 — 보상 드로우 가중치와 툴팁 이름 색을 정한다 (2026-08-28).</summary>
    public enum DieRarity
    {
        Common,     // 일반
        Advanced,   // 고급
        Rare,       // 희귀
    }

    /// <summary>
    /// 주사위 한 종류 = 에셋 1개. 표준(면 1~6, 효과 없음) 및 특수 주사위 모두 이 SO로 만든다.
    /// saveId = 에셋 파일명 (후속 세이브 리팩터의 SaveIdCatalog와 정합).
    /// </summary>
    [CreateAssetMenu(fileName = "Die", menuName = "DiceOrbit/Die Definition")]
    public class DieDefinitionSO : ScriptableObject
    {
        public string Name = "표준 주사위";
        public int[] Faces = { 1, 2, 3, 4, 5, 6 };       // 길이 6 권장
        [SerializeReference, SubclassPicker] public DieEffect Effect;  // null = 효과 없음
        public Sprite Icon;                              // 보상/교체 목록 표시용

        [Tooltip("보상 드로우 가중치·툴팁 이름 색 (일반 6 : 고급 3 : 희귀 1)")]
        public DieRarity Rarity = DieRarity.Common;

        [Tooltip("툴팁 보조 설명 — 조건 성공률·면 구성의 특징 등 (비우면 표시 안 함)")]
        [TextArea(1, 3)] public string Description;

        public int RollFace()
            => (Faces != null && Faces.Length > 0) ? Faces[Random.Range(0, Faces.Length)] : 1;

        /// <summary>등급 한국어 표기.</summary>
        public string RarityLabel
            => Rarity switch { DieRarity.Advanced => "고급", DieRarity.Rare => "희귀", _ => "일반" };

        /// <summary>등급 색 (일반 잉크/고급 파랑/희귀 보라).</summary>
        public Color RarityColor
            => Rarity switch
            {
                DieRarity.Advanced => new Color(0.25f, 0.5f, 0.9f),
                DieRarity.Rare     => new Color(0.62f, 0.35f, 0.85f),
                _                  => new Color(0.12f, 0.11f, 0.16f),
            };
    }

    /// <summary>
    /// 덱의 한 칸 (런타임). 베이스 주사위(SO) + 이벤트로 붙은 효과/면 변형(선택).
    /// 교체 = BaseDie 스왑 / 효과 부여 = AttachedEffect / 면 변형 = FaceOverride.
    /// </summary>
    public class DieInstance
    {
        public DieDefinitionSO BaseDie;
        public DieEffect AttachedEffect;   // 없으면 BaseDie.Effect 사용
        public int[] FaceOverride;         // 이벤트로 변형된 면 (null = 원본)

        public DieInstance(DieDefinitionSO baseDie) { BaseDie = baseDie; }

        public int[] Faces => FaceOverride ?? (BaseDie != null ? BaseDie.Faces : System.Array.Empty<int>());
        public DieEffect Effect => AttachedEffect ?? BaseDie?.Effect;

        /// <summary>인스턴스 면(변형 반영)에서 굴린다 — BaseDie 직행 금지.</summary>
        public int RollFace()
        {
            var faces = Faces;
            return faces.Length > 0 ? faces[Random.Range(0, faces.Length)] : 1;
        }

        /// <summary>면 변형 준비 — 오버라이드가 없으면 원본 복사본 생성 후 반환.</summary>
        public int[] EnsureFaceOverride()
        {
            if (FaceOverride == null)
                FaceOverride = (int[])Faces.Clone();
            return FaceOverride;
        }
    }
}
