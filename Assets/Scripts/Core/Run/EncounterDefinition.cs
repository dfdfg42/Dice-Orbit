using System.Collections.Generic;
using DiceOrbit.Data.Monsters;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 전투 1회의 몹 세트 (구 WaveDefinition 개명, 2026-07-21).
    /// ActDefinition의 티어/엘리트/보스 풀에 인라인 직렬화된다.
    /// BackgroundSprite는 막 기본 배경(ActDefinition.DefaultBackground)의 오버라이드 — 비면 막 기본.
    /// </summary>
    [System.Serializable]
    public class EncounterDefinition
    {
        public List<MonsterPreset> MonsterPresets;
        public Sprite BackgroundSprite;
    }
}
