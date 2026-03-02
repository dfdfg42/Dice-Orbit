using DiceOrbit.Data;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Goblin
{
    /// <summary>
    /// 고블린 지뢰 폭파 스킬
    /// 맵에 있는 모든 지뢰를 폭파시켜 주변 타일의 캐릭터에게 데미지를 줍니다.
    /// </summary>
    [System.Serializable]
    public class MineBombSkill : MonsterSkillData
    {
        // 생성자에서 기본값 설정
        public MineBombSkill()
        {
            skillName = "지뢰 폭파";
            description = "맵에 있는 모든 지뢰를 폭파시켜 데미지를 줍니다.";
            // Effects.Add 제거 - ScriptableObject는 생성자에서 생성 불가!
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            // 부모 클래스의 Execute 호출 (Effects 실행)
            base.Execute(source, targetUnits, targetTiles, diceValue);
        }
    }
}
