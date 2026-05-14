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
        [Tooltip("레벨별 공격 피해 증가율(%). 예: 5는 +5%")]
        [SerializeField] private float[] damageBonusPercentByLevel = { 10f, 15f, 20f, 25f, 30f };

        public float damageMultiplier = 1.05f;
        public float CurrentDamageMultiplier => damageMultiplier;

        public override int Priority => 100;

        public override string GetDynamicDescription()
        {
            return $"피해 +{(CurrentDamageMultiplier - 1f) * 100f:0.#}%";
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

        protected override void ApplyLevel(int level)
        {
            float bonusPercent = ResolveBonusPercent(level);
            damageMultiplier = 1f + (bonusPercent / 100f);
        }

        private float ResolveBonusPercent(int level)
        {
            if (damageBonusPercentByLevel == null || damageBonusPercentByLevel.Length == 0)
            {
                return (damageMultiplier - 1f) * 100f;
            }

            int index = Mathf.Clamp(level - 1, 0, damageBonusPercentByLevel.Length - 1);
            return damageBonusPercentByLevel[index];
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null || context == null || context.Action == null) return;
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (context.Action.Type != ActionType.Attack) return;
            if (context.SourceUnit != owner) return;

            context.OutputValue *= damageMultiplier;
        }

        private void RefreshRangePreview(TileData centerTile)
        {
            TileSkillPreviewManager.EnsureInstance();
            var manager = TileSkillPreviewManager.Instance;
            if (manager == null || centerTile == null) return;

            var tiles = new List<TileData>();
            if (centerTile.PreviousTile != null) tiles.Add(centerTile.PreviousTile);
            if (centerTile.NextTile != null) tiles.Add(centerTile.NextTile);

            manager.ShowPassiveRange(tiles, TilePreviewStyle.Buff);
        }
    }
}
