using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 저장·복원 오케스트레이션. 상태는 전부 참가자가 소유하고, 여기는 순서만 안다.
    /// 맵 화면 진입마다 자동 저장 → 사망/승리/새 게임 시 삭제.
    /// </summary>
    public static class RunSaveService
    {
        public static bool HasSave() => RunSaveFile.HasValidSave();
        public static void Delete()  => RunSaveFile.Delete();

        public static void SaveCurrent()
        {
            var run = RunManager.Instance;
            if (run == null || !run.RunActive) return;

            var participants = CollectParticipants(out string missing);
            if (participants == null)
            {
                // 기존 세이브를 덮어쓰지 않는다 — 반쪽 저장보다 직전 세이브 유지가 낫다.
                Debug.LogWarning($"[RunSave] 저장 중단 — {missing} 없음");
                return;
            }

            var data = new RunSaveData();
            foreach (var participant in participants) participant.Capture(data);
            RunSaveFile.Write(data);
        }

        public static RestoreReport RestoreCurrent()
        {
            var report = new RestoreReport();

            var data = RunSaveFile.Read(report);
            if (data == null) return report;

            var participants = CollectParticipants(out string missing);
            if (participants == null)
            {
                report.Fail($"{missing}이(가) 없어 복원할 수 없습니다.");
                return report;
            }

            var ctx = BuildContext(report);
            if (ctx.Catalog == null)
            {
                report.Fail(
                    "SaveIdCatalog를 로드하지 못했습니다 — Assets/Resources/SaveIdCatalog.asset을 확인하세요.");
                return report;
            }

            // 1단계 — 검증만. 첫 실패에서 멈추지 않고 전부 수집한다 (디버깅 편의).
            foreach (var participant in participants) participant.Validate(data, ctx);
            if (!report.Success) return report;   // 아무것도 적용되지 않은 상태

            // 2단계 — 적용
            foreach (var participant in participants) participant.Apply(data, ctx);
            return report;
        }

        /// <summary>
        /// 순서 = 복원 의존성 순서.
        /// FindObjectsByType 순회는 쓰지 않는다 — 순서가 비결정적이고, 인스턴스가 없으면
        /// 그 섹션이 '조용히' 누락된다. 씬 배치 매니저가 없으면 누락이 아니라 명시적 실패다.
        /// </summary>
        private static List<IRunSaveParticipant> CollectParticipants(out string missing)
        {
            missing = null;

            var run   = RunManager.Instance;      // 씬 배치 (EnsureInstance 없음)
            var party = PartyManager.Instance;    // 씬 배치 (EnsureInstance 없음)
            if (run == null)   { missing = nameof(RunManager);   return null; }
            if (party == null) { missing = nameof(PartyManager); return null; }

            return new List<IRunSaveParticipant>
            {
                run,                                // 맵·진행 — 가장 먼저
                GoldManager.EnsureInstance(),
                ArtifactManager.EnsureInstance(),
                PotionManager.EnsureInstance(),
                party,                              // 파티 스폰 — 맨 마지막
            };
        }

        private static RunRestoreContext BuildContext(RestoreReport report)
        {
            return new RunRestoreContext
            {
                Spawner = Object.FindFirstObjectByType<CharacterSpawner>(),
                Catalog = SaveIdCatalog.Get(),
                Report  = report,
            };
        }
    }
}
