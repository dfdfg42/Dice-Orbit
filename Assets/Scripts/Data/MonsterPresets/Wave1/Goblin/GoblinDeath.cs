using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Goblin
{
    /// <summary>
    /// 고블린 사망 효과
    /// 몬스터가 죽을 때 실행되는 효과를 정의합니다.
    /// </summary>
    [System.Serializable]
    public class GoblinDeath : DeathEffect
    {
        // TODO: 필요한 필드 추가
        // 예: [SerializeField] private int explosionDamage = 10;

        // 생성자에서 기본값 설정
        public GoblinDeath()
        {
            effectName = "Goblin Death";
            description = "고블린이 죽을 때 발동하는 효과";
        }

        /// <summary>
        /// 사망 효과 실행
        /// </summary>
        public override void Execute(Monster deadMonster)
        {
            // TODO: 사망 효과 로직 구현

            Debug.Log($"[GoblinDeath] {deadMonster.name} died! Executing death effect...");

            var tiles = GameManager.Instance.GetOrbitManager().Tiles;
            foreach (var tile in tiles)
            {
                tile.RemoveAttributeType(DiceOrbit.Data.Tile.TileAttributeType.RandMine);
            }
        }
    }
}
