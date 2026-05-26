using System;
using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Skills;

namespace DiceOrbit.Data.Modifiers
{
    /// <summary>
    /// 런 중 캐릭터에 장착되는 모디파이어의 추상 베이스.
    /// Signature: 특정 스킬 RuntimeInstance를 OnEquipped에서 직접 패치.
    /// Generic: OnReact에서 파이프라인 훅.
    /// </summary>
    [Serializable]
    public abstract class CharacterModifier : ICombatReactor
    {
        public abstract string           ModifierName { get; }
        public abstract string           Description  { get; }
        public virtual  Sprite           Icon         => null;
        public abstract ModifierCategory Category     { get; }

        // 패시브 50~100, 모디파이어 10~30 대역
        public virtual int Priority => 10;

        protected Character owner;

        /// <summary>장착 시: RuntimeInstance 패치 또는 owner 등록.</summary>
        public virtual void OnEquipped(Character character)   { owner = character; }

        /// <summary>해제 시: 패치 원복. 씬 전환/상점 제거 대비.</summary>
        public virtual void OnUnequipped(Character character) { owner = null; }

        /// <summary>파이프라인 훅. Generic은 여기서 OutputValue 조작.</summary>
        public virtual void OnReact(CombatTrigger trigger, CombatContext context) { }

        /// <summary>모디파이어 장착/해제 시 스킬 컨텍스트 갱신 훅.</summary>
        public virtual void OnRefreshSkill(ModifiedSkillContext context) { }

        /// <summary>
        /// 스킬 툴팁에 표시할 설명 줄.
        /// 이 모디파이어가 해당 스킬과 관련 없으면 string.Empty 반환.
        /// 같은 타입이 여러 개 장착된 경우 UI에서 (×N)으로 그룹핑됨.
        /// </summary>
        public virtual string GetSkillLine(CharacterActiveSkill skill) => string.Empty;
    }
}
