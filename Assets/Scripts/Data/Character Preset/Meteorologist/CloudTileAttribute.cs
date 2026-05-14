using DiceOrbit.Core;
using UnityEngine;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 구름 타일 속성. 아군이 이 타일에서 턴 종료 시 회피율을 영구 부여합니다.
    /// </summary>
    public class CloudTileAttribute : TileAttribute
    {
        public CloudTileAttribute() : base(TileAttributeType.Cloud, 10, -1) { }

        public override void OnEndTurn(Character character)
        {
            if (character == null) return;
            character.Stats.DodgeChance += Value;
            Debug.Log($"[구름 타일] {character.Stats.CharacterName} 회피율 +{Value}% 부여 (누계: {character.Stats.DodgeChance}%)");
        }

        public override string GetDisplayName() => "구름 타일";

        public override string GetDescription() =>
            $"이 타일에서 턴 종료 시 회피율 +{Value}% 영구 부여";
    }
}
