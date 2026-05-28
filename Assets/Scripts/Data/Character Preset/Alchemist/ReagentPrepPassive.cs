using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// [시약 준비] 웨이브 시작 시 무작위 타일에 시약 타일을 설치한다.
    /// 연금술사가 시약 타일을 통과하거나 그 위에서 턴을 종료하면 해당 웨이브 동안
    /// 피해량이 증가하며 중첩된다.
    /// </summary>
    [System.Serializable]
    public class ReagentPrepPassive : CharacterPassiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("시약 타일 접촉 1회당 피해 증가율(%). 예: 50은 +50%")]
        [SerializeField] private float bonusPercentPerStack = 50f;
        [Tooltip("웨이브 시작 시 설치할 시약 타일 개수")]
        [SerializeField] private int reagentTileCount = 3;

        private int reagentStacks;

        public override int Priority => 98;

        public override string GetDynamicDescription()
        {
            return $"웨이브 시작 시 시약 타일 {reagentTileCount}개 설치. 통과/턴 종료 시 피해 +{bonusPercentPerStack:0.#}% (중첩)";
        }

        public override void Initialize(Unit ownerUnit)
        {
            base.Initialize(ownerUnit);
            SubscribeWaveStart();

            // 웨이브 진행 중에 합류한 경우 즉시 설치
            if (WaveManager.Instance != null && WaveManager.Instance.IsWaveActive)
            {
                reagentStacks = 0;
                PlaceReagentTiles();
            }
        }

        private void SubscribeWaveStart()
        {
            if (WaveManager.Instance == null) return;
            WaveManager.Instance.OnWaveStart -= HandleWaveStart;
            WaveManager.Instance.OnWaveStart += HandleWaveStart;
        }

        private void HandleWaveStart(int wave)
        {
            if (owner == null) return;
            if (!(owner is Character ch) || !ch.IsAlive) return;

            reagentStacks = 0;
            ClearReagentTiles();
            PlaceReagentTiles();
        }

        /// <summary>시약 타일이 연금술사에 의해 발동될 때 호출.</summary>
        public void AddReagentStack()
        {
            reagentStacks++;
            Notify($"{PassiveName} +{bonusPercentPerStack:0.#}%");
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null || context == null || context.Action == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Action.Type != ActionType.Attack) return;
            if (context.SourceUnit != owner) return;
            if (reagentStacks <= 0) return;

            float multiplier = 1f + (bonusPercentPerStack / 100f) * reagentStacks;
            context.OutputValue *= multiplier;
        }

        private void PlaceReagentTiles()
        {
            var orbitManager = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbitManager == null) return;

            var tiles = orbitManager.Tiles;
            if (tiles == null || tiles.Count == 0) return;

            var alchemist = owner as Character;
            int count = Mathf.Clamp(reagentTileCount, 0, tiles.Count);

            var pool = tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Reagent)).ToList();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int idx = Random.Range(0, pool.Count);
                var tile = pool[idx];
                pool.RemoveAt(idx);
                tile.AddAttribute(new ReagentTile(this, alchemist));
            }
        }

        private void ClearReagentTiles()
        {
            var orbitManager = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbitManager == null) return;

            var tiles = orbitManager.Tiles;
            if (tiles == null) return;

            foreach (var tile in tiles)
            {
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Reagent);
            }
        }
    }
}
