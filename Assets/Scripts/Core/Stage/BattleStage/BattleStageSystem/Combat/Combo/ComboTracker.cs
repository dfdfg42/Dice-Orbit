namespace DiceOrbit.Core.Combo
{
    /// <summary>콤보 상태 변화의 이유 — UI 피드백과 로그가 구분해 쓴다.</summary>
    public enum ComboOutcome
    {
        Advanced,         // 1·2단계 성공 → 다음 단계로 전진
        Finished,         // 3단계 발동 완료 → 0으로 순환
        BrokenByDice,     // 조건 불만족 주사위 사용 → 즉시 초기화 + 기본공격
        BrokenByNoTarget, // 게이트는 맞았지만 유효 표적이 없어 강화공격 미발생 → 초기화
        BrokenByNoMove,   // 이동 없이 턴 종료 → 초기화
        ResetBySystem,    // 전투 시작/종료·사망 등 시스템 초기화
    }

    /// <summary>
    /// 캐릭터 1명분 콤보 카운터 (순수 로직 — 자가 테스트 대상).
    /// Stage는 '다음 게이트 성공 시 사용할 단계 인덱스'(0~2)다.
    /// 실행 확정(ConfirmExecuted) 시에만 전진하고, 3단계를 쓰고 나면 0으로 돌아간다.
    /// </summary>
    public class ComboTracker
    {
        public const int StageCount = 3;

        public int Stage { get; private set; }

        /// <summary>게이트 성공 — 이번 공격에 사용할 단계 인덱스(0~2)를 반환한다. 전진은 ConfirmExecuted에서.</summary>
        public int RegisterGateSuccess() => Stage;

        /// <summary>강화공격이 실제로 발생했다 — 단계를 전진시킨다. 3단계였다면 0으로 초기화.</summary>
        public ComboOutcome ConfirmExecuted()
        {
            if (Stage >= StageCount - 1)
            {
                Stage = 0;
                return ComboOutcome.Finished;
            }
            Stage++;
            return ComboOutcome.Advanced;
        }

        public void Reset() => Stage = 0;
    }
}
