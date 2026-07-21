using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data.Waves;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 층 구간 하나의 일반 전투 몹 세트 풀 — 이 구간의 전투 노드는 여기서 랜덤 1개를 뽑는다.
    /// </summary>
    [System.Serializable]
    public class EncounterTier
    {
        public string Name = "구간";
        [Tooltip("적용 시작 층 (0-based, 포함)")]
        public int MinFloor = 0;
        [Tooltip("적용 끝 층 (포함)")]
        public int MaxFloor = 4;
        [Tooltip("이 구간의 몹 세트 풀 — 노드마다 랜덤 1개 (같은 층이라도 노드마다 다를 수 있음)")]
        public List<EncounterDefinition> Encounters = new List<EncounterDefinition>();
    }

    /// <summary>
    /// 막(Act) 하나의 데이터 정의. 맵 생성기는 이것만 읽는다 — 2막 추가 = 에셋 하나 만들기.
    /// (스펙 §1 ActDefinition)
    /// </summary>
    [CreateAssetMenu(fileName = "Act", menuName = "DiceOrbit/Act Definition")]
    public class ActDefinition : ScriptableObject
    {
        [Header("정보")]
        public string ActName = "Act 1";

        [Header("맵 구조")]
        [Min(4)] public int FloorCount = 12;
        [Range(1, 5)] public int MinNodesPerFloor = 2;
        [Range(1, 5)] public int MaxNodesPerFloor = 4;
        [Tooltip("앞 N층은 단일 전투 노드 — 자동 모집 인트로 구간 (스펙 §3)")]
        [Min(0)] public int IntroFloors = 2;

        [Header("보장 규칙")]
        [Tooltip("이 층(0-based)에 엘리트 1개 보장. 음수면 비활성")]
        public int FirstEliteFloor = 5;
        [Tooltip("보스 앞 층을 '엘리트 or 휴식' 2택 층으로")]
        public bool PreBossChoice = true;
        [Min(0)] public int ShopCount = 1;
        [Min(0)] public int RestCount = 1;
        [Min(0)] public int EventCount = 2;
        [Tooltip("주사위 개조 드랍을 예고하는 전투 노드 수")]
        [Min(0)] public int DiceModBattleCount = 2;

        [Header("전투 내용 — 층 구간별 몹 세트 풀")]
        [Tooltip("일반 전투: 층이 속한 구간의 풀에서 노드마다 랜덤 1개")]
        public List<EncounterTier> BattleTiers = new List<EncounterTier>();
        [Tooltip("엘리트 전투 풀 (랜덤 1개)")]
        public List<EncounterDefinition> ElitePool = new List<EncounterDefinition>();
        [Tooltip("보스 전투 풀 (랜덤 1개)")]
        public List<EncounterDefinition> BossPool = new List<EncounterDefinition>();

        [Header("폴백 — 위 풀이 비어 있으면 구 웨이브 DB에서 유도 (마이그레이션용)")]
        public WaveDatabase WaveDatabase;

        private int WaveCount => WaveDatabase != null && WaveDatabase.Waves != null ? WaveDatabase.Waves.Count : 0;

        /// <summary>일반 전투 몹 세트: 층이 속한 티어 풀에서 랜덤. 풀이 없으면 DB 진행도 비례 폴백.</summary>
        public EncounterDefinition ResolveBattleEncounter(int floor, System.Random rng)
        {
            foreach (var tier in BattleTiers)
            {
                if (tier == null || tier.Encounters == null || tier.Encounters.Count == 0) continue;
                if (floor < tier.MinFloor || floor > tier.MaxFloor) continue;
                return tier.Encounters[rng.Next(tier.Encounters.Count)];
            }

            // 폴백: 구 방식 — 진행도 비례 DB 인덱스 (보스 웨이브 제외 범위)
            int normalMax = Mathf.Max(0, WaveCount - 2);
            if (WaveCount == 0) return null;
            float progress = FloorCount > 1 ? (float)floor / (FloorCount - 1) : 0f;
            int idx = Mathf.Clamp(Mathf.RoundToInt(progress * normalMax), 0, Mathf.Max(0, WaveCount - 1));
            return WaveDatabase.Waves[idx];
        }

        public EncounterDefinition ResolveEliteEncounter(System.Random rng)
        {
            if (ElitePool != null && ElitePool.Count > 0)
                return ElitePool[rng.Next(ElitePool.Count)];
            return WaveCount > 0 ? WaveDatabase.Waves[Mathf.Max(0, WaveCount - 2)] : null;
        }

        public EncounterDefinition ResolveBossEncounter(System.Random rng)
        {
            if (BossPool != null && BossPool.Count > 0)
                return BossPool[rng.Next(BossPool.Count)];
            return WaveCount > 0 ? WaveDatabase.Waves[WaveCount - 1] : null;
        }
    }
}
