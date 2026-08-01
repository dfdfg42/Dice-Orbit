using System.Collections.Generic;
using System.Text;

namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 복원 중 수집한 실패·경고. 실패가 하나라도 있으면 복원을 포기한다.
    ///
    /// saveId 도입 이후 복원 실패는 플레이어 상황이 아니라 개발 중 버그를 뜻한다.
    /// 조용히 반쪽으로 복원되면 그 버그를 놓치므로, "이어할 수 없습니다"가 정직하다.
    /// </summary>
    public class RestoreReport
    {
        public readonly List<string> Failures = new List<string>();   // 하나라도 있으면 복원 포기
        public readonly List<string> Warnings = new List<string>();   // 진행을 막지 않음

        public bool Success => Failures.Count == 0;

        public void Fail(string message) => Failures.Add(message);
        public void Warn(string message) => Warnings.Add(message);

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var f in Failures) sb.AppendLine("[실패] " + f);
            foreach (var w in Warnings) sb.AppendLine("[경고] " + w);
            return sb.Length == 0 ? "(문제 없음)" : sb.ToString().TrimEnd();
        }
    }
}
