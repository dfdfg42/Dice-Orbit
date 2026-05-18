using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Visuals;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.Passives
{
    [System.Serializable]
    public class BattleCryPassive : CharacterPassiveSkill
    {
        [Header("Designer Tuning")]
        [Tooltip("레벨별 인접 아군 1명당 피해 증가율(%). 예: 10은 +10%")]
        [SerializeField] private float[] bonusPercentPerAllyByLevel = { 10f, 50f, 100f, 150f, 200f };

        public override int Priority => 100;

        public override string GetDynamicDescription()
        {
            float bonus = ResolveBonusPercent(currentLevel);
            return $"좌우 1칸 아군 1명당 피해 +{bonus:0.#}%";
        }

        public override void Initialize(Unit ownerUnit)
        {
            base.Initialize(ownerUnit);
            if (owner is Character character && character.CurrentTile != null)
                RefreshRangePreview(character.CurrentTile);
        }

        public override void OnOwnerMoved(TileData newTile)
        {
            RefreshRangePreview(newTile);
        }

        protected override void ApplyLevel(int level) { }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null || context == null || context.Action == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Action.Type != ActionType.Attack) return;
            if (context.SourceUnit != owner) return;

            int allyCount = CountAdjacentAllies();
            if (allyCount <= 0) return;

            float bonusPercent = ResolveBonusPercent(currentLevel);
            float multiplier = 1f + (bonusPercent / 100f) * allyCount;
            context.OutputValue *= multiplier;
            Notify();
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

        private float ResolveBonusPercent(int level)
        {
            if (bonusPercentPerAllyByLevel == null || bonusPercentPerAllyByLevel.Length == 0)
                return 10f;
            int index = Mathf.Clamp(level - 1, 0, bonusPercentPerAllyByLevel.Length - 1);
            return Mathf.Max(0f, bonusPercentPerAllyByLevel[index]);
        }

        private void RefreshRangePreview(TileData centerTile)
        {
            TileSkillPreviewManager.EnsureInstance();
            var manager = TileSkillPreviewManager.Instance;
            if (manager == null || centerTile == null) return;

            var tiles = new List<TileData>();
            if (centerTile.PreviousTile != null) tiles.Add(centerTile.PreviousTile);
            if (centerTile.NextTile != null) tiles.Add(centerTile.NextTile);

            manager.ShowPassiveRange(owner, tiles, TilePreviewStyle.Buff);
        }
    }
}
