using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.CharacterActives
{
    /// <summary>
    /// [일기예보] 액티브 — 주사위 눈금 2 이하 사용.
    /// 다음 2턴 동안 낮은 주사위 눈금이 나올 확률을 5% 높입니다.
    /// </summary>
    [System.Serializable]
    public class WeatherForecastActive : CharacterActiveSkill
    {
        [SerializeField] private int biasDurationTurns = 2;
        [SerializeField] private int biasPercent = 5;

        public override int CalculateRawDamage(Character source, ActiveSkillSlot ability, int diceValue)
        {
            return 0;
        }

        public override string BuildPreview(Character source, ActiveSkillSlot ability, int diceValue)
        {
            return $"다음 {biasDurationTurns}턴 동안 낮은 주사위 눈금 확률 +{biasPercent}%";
        }

        public override bool Execute(Character source, ActiveSkillSlot ability, List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            DiceManager.Instance?.ApplyForecastBias(biasPercent, biasDurationTurns);
            Debug.Log($"[일기예보] 다음 {biasDurationTurns}턴 낮은 주사위 확률 +{biasPercent}%");
            OnAfterResolved(source, ability);
            return true;
        }
    }
}
