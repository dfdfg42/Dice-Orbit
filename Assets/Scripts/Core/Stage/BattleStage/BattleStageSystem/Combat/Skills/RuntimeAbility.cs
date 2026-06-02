using System;
using System.Collections.Generic;
using DiceOrbit.Core;
using UnityEngine;

namespace DiceOrbit.Data.Skills
{
    [Serializable]
    public class ActiveSkillSlot
    {
        [SerializeReference] public CharacterActiveSkill BaseSkill;
        public int CurrentLevel;

        [NonSerialized] public CharacterActiveSkill RuntimeInstance;
        // 유효 타게팅(모디파이어 반영) 계산을 위해 소유 캐릭터 참조. Character.InitializeStats에서 주입.
        [NonSerialized] public Character Owner;

        public ActiveSkillSlot(CharacterActiveSkill skill, int initialLevel = 1)
        {
            BaseSkill       = skill;
            int max         = skill != null ? Mathf.Max(1, skill.MaxLevel) : 1;
            CurrentLevel    = Mathf.Clamp(initialLevel, 1, max);
            RuntimeInstance = skill?.Clone();
        }

        /// <summary>스킬 기본 컨텍스트를 만들고 소유자의 모디파이어를 적용한 "유효 컨텍스트"를 생성.</summary>
        public Core.Pipeline.CharacterModfierContext BuildEffectiveContext()
        {
            var skill = RuntimeInstance ?? BaseSkill;
            if (skill == null) return null;

            var ctx = skill.GenerateContext(Owner, this);
            Owner?.Stats?.Modifiers?.ApplyTo(ctx);
            return ctx;
        }

        // 타게팅 값은 모디파이어가 반영된 유효값을 노출 (읽는 쪽 코드는 무변경으로 자동 반영됨).
        public CharacterSkillTargetType TargetType
            => BuildEffectiveContext()?.TargetType ?? BaseSkill?.TargetType ?? CharacterSkillTargetType.None;
        public Visuals.TilePreviewStyle PreviewStyle
            => BuildEffectiveContext()?.PreviewStyle ?? BaseSkill?.PreviewStyle ?? Visuals.TilePreviewStyle.Neutral;
        public int TargetCount
            => BuildEffectiveContext()?.TargetCount ?? BaseSkill?.TargetCount ?? 1;

        public string          GetDescription() => BaseSkill?.Description ?? string.Empty;
        public DiceRequirement GetRequirement() => BaseSkill?.requirement;

        public bool IsMaxLevel => BaseSkill == null || CurrentLevel >= BaseSkill.MaxLevel;

        public bool TryUpgrade()
        {
            if (BaseSkill == null || IsMaxLevel) return false;
            CurrentLevel++;
            return true;
        }

        public bool CanUse(int diceValue) => BaseSkill?.CanUse(diceValue) ?? false;

        public bool Execute(Character source, List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            if (RuntimeInstance != null)
                return RuntimeInstance.Execute(source, this, targets, targetTiles, diceValue);
            return false;
        }

        public string BuildPreview(Character source, int diceValue)
        {
            if (RuntimeInstance != null)
                return RuntimeInstance.BuildPreview(source, this, diceValue);
            return "예상: -";
        }
    }
}
