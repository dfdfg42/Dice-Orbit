using UnityEngine;

namespace DiceOrbit.Core.Pipeline
{
    /// <summary>
    /// 전투 트리거 시점 정의
    /// </summary>
    public enum CombatTrigger
    {

        // 2. 액션 실행 전
        OnPreAction,        // 액션 준비 (실행 가능 여부, 비용 소모 확인)
        OnCalculateOutput,  // 데미지/힐량 계산 (버프/디버프/패시브 보정)
        
        // 3. 액션 실행 후
        OnHit,              // 적중 시 (방어, 반격)
        OnActionSuccess,    // 액션 성공 (흡혈, 처치 시 효과)
        OnPostAction        // 모든 처리 완료 후
    }

    /// <summary>
    /// 전투 상황에 반응하는 객체 레이어 (Passive, Effect, Equipment 등)
    /// </summary>
    public interface ICombatReactor
    {
        /// <summary>
        /// 특정 트리거가 발생했을 때 로직 수행
        /// </summary>
        void OnReact(CombatTrigger trigger, CombatContext context);
        
        /// <summary>
        /// 실행 우선순위 (높을수록 먼저 실행, 데미지 계산 시 중요)
        /// </summary>
        int Priority { get; }
    }
}
