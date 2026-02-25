using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Skeleton
{
    /// <summary>
    /// 지뢰 설치 패시브
    /// 몬스터가 이동 시 일정 확률로 현재 타일에 지뢰를 설치합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "PlantBone", menuName = "Passives/Monster/PlantBone")]
    public class PlantBone : PassiveAbility, IIntValueReceiver
    {
        private int mineDamage = 10;

        [Tooltip("지뢰 지속 턴 (-1은 영구)")]
        [SerializeField] private int mineDuration = -1;

        public override int Priority => 10;

        public override string Description()
        {
            return $"지나갈 시 방어도를 {mineDamage} 얻는 타일을 설치합니다.";
        }

        /// <summary>
        /// IIntValueReceiver 구현 - 지뢰 데미지 값을 설정받습니다.
        /// </summary>
        public void SetValue(int value)
        {
            mineDamage = value;
            Debug.Log($"Bone set to {mineDamage}");
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 턴 시작 시에만 반응하도록 설정
            if (context.Action.Type == ActionType.OnStartTurn && context.SourceUnit == owner && trigger == CombatTrigger.OnPreAction)
            {
                Debug.Log($"[PlantMine] Triggered on {trigger} for unit");
                PlantMineOnTile();
            }
        }

        private void PlantMineOnTile()
        {
            var orbitManager = GameManager.Instance.GetOrbitManager();
            if (orbitManager == null) return;

            var targetTiles = new List<TileData>();
            for (int index = 4; index < 20; index+=5) {// 최대 3개의 타일에 지뢰 설치
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
                var mineAttribute = new BoneTile(
                    TileAttributeType.Bone,
                    mineDamage,
                    mineDuration
                );

                tile.AddAttribute(mineAttribute);
            }
        }

        public override bool AllowSameSkill(PassiveAbility incoming)
        {
            // 지뢰 설치는 중복 불가 (하나만 존재)
            return false;
        }
    }
}