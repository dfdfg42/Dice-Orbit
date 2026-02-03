using UnityEngine;
using DiceOrbit.Data; // EffectType 등이 있다면 필요

namespace DiceOrbit.Core.Pipeline
{
    /// <summary>
    /// 파이프라인을 통과하며 데이터를 공유하는 컨텍스트
    /// </summary>
    public class CombatContext
    {
        public object SourceUnit; // (Character or Monster)
        public object Target;     // (Character or Monster)
        
        // 편의 접근자
        public Core.Character SourceCharacter => SourceUnit as Core.Character;
        public Core.Monster SourceMonster => SourceUnit as Core.Monster;
        public Core.Character TargetCharacter => Target as Core.Character;
        public Core.Monster TargetMonster => Target as Core.Monster;

        public CombatAction Action;     // 수행 중인 액션
        public float OutputValue;       // 최종 결과값 (데미지/힐량)
        public bool IsCancelled;        // 액션 취소 여부

        // 생성자
        public CombatContext(object source, object target, CombatAction action)
        {
            SourceUnit = source;
            Target = target;
            Action = action;
            OutputValue = action.BaseValue;
            IsCancelled = false;
        }

        /// <summary>
        /// 상태이상을 타겟에게 부여하는 유틸리티 메서드 (일관성 확보)
        /// </summary>
        public void AddEffectToTarget(DiceOrbit.Data.EffectType type, int value, int duration)
        {
            // 타겟이 캐릭터인지 몬스터인지 확인 후 적용
            // 여기서 StatusEffectManager 호출 (구현 필요)
            // 예: StatusEffectManager.Instance.AddEffect(Target, type, value, duration);
            
            // 임시 로그
            Debug.Log($"[Pipeline] Request to add effect {type} (Val:{value}, Dur:{duration}) to {Target}");
        }
    }
}
