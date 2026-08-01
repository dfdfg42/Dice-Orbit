namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 매니저가 자기 상태의 저장·복원을 직접 소유하기 위한 계약.
    ///
    /// Validate/Apply 2단계 분리가 핵심이다. 검증은 아무것도 바꾸지 않고 "이 세이브의 모든 ID를
    /// 카탈로그·레지스트리에서 해결할 수 있는가"만 확인한다. 참가자 전원이 통과했을 때만 Apply로
    /// 넘어가므로 "골드는 넣었는데 파티 스폰 중 실패" 같은 중간 상태가 생기지 않는다 — 롤백 코드가 없다.
    /// </summary>
    public interface IRunSaveParticipant
    {
        /// <summary>현재 상태 → DTO.</summary>
        void Capture(RunSaveData data);

        /// <summary>부작용 없이 해결 가능성만 확인. 실패는 ctx.Report.Fail에 기록한다.</summary>
        void Validate(RunSaveData data, RunRestoreContext ctx);

        /// <summary>참가자 전원이 검증을 통과한 뒤에만 호출된다.</summary>
        void Apply(RunSaveData data, RunRestoreContext ctx);
    }
}
