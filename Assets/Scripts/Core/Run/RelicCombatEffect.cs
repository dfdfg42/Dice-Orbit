using DiceOrbit.Core.Pipeline;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 유물의 전투 반응 효과 — CombatPipeline 리액터 (구 Artifact 시스템의 RuntimeArtifact 계승, 2026-07 통합).
    /// RelicDefinition에 [SerializeReference]로 인라인 (스킬/패시브와 같은 패턴).
    /// 서브클래스는 OnAttack/OnHeal/OnMove/OnTurnEvent 훅 중 필요한 것만 구현한다.
    /// </summary>
    [System.Serializable]
    public abstract class RelicCombatEffect : ICombatReactor
    {
        public virtual int Priority => 11;   // 구 RuntimeArtifact 우선순위 승계 (패시브 50~100 뒤, 모디파이어 10~30 대역)
    }

    /// <summary>
    /// 구 PowerfullPunch 이식 (디버그용 예시): 캐릭터 공격의 출력을 고정값으로.
    /// </summary>
    [System.Serializable]
    public class PowerfulPunchEffect : RelicCombatEffect
    {
        public int fixedOutput = 1000;

        public void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit is not Character) return;
            Debug.Log("[Relic] PowerfulPunch react");
            context.OutputValue = fixedOutput;
        }
    }
}
