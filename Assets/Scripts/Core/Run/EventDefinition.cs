using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    public enum EventResolution
    {
        Instant,    // 즉시 결과 적용
        DiceCheck,  // 주사위 판정: 합이 목표 이상이면 성공 결과, 아니면 실패 결과
    }

    /// <summary>이벤트 선택지: 판정 방식 + 성공/실패 결과 묶음.</summary>
    [System.Serializable]
    public class EventChoice
    {
        public string Label = "선택";
        public EventResolution Resolution = EventResolution.Instant;

        [Header("주사위 판정 (Resolution = DiceCheck일 때)")]
        [Range(1, 5)] public int DiceCount = 3;
        public int SuccessThreshold = 11;

        [Header("성공 결과 (Instant는 이것만 사용)")]
        [SerializeReference, SubclassPicker] public List<EventOutcome> SuccessOutcomes = new List<EventOutcome>();
        [TextArea(1, 3)] public string SuccessText = "";

        [Header("실패 결과 (DiceCheck 전용)")]
        [SerializeReference, SubclassPicker] public List<EventOutcome> FailOutcomes = new List<EventOutcome>();
        [TextArea(1, 3)] public string FailText = "";
    }

    /// <summary>
    /// 이벤트 1개 = 에셋 1개. EventUI가 풀에서 랜덤으로 뽑아 표시한다.
    /// (구 주사위 도박은 이 모델의 특수 사례 — DiceCheck 선택지 하나 + 지나가기)
    /// </summary>
    [CreateAssetMenu(fileName = "Event", menuName = "DiceOrbit/Event Definition")]
    public class EventDefinition : ScriptableObject
    {
        public string Title = "이벤트";
        [Tooltip("전체 배경 (비우면 기본 펠트)")]
        public Sprite Background;
        [TextArea(3, 6)] public string FlavorText = "";
        [Tooltip("방문당 선택 가능 횟수 (0 = 무제한). 결과 없는 넘어가기 선택지는 카운트 안 함")]
        public int UseLimit = 0;
        public List<EventChoice> Choices = new List<EventChoice>();
    }
}
