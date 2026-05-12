using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.Characters.Scout
{
    // ── 치유 타일 속성 ───────────────────────────────────────────────────

    [System.Serializable]
    public class ScoutHealTile : TileAttribute
    {
        private float healPercent;

        public ScoutHealTile(float healPercent) : base(TileAttributeType.ScoutHeal, 0, 1, false)
        {
            this.healPercent = healPercent;
        }

        public override void OnArrive(Character character)    => ApplyHeal(character);
        public override void OnTraverse(Character character)  => ApplyHeal(character);

        private void ApplyHeal(Character character)
        {
            if (character == null || !character.IsAlive) return;

            int healAmount = Mathf.RoundToInt(character.Stats.MaxHP * healPercent);
            if (healAmount <= 0) return;

            var context = new CombatContext(
                character, character,
                new CombatAction("ScoutHeal", ActionType.Heal, healAmount)
            );
            CombatPipeline.Instance?.Process(context);
        }

        public override string GetDisplayName() => "응급 치유";
        public override string GetDescription()  => $"최대체력의 {healPercent * 100f:0.#}% 회복 (1턴)";
    }

    // ── 액티브 스킬 템플릿 ───────────────────────────────────────────────

    [System.Serializable]
    public class ScoutTrapRemoveActive : CharacterActiveTemplate
    {
        [Header("레벨별 회복량 (최대체력 %)")]
        [Tooltip("레벨 1~N 순서. 예: 0.01 = 1%, 0.30 = 30%")]
        [SerializeField] private float[] healPercentByLevel = { 0.01f, 0.06f, 0.12f, 0.20f, 0.30f };

        private static readonly TileAttributeType[] DebuffTypes =
        {
            TileAttributeType.RandMine,
            TileAttributeType.Bone,
            TileAttributeType.Honey,
            TileAttributeType.SnowPrison,
        };

        public override bool Execute(
            Character source, RuntimeAbility ability,
            List<Unit> targets, List<TileData> targetTiles, int diceValue)
        {
            if (targetTiles == null || targetTiles.Count == 0) return false;

            var tile = targetTiles[0];
            if (tile == null) return false;

            foreach (var type in DebuffTypes)
                tile.RemoveAttributeType(type);

            float heal = ResolveHealPercent(ability.CurrentLevel);
            tile.AddAttribute(new ScoutHealTile(heal));

            Debug.Log($"[ScoutTrapRemove] Tile #{tile.TileIndex} → 치유 타일 ({heal * 100f:0.#}%)");
            return true;
        }

        public override string BuildPreview(Character source, RuntimeAbility ability, int diceValue)
        {
            float heal = ResolveHealPercent(ability.CurrentLevel);
            return $"치유 타일 설치 ({heal * 100f:0.#}% 회복)";
        }

        public override int CalculateRawDamage(Character source, RuntimeAbility ability, int diceValue) => 0;

        private float ResolveHealPercent(int level)
        {
            if (healPercentByLevel == null || healPercentByLevel.Length == 0)
                return 0.05f;
            int idx = Mathf.Clamp(level - 1, 0, healPercentByLevel.Length - 1);
            return Mathf.Clamp01(healPercentByLevel[idx]);
        }
    }
}
