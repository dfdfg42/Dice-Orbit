using System;
using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Skills;

namespace DiceOrbit.Data.Modifiers
{
    public enum ModifierCategory
    {
        Signature,
        Generic,
    }

    /// <summary>
    /// 런 중 캐릭터에 장착되는 모디파이어의 추상 베이스.
    /// Signature: 특정 스킬 RuntimeInstance를 OnEquipped에서 직접 패치.
    /// Generic: OnReact에서 파이프라인 훅.
    /// </summary>
    [Serializable]
    public abstract class CharacterModifier: ICombatReactor
    {
        public abstract string           ModifierName { get; }
        public abstract string           Description  { get; }
        public virtual  Sprite           Icon         => null;
        public abstract ModifierCategory Category     { get; }

        /// <summary>계열 — 보상 카드의 아이콘·라벨 (2026-10-03). 공용 12종은 전부 지정, 기본은 None(보상 카드로 제시 불가).</summary>
        public virtual  ModifierFamily   Family       => ModifierFamily.None;

        // 패시브 50~100, 모디파이어 10~30 대역
        public virtual int Priority => 10;

        public Character owner;

        /// <summary>파이프라인 훅. Generic은 여기서 OutputValue 조작.</summary>
        public virtual void OnAttack(CombatTrigger trigger, AttackContext context) {
            if (context.SourceUnit != owner) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            OnAttackWithActive(context);
        }

        protected virtual void OnAttackWithActive(AttackContext context) {

        }

        // ⚠️ 훅은 반드시 인터페이스를 나열한 이 베이스에 virtual로 선언해야 한다 — override 없는
        // public void 선언은 DIM 매핑에서 빠져 절대 호출되지 않는다 (2026-07-21 죽은 훅 사고와 동일 패턴).
        /// <summary>턴/전투 사건 훅 (전투 시작 방어도, 상태 초기화 등).</summary>
        public virtual void OnTurnEvent(CombatTrigger trigger, TurnEventContext context) { }

        /// <summary>모디파이어 장착/해제 시 스킬 컨텍스트 갱신 훅.</summary>
        public virtual void OnRefreshSkill(CharacterModfierContext context) { }

        /// <summary>
        /// 스킬 툴팁에 표시할 설명 줄.
        /// 이 모디파이어가 해당 스킬과 관련 없으면 string.Empty 반환.
        /// 같은 타입이 여러 개 장착된 경우 UI에서 (×N)으로 그룹핑됨.
        /// </summary>
        public virtual string GetSkillLine(CharacterActiveSkill skill) => string.Empty;

        /// <summary>
        /// 이 모디파이어를 해당 캐릭터에게 줄 수 있는지(보상 제시/장착 가능 여부).
        /// 기본은 모든 캐릭터(Generic). 특정 스킬에만 작동하는 시그니처는 override해서 제한한다.
        /// </summary>
        public virtual bool CanApplyTo(Character character) => true;
    }
}
