using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// GitHub Pages용 WebGL 헤드리스 빌드.
    /// 사용: Unity.exe -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod DiceOrbit.EditorTools.WebGLBuilder.BuildForPages
    /// 출력: Builds/WebGL/
    ///
    /// GitHub Pages는 .br/.gz에 Content-Encoding 헤더를 붙여주지 않으므로
    /// Decompression Fallback(로더가 JS로 해제)을 반드시 켠다.
    /// </summary>
    public static class WebGLBuilder
    {
        private const string OutputDir = "Builds/WebGL";

        [MenuItem("Tools/Build/WebGL (GitHub Pages)")]
        public static void BuildForPages()
        {
            // ── GitHub Pages 필수 설정 ─────────────────────────────
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;   // Pages가 Content-Encoding 미지원 → JS 해제
            PlayerSettings.runInBackground = true;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Debug.Log($"[WebGLBuilder] Building {scenes.Length} scenes → {OutputDir}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[WebGLBuilder] Result={summary.result} Size={summary.totalSize / (1024 * 1024)}MB " +
                      $"Errors={summary.totalErrors} Warnings={summary.totalWarnings} Time={summary.totalTime}");

            if (summary.result != BuildResult.Succeeded)
                throw new System.Exception($"[WebGLBuilder] Build failed: {summary.result} ({summary.totalErrors} errors)");
        }
    }
}
