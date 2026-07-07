using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Visuals;

namespace DiceOrbit.Data.Skills
{
    public enum CharacterSkillTargetType
    {
        None,        // 타겟 없음, 즉시 실행
        OneEnemy,    // 단일 적
        AllEnemies,  // 모든 적
        OneAlly,     // 단일 아군
        AllAllies,   // 모든 아군
        OneTile,     // 단일 타일
        AllTiles,    // 모든 타일
        MultiEnemy,  // N명 적 순차 선택
        MultiAlly,   // N명 아군 순차 선택
        MultiTile,   // N개 타일 순차 선택
    }

    [System.Serializable]
    public abstract class CharacterActiveSkill
    {
        [Header("Info")]
        [SerializeField] protected string skillName = "";
        [SerializeField, TextArea(2, 4)] protected string description = "";
        [SerializeField] public Sprite icon;

        [Header("Requirement")]
        [SerializeField] public DiceRequirement requirement = new DiceRequirement();

        [Header("Targeting")]
        [SerializeField] public CharacterSkillTargetType targetType = CharacterSkillTargetType.OneEnemy;
        [SerializeField] public TilePreviewStyle previewStyle = TilePreviewStyle.Neutral;
        [Tooltip("MultiEnemy/MultiAlly/MultiTile 타입일 때 선택할 개수")]
        [SerializeField] public int targetCount = 1;

        [Header("VFX")]
        [SerializeField] protected CombatVfxProfile vfxProfile;

        public string SkillName   => skillName;
        public string Description => description;

        public CharacterSkillTargetType TargetType => targetType;
        public TilePreviewStyle PreviewStyle       => previewStyle;
        public int              TargetCount        => Mathf.Max(1, targetCount);
        public CombatVfxProfile VfxProfile         => vfxProfile;

        public bool CanUse(int diceValue) => requirement.CanUse(diceValue);

        /// <summary>
        /// 표시용 동적 설명. 현재 유효 수치(배율/조건)에서 매번 생성하므로
        /// 모디파이어 등이 값을 바꾸면 자동으로 반영된다. 기본은 정적 Description.
        /// </summary>
        public virtual string GetDynamicDescription() => Description;

        /// <summary>주사위 요구 조건을 한국어 문구로 변환. (정보 패널 등 UI 표시용으로도 사용)</summary>
        public string FormatDiceCondition()
        {
            if (requirement == null) return string.Empty;

            if (requirement.ExactDiceValue.HasValue)
                return $"주사위 {requirement.ExactDiceValue.Value}";

            switch (requirement.Pattern)
            {
                case DicePattern.Odd:  return "홀수 주사위";
                case DicePattern.Even: return "짝수 주사위";
                case DicePattern.High: return "주사위 4 이상";
                case DicePattern.Low:  return "주사위 3 이하";
            }

            int min = requirement.MinDiceValue;
            var max = requirement.MaxDiceValue;
            if (max.HasValue && min > 1) return $"주사위 {min}~{max.Value}";
            if (max.HasValue)            return $"주사위 {max.Value} 이하";
            if (min > 1)                 return $"주사위 {min} 이상";
            return "주사위";
        }

        public virtual CharacterActiveSkill Clone() => (CharacterActiveSkill)MemberwiseClone();

        /// <summary>
        /// 이 스킬에 맞는 캐싱용 컨텍스트를 생성하여 반환합니다.
        /// 파생 클래스에서 오버라이드하여 전용 컨텍스트를 생성할 수 있습니다.
        /// </summary>
        public virtual CharacterModfierContext GenerateContext(Character source, ActiveSkillSlot ability)
        {
            return new CharacterModfierContext(source, this);
        }

        public abstract int    CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue);
        public abstract string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue);

        public virtual bool Execute(
            Character source, ActiveSkillSlot ability,
            List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            if (source == null || ability == null) return false;

            int rawDamage = CalculateRawDamage(source, ability, diceValue);
            if (rawDamage <= 0 || targets == null)
            {
                OnAfterResolved(source, ability);
                return true;
            }

            VfxManager.PlayCast(vfxProfile, source);

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                var context = new AttackContext(source, target, skillName, rawDamage);
                context.VfxProfile = vfxProfile;   // 재생 판단은 파이프라인 ApplyAction 한 곳에서
                CombatPipeline.Instance?.Process(context);
            }

            OnAfterResolved(source, ability);
            return true;
        }

        public virtual void OnAfterResolved(Character source, ActiveSkillSlot ability) { }
    }
}
