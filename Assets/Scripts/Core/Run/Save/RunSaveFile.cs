using System.IO;
using UnityEngine;

namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 세이브 파일 계층 전담. 매니저도 서비스도 File API를 직접 만지지 않는다.
    /// </summary>
    public static class RunSaveFile
    {
        public const int CurrentVersion = 2;

        private static string Dir         => Application.persistentDataPath;
        private static string SavePath    => Path.Combine(Dir, "run_save.json");
        private static string BakPath     => Path.Combine(Dir, "run_save.bak");
        private static string TmpPath     => Path.Combine(Dir, "run_save.tmp");
        private static string CorruptPath => Path.Combine(Dir, "run_save.corrupt.json");

        /// <summary>임시 파일에 먼저 쓰고 교체한다 — 저장 중 크래시해도 기존 세이브가 살아남는다.</summary>
        public static void Write(RunSaveData data)
        {
            try
            {
                File.WriteAllText(TmpPath, JsonUtility.ToJson(data, true));

                if (!File.Exists(SavePath))
                {
                    File.Move(TmpPath, SavePath);
                }
                else
                {
                    try
                    {
                        File.Replace(TmpPath, SavePath, BakPath);   // 원자적 교체 + 백업
                    }
                    catch (System.Exception)
                    {
                        // File.Replace는 플랫폼에 따라 동작이 다르다 (일부 파일시스템 미지원).
                        if (File.Exists(BakPath)) File.Delete(BakPath);
                        File.Move(SavePath, BakPath);
                        File.Move(TmpPath, SavePath);
                    }
                }

                Debug.Log($"[RunSave] 저장됨 — 노드 {data.Progress.CurrentNodeId}, 파티 {data.Party.Count}명");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RunSave] 저장 실패: {ex.Message}");
            }
        }

        /// <summary>3단 방어: 본 파일 → .bak → 손상 파일 보존 후 null.</summary>
        public static RunSaveData Read(RestoreReport report)
        {
            var data = TryParse(SavePath);
            if (data != null) return data;

            data = TryParse(BakPath);
            if (data != null)
            {
                report?.Warn("본 세이브가 손상되어 .bak에서 복구했습니다.");
                return data;
            }

            // 손상 파일을 지우지 않고 남긴다 — 개발 중 재현을 위해서다.
            if (File.Exists(SavePath))
            {
                try
                {
                    if (File.Exists(CorruptPath)) File.Delete(CorruptPath);
                    File.Move(SavePath, CorruptPath);
                    report?.Fail($"세이브를 읽을 수 없습니다. 손상 파일을 {CorruptPath}에 보존했습니다.");
                }
                catch (System.Exception ex)
                {
                    report?.Fail($"세이브를 읽을 수 없고 손상 파일 보존도 실패했습니다: {ex.Message}");
                }
            }
            else
            {
                report?.Fail("세이브 파일이 없습니다.");
            }
            return null;
        }

        /// <summary>파일 존재만이 아니라 파싱과 버전 검사까지 통과해야 true.</summary>
        public static bool HasValidSave() => TryParse(SavePath) != null || TryParse(BakPath) != null;

        public static void Delete()
        {
            SafeDelete(SavePath);
            SafeDelete(BakPath);
            SafeDelete(TmpPath);
        }

        private static RunSaveData TryParse(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(path));
                if (data == null) return null;
                if (data.Version != CurrentVersion) return null;   // 마이그레이션 없음 — 폐기
                return data;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (System.Exception ex) { Debug.LogWarning($"[RunSave] 삭제 실패 {path}: {ex.Message}"); }
        }
    }
}
