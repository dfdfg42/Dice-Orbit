#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>튜토리얼 반복 테스트용 — 완료 플래그 리셋 등.</summary>
    public static class TutorialDevTools
    {
        [MenuItem("Tools/Tutorial/튜토리얼 다시 보기 (tutorial_done 리셋)")]
        public static void ResetTutorial()
        {
            PlayerPrefs.DeleteKey("tutorial_done");
            PlayerPrefs.Save();
            Debug.Log("[Tutorial] tutorial_done 리셋 — 다음 '게임 시작'에 튜토리얼 프롬프트가 다시 뜹니다.");
        }

        [MenuItem("Tools/Tutorial/튜토리얼 봤음 처리 (tutorial_done=1)")]
        public static void MarkTutorialDone()
        {
            PlayerPrefs.SetInt("tutorial_done", 1);
            PlayerPrefs.Save();
            Debug.Log("[Tutorial] tutorial_done=1 — 프롬프트 없이 바로 게임 시작.");
        }
    }
}
#endif
