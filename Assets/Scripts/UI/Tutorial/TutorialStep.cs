using System;
using UnityEngine;

namespace DiceOrbit.UI.Tutorial
{
    public enum TutorialAdvance
    {
        Confirm,          // "다음" 버튼 클릭으로 진행 (정보 단계)
        Custom            // Step.Done() 델리게이트가 true를 반환하면 진행 (조작 단계)
    }

    public enum TutorialActionLock
    {
        None,             // 제한 없음
        MoveOnly,         // 이동만 가능 (스킬 버튼 비활성)
        SkillOnly         // 스킬만 가능 (이동 버튼 비활성)
    }

    /// <summary>튜토리얼 한 단계. Target/Done은 런타임 상태를 참조하므로 델리게이트로 지연 평가.</summary>
    public class TutorialStep
    {
        public string Instruction;
        public Func<RectTransform> Target;   // 하이라이트 대상(널 가능). 매 프레임 재평가.
        public TutorialAdvance Advance = TutorialAdvance.Confirm;
        public Func<bool> Done;              // Advance=Custom일 때 진행조건
        public bool GateInput;               // 대상 외 입력 차단
        public bool NoSpotlight;             // 딤/스포트라이트 없이 화면 전체를 밝게 (공격 등 전체를 봐야 하는 단계)
        public int? OnlyDieValue;            // 지정 시 이 눈 값 주사위만 선택 가능(나머지 잠금)
        public TutorialActionLock ActionLock;// 이동/스킬 중 하나만 허용
        public Action OnEnter;               // 단계 진입 시 1회 (통제 주사위 세팅 등)
        public Vector2 HighlightOffset;      // 하이라이트 구멍 이동 (스크린px, +y=위)
        public Vector4 HighlightPad;         // 구멍 각 변 확장 (스크린px): x=좌, y=우, z=상, w=하
        public bool AllowDismiss;            // Custom 단계에서 "확인"으로 안내 닫기 (진행조건은 계속 대기)
        public bool CenterBubble;            // 대상이 있어도 안내문구를 화면 중앙(하단)에

        public TutorialStep(string instruction) { Instruction = instruction; }
    }
}
