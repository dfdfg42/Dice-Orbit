using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Skills.Effects;
using DiceOrbit.Data.Tile;
using DiceOrbit.Visuals;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Goblin
{
    [CreateAssetMenu(fileName = "MineBomb", menuName = "DiceOrbit/Skill Effects/Mine Bomb")]
    public class MineBomb : SkillEffectBase
    {
        [SerializeField] private int damage = 20;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;

            // targetTiles가 시스템(MonsterSkill)에 의해 제공되므로 신뢰하고 사용
            var affectedTiles = targetTiles != null ? targetTiles.Where(tile => tile != null).Distinct().ToList() : new List<TileData>();

            var mineTiles = affectedTiles
                .Where(tile => tile.GetAttributes().Any(attr => attr != null && attr.Type == TileAttributeType.RandMine))
                .ToList();

            // 대상에 지뢰나 영향받는 타일이 없으면 종료
            if (mineTiles.Count == 0 && affectedTiles.Count == 0) return;

            VfxManager.PlayCast(vfxProfile, source);

            foreach (var tile in affectedTiles)
            {
                VfxManager.PlayTile(vfxProfile, tile);
            }

            var aliveCharacters = partyManager.GetAliveCharacters();
            foreach (var character in aliveCharacters)
            {
                if (character == null || !character.IsAlive) continue;
                if (character.CurrentTile == null || !affectedTiles.Contains(character.CurrentTile)) continue;

                var action = new CombatAction("Mine Bomb", ActionType.Attack, damage);
                if (vfxProfile != null)
                {
                    action.AddTag("CustomVfx");
                }

                var context = new CombatContext(
                    source,
                    character,
                    action
                );
                CombatPipeline.Instance?.Process(context);

                if (context.IsEffected)
                {
                    VfxManager.PlayHit(vfxProfile, character);
                }
            }

            // 폭발 후 지뢰 속성 제거
            foreach (var mineTile in mineTiles)
            {
                var mines = mineTile.GetAttributes()
                    .Where(attr => attr != null && attr.Type == TileAttributeType.RandMine)
                    .ToList();

                foreach (var mine in mines)
                {
                    mineTile.RemoveAttribute(mine);
                }
            }
        }
    }
}
