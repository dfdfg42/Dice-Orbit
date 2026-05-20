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
        [SerializeField] public int maxLevel = 1;

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
        public virtual int MaxLevel => Mathf.Max(1, maxLevel);

        public CharacterSkillTargetType TargetType => targetType;
        public TilePreviewStyle PreviewStyle       => previewStyle;
        public int              TargetCount        => Mathf.Max(1, targetCount);
        public CombatVfxProfile VfxProfile         => vfxProfile;

        public bool CanUse(int diceValue) => requirement.CanUse(diceValue);

        public virtual CharacterActiveSkill Clone() => (CharacterActiveSkill)MemberwiseClone();

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

                var action = new CombatAction(skillName, ActionType.Attack, rawDamage);
                if (vfxProfile != null) action.AddTag("CustomVfx");

                var context = new CombatContext(source, target, action);
                CombatPipeline.Instance?.Process(context);

                if (context.IsEffected) VfxManager.PlayHit(vfxProfile, target);
            }

            OnAfterResolved(source, ability);
            return true;
        }

        public virtual void OnAfterResolved(Character source, ActiveSkillSlot ability) { }
    }
}
