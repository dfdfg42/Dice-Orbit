using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Visuals;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class BattleCryPassive : CharacterPassiveSkill, IPassiveRangeProvider
    {
        [Header("Designer Tuning")]
        [Tooltip("인접(좌우 1칸) 아군 1명당 피해 증가율(%). 예: 50은 +50%")]
        [SerializeField] private float bonusPercentPerAlly = 50f;

        public override int Priority => 100;

        public override string GetDynamicDescription()
        {
            return $"좌우 1칸 아군 1명당 피해 +{bonusPercentPerAlly:0.#}%";
        }

        /// <summary>
        /// 패시브 영향 범위 = 좌우 인접 타일. PassiveRangeIndicator가 조회 시 브래킷 표시에 사용.
        /// (구 방식: 상시 회전 트레일 → 조회 시 브래킷으로 대체, 2026-07)
        /// </summary>
        public IReadOnlyList<TileData> GetRangeTiles()
        {
            var tiles = new List<TileData>();
            var center = (owner as Character)?.CurrentTile;
            if (center == null) return tiles;

            if (center.PreviousTile != null) tiles.Add(center.PreviousTile);
            if (center.NextTile != null) tiles.Add(center.NextTile);
            return tiles;
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.SourceUnit != owner) return;

            int allyCount = CountAdjacentAllies();
            if (allyCount <= 0) return;

            float multiplier = 1f + (bonusPercentPerAlly / 100f) * allyCount;
            context.OutputValue *= multiplier;
            if (!context.IsSimulation) Notify();
        }

        private int CountAdjacentAllies()
        {
            var character = owner as Character;
            if (character?.CurrentTile == null) return 0;

            var partyManager = PartyManager.Instance;
            if (partyManager == null) return 0;

            var adjacentTiles = new HashSet<TileData>();
            if (character.CurrentTile.PreviousTile != null)
                adjacentTiles.Add(character.CurrentTile.PreviousTile);
            if (character.CurrentTile.NextTile != null)
                adjacentTiles.Add(character.CurrentTile.NextTile);

            int count = 0;
            foreach (var ally in partyManager.GetAliveCharacters())
            {
                if (ally == null || ally == character) continue;
                if (adjacentTiles.Contains(ally.CurrentTile))
                    count++;
            }
            return count;
        }

    }
}
