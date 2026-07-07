using UnityEngine;
using DiceOrbit.Data.Waves;

namespace DiceOrbit.Core.Run
{
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

        [Header("전투 내용")]
        public WaveDatabase WaveDatabase;
        [Tooltip("엘리트 전투에 쓸 웨이브 인덱스. 음수면 (마지막-1) 자동")]
        public int EliteWaveIndex = -1;
        [Tooltip("보스 전투에 쓸 웨이브 인덱스. 음수면 마지막 자동")]
        public int BossWaveIndex = -1;

        public int WaveCount => WaveDatabase != null && WaveDatabase.Waves != null ? WaveDatabase.Waves.Count : 0;

        public int ResolveEliteWaveIndex()
        {
            if (EliteWaveIndex >= 0) return EliteWaveIndex;
            return Mathf.Max(0, WaveCount - 2);
        }

        public int ResolveBossWaveIndex()
        {
            if (BossWaveIndex >= 0) return BossWaveIndex;
            return Mathf.Max(0, WaveCount - 1);
        }

        /// <summary>일반 전투의 웨이브 인덱스: 층 진행도에 비례 (보스/엘리트 전용 웨이브는 제외 범위).</summary>
        public int ResolveBattleWaveIndex(int floor)
        {
            int normalMax = Mathf.Max(0, WaveCount - 2);   // 마지막(보스) 제외한 범위 [0, N-2]
            if (normalMax <= 0) return 0;
            float progress = FloorCount > 1 ? (float)floor / (FloorCount - 1) : 0f;
            return Mathf.Clamp(Mathf.RoundToInt(progress * normalMax), 0, normalMax);
        }
    }
}
