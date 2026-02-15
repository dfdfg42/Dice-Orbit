using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// 패시브 타입
    /// </summary>
    [System.Serializable]
    public class PassiveType
    {
        public string Name;
        public bool IsStackable;
    }

    /// <summary>
    /// 모든 패시브의 기본 클래스
    /// ScriptableObject로 정의되며, ICombatReactor를 구현합니다.
    /// </summary>
    public abstract class PassiveAbility : ScriptableObject, ICombatReactor
    {
        public string PassiveName;
        [TextArea] protected string basic_description= "패시브 설명";
        public PassiveType type;
        public Sprite Icon;
        protected Unit owner;

        // 우선순위 (기본 0, 높으면 먼저 실행)
        public virtual int Priority => 0;

        public virtual string Description()
        {
            return basic_description;
        }
        /// <summary>
        /// 초기화 (필요 시)
        /// </summary>
        public virtual void Initialize(Unit Owner) { 
            owner = Owner;
        }

        /// <summary>
        /// 전투 파이프라인 반응 로직
        /// </summary>
        public abstract void OnReact(CombatTrigger trigger, CombatContext context);

        /// <summary>
        /// 같은 패시브가 중첩될 때 처리 로직 (예: 지속시간 연장, 효과 강화 등)
        /// </summary>
        public virtual bool AllowSameSkill(PassiveAbility incoming) 
        {
            return true; // 기본적으로 중첩 허용, 필요에 따라 오버라이드하여 중첩 방지 또는 다른 처리 로직 구현
        }
    }

    /// <summary>
    /// 정수값 파라미터를 받아 초기화가 필요한 패시브들이 구현해야 할 인터페이스
    /// </summary>
    public interface IIntValueReceiver
    {
        void SetValue(int value);
    }

    /// <summary>
    /// 두 개의 정수값 파라미터를 받아 초기화가 필요한 패시브들이 구현
    /// </summary>
    public interface ITwoIntValuesReceiver
    {
        void SetValues(int v1, int v2);
    }

    /// <summary>
    /// 다른 패시브 참조와 정수값 하나를 받아 초기화가 필요한 패시브들이 구현 (예: 시너지 효과)
    /// </summary>
    public interface IPassiveReferenceReceiver
    {
        void SetReference(PassiveAbility passiveRef, int value);
    }
}
