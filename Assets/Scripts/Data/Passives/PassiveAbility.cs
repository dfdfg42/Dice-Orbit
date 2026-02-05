using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// 모든 패시브의 기본 클래스
    /// ScriptableObject로 정의되며, ICombatReactor를 구현합니다.
    /// </summary>
    public abstract class PassiveAbility : ScriptableObject, ICombatReactor
    {
        public string PassiveName;
        [TextArea] public string Description;
        public Sprite Icon;

        // 우선순위 (기본 0, 높으면 먼저 실행)
        public virtual int Priority => 0;

        /// <summary>
        /// 초기화 (필요 시)
        /// </summary>
        public virtual void Initialize(Core.Character owner) { }

    /// <summary>
    /// 몬스터 초기화 (필요 시)
    /// </summary>
    public virtual void Initialize(Core.Monster owner) { }

        /// <summary>
        /// 전투 파이프라인 반응 로직
        /// </summary>
        public abstract void OnReact(CombatTrigger trigger, CombatContext context);
    }
}
