using UnityEngine;
using DiceOrbit.Data; // EffectType 등이 있다면 필요

namespace DiceOrbit.Core.Pipeline
{
    /// <summary>
    /// 파이프라인을 통과하며 데이터를 공유하는 컨텍스트
    /// </summary>
    public class CombatContext
    {
        public Core.Character Source;   // 시전 주체 (Monster일수도 있음 -> Monster/Character Base 공통 부모가 없다면 object나 Interface 필요)
        public object Target;           // 타겟 (Character or Monster)
        
        public CombatAction Action;     // 수행 중인 액션
        public float OutputValue;       // 최종 결과값 (데미지/힐량) 변조 가능

        public bool IsCancelled;        // 액션 취소 여부

        // 생성자
        public CombatContext(Core.Character source, object target, CombatAction action)
        {
            Source = source;
            Target = target;
            Action = action;
            OutputValue = action.BaseValue;
            IsCancelled = false;
        }

        // 몬스터 시전자를 위한 생성자 오버로딩 또는 공통 인터페이스 필요
        // 현재는 편의상 Core.Character를 Source로 두었으나, Monster도 공격하므로 구체화 필요.
        // 임시로 Source를 object로 두거나 IUnit 인터페이스를 도입하면 좋음.
        // 여기서는 편의성을 위해 object로 일반화하거나 별도 필드 추가.
        public object SourceUnit; // (Character or Monster)

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
