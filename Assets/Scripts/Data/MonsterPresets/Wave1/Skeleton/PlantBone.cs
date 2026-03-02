using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Skeleton
{
    /// <summary>
    /// 뼈 설치 패시브
    /// 몬스터가 턴 시작 시 무작위 타일에 방어도를 주는 타일을 설치합니다.
    /// </summary>
    [System.Serializable]
    public class PlantBonePassive : PassiveAbility
    {
        [Header("Bone Settings")]
        [SerializeField] private int armorAmount = 10;

        [Tooltip("지속 턴 (-1은 영구)")]
        [SerializeField] private int duration = -1;
        bool activated = false;
        // 생성자에서 기본값 설정
        public PlantBonePassive()
        {
            if (string.IsNullOrEmpty(passiveName))
                passiveName = "Plant Bone";
            if (string.IsNullOrEmpty(description))
                description = $"Place tiles that grant {armorAmount} armor";
            priority = 10;
            isStackable = false;
        }

        public override string Description => $"지나갈 시 방어도를 {armorAmount} 얻는 타일을 설치합니다.";

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 턴 시작 시에만 반응하도록 설정
            if (context.Action.Type == ActionType.OnStartTurn && context.SourceUnit == owner && trigger == CombatTrigger.OnPreAction)
            {
                if (activated) return; // 이미 활성화된 경우 중복 방지
                Debug.Log($"[PlantMine] Triggered on {trigger} for unit");
                PlantMineOnTile();
            }
        }

        private void PlantMineOnTile()
        {
            var orbitManager = GameManager.Instance.GetOrbitManager();
            if (orbitManager == null) return;

            var targetTiles = new List<TileData>();
            for (int index = 4; index < 20; index+=5) {
                var tile = orbitManager.GetTile(index);
                if (tile != null)
                {
                    targetTiles.Add(tile);
                    Debug.Log($"Bone Generated at index: {index}");
                }
            }

            // 각 타일에 뼈 속성 추가
            foreach (var tile in targetTiles)
            {
                var boneAttribute = new BoneTile(
                    TileAttributeType.Bone,
                    armorAmount,
                    duration
                );

                tile.AddAttribute(boneAttribute);
            }
        }

        public override bool AllowSamePassive(PassiveAbility incoming)
        {
            // 뼈 설치는 중복 불가 (하나만 존재)
            return false;
        }
    }
}