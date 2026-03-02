using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Goblin
{
    /// <summary>
    /// 지뢰 설치 패시브
    /// 몬스터가 턴 시작 시 무작위 타일에 지뢰를 설치합니다.
    /// </summary>
    [System.Serializable]
    public class PlantMinePassive : PassiveAbility
    {
        [Header("Mine Settings")]
        [Tooltip("설치할 지뢰의 데미지")]
        [SerializeField] private int mineDamage = 5;

        [Tooltip("지뢰 지속 턴 (-1은 영구)")]
        [SerializeField] private int mineDuration = -1;

        // 생성자에서 기본값 설정
        public PlantMinePassive()
        {
            passiveName = "지뢰 설치";
            description = "지나갈 시 {mineDamege}의 피해를 주는 지뢰 타일을 생성합니다";
            priority = 10;
            isStackable = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 턴 시작 시에만 반응하도록 설정
            if (context.Action.Type == ActionType.OnStartTurn && context.SourceUnit == owner && trigger==CombatTrigger.OnPreAction)
            {
                Debug.Log($"[PlantMine] Triggered on {trigger} for unit");
                PlantMineOnTile();
            }
        }

        private void PlantMineOnTile()
        {
            var orbitManager = GameManager.Instance.GetOrbitManager();
            if (orbitManager == null) return;

            // 0~19 사이의 랜덤한 2개 정수를 뽑기
            var randomIndices = new List<int>();
            while (randomIndices.Count < 2)
            {
                int randomIndex = Random.Range(0, 20);
                if (!randomIndices.Contains(randomIndex))
                {
                    randomIndices.Add(randomIndex);
                }
            }

            // 해당 인덱스의 타일을 List<TileData>에 저장
            var targetTiles = new List<TileData>();
            foreach (var index in randomIndices)
            {
                var tile = orbitManager.GetTile(index);
                if (tile != null)
                {
                    targetTiles.Add(tile);
                    Debug.Log($"Mine Generated random index: {index}");
                }
            }

            // 각 타일에 지뢰 속성 추가
            foreach (var tile in targetTiles)
            {
                var mineAttribute = new RandMineTile(
                    TileAttributeType.RandMine,
                    mineDamage,
                    mineDuration
                );

                tile.AddAttribute(mineAttribute);
            }
        }

        public override bool AllowSamePassive(PassiveAbility incoming)
        {
            // 지뢰 설치는 중복 불가 (하나만 존재)
            return false;
        }
    }
}