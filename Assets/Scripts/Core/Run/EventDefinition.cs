using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>이벤트 선택지의 결과 한 줄 (한 선택지에 여러 개 조합 가능).</summary>
    public enum EventOutcomeType
    {
        None,               // 아무 일 없음
        GainGold,           // 골드 +N
        LoseGold,           // 골드 -N (0 밑으로 안 내려감)
        HealPartyPercent,   // 파티 전원 최대 HP의 N% 회복
        DamagePartyFlat,    // 파티 전원 N 피해 (HP 1 미만 방지 — 전투 밖 사망 없음)
        GainRandomPotion,   // 랜덤 포션 1개 (슬롯 가득이면 무효)
        GainRandomRelic,    // 미보유 유물 랜덤 1개
    }

    [System.Serializable]
    public class EventOutcome
    {
        public EventOutcomeType Type = EventOutcomeType.None;
        public int Value;
    }

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
        public List<EventOutcome> SuccessOutcomes = new List<EventOutcome>();
        [TextArea(1, 3)] public string SuccessText = "";

        [Header("실패 결과 (DiceCheck 전용)")]
        public List<EventOutcome> FailOutcomes = new List<EventOutcome>();
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
        public List<EventChoice> Choices = new List<EventChoice>();
    }

    /// <summary>이벤트 결과 실행기 — 적용하고 사람이 읽을 요약을 돌려준다.</summary>
    public static class EventOutcomes
    {
        public static string Apply(List<EventOutcome> outcomes)
        {
            if (outcomes == null || outcomes.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var outcome in outcomes)
            {
                if (outcome == null) continue;
                string line = ApplyOne(outcome);
                if (string.IsNullOrEmpty(line)) continue;
                if (sb.Length > 0) sb.Append("  ·  ");
                sb.Append(line);
            }
            return sb.ToString();
        }

        private static string ApplyOne(EventOutcome outcome)
        {
            switch (outcome.Type)
            {
                case EventOutcomeType.GainGold:
                    GoldManager.EnsureInstance().AddGold(outcome.Value);
                    return $"골드 +{outcome.Value}";

                case EventOutcomeType.LoseGold:
                    GoldManager.EnsureInstance().AddGold(-outcome.Value);
                    return $"골드 -{outcome.Value}";

                case EventOutcomeType.HealPartyPercent:
                {
                    foreach (var c in AliveParty())
                    {
                        int heal = Mathf.RoundToInt(c.Stats.MaxHP * outcome.Value / 100f);
                        c.Stats.CurrentHP = Mathf.Min(c.Stats.MaxHP, c.Stats.CurrentHP + heal);
                    }
                    return $"파티 {outcome.Value}% 회복";
                }

                case EventOutcomeType.DamagePartyFlat:
                {
                    foreach (var c in AliveParty())
                        c.Stats.CurrentHP = Mathf.Max(1, c.Stats.CurrentHP - outcome.Value);
                    return $"파티 전원 {outcome.Value} 피해";
                }

                case EventOutcomeType.GainRandomPotion:
                {
                    var potion = PotionManager.EnsureInstance().GrantRandomDrop();
                    return potion != null ? $"포션 획득 — {potion.PotionName}" : "포션 슬롯이 가득...";
                }

                case EventOutcomeType.GainRandomRelic:
                {
                    var relic = RelicManager.EnsureInstance().GrantRandom();
                    return relic != null ? $"유물 획득 — {relic.RelicName}" : "";
                }
            }
            return "";
        }

        private static IEnumerable<Character> AliveParty()
        {
            var party = PartyManager.Instance?.Party;
            if (party == null) yield break;
            foreach (var c in party)
                if (c != null && c.IsAlive && c.Stats != null) yield return c;
        }
    }
}
