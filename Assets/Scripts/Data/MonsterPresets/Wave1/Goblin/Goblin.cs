using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Skills.Effects;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Visuals;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Goblin
{
    // ==========================================
    // 1. Goblin Attack
    // ==========================================
    [System.Serializable]
    public class GoblinAttack : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 10;

        public GoblinAttack()
        {
            skillName = "test";
            description = "test";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            foreach (var target in targetUnits)
            {
                if (target == null || !target.IsAlive) continue;

                var context = new CombatContext(
                    source,
                    target,
                    new CombatAction(SkillName, ActionType.Attack, damage)
                );
                CombatPipeline.Instance?.Process(context);

                Debug.Log($"[{SkillName}] {source.name} attacks {target.name} for {damage} damage");
            }
        }
    }

    // ==========================================
    // 2. Goblin Death
    // ==========================================
    [System.Serializable]
    public class GoblinDeath : DeathEffect
    {
        public GoblinDeath()
        {
            effectName = "Goblin Death";
            description = "고블린이 죽을 때 발동하는 효과";
        }

        public override void Execute(Monster deadMonster)
        {
            Debug.Log($"[GoblinDeath] {deadMonster.name} died! Executing death effect...");

            var tiles = GameManager.Instance.GetOrbitManager().Tiles;
            foreach (var tile in tiles)
            {
                tile.RemoveAttributeType(DiceOrbit.Data.Tile.TileAttributeType.RandMine);
            }
        }
    }

    // ==========================================
    // 3. Plant Mine Passive
    // ==========================================
    [System.Serializable]
    public class PlantMinePassive : PassiveAbility
    {
        [Header("Mine Settings")]
        [Tooltip("설치할 지뢰의 데미지")]
        [SerializeField] private int mineDamage = 5;

        [Tooltip("지뢰 지속 턴 (-1은 영구)")]
        [SerializeField] private int mineDuration = -1;

        public PlantMinePassive()
        {
            passiveName = "지뢰 설치";
            description = "지나갈 시 지뢰 피해를 주는 타일을 생성합니다";
            priority = 10;
            isStackable = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
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

            var randomIndices = new List<int>();
            while (randomIndices.Count < 2)
            {
                int randomIndex = Random.Range(0, 20);
                if (!randomIndices.Contains(randomIndex))
                {
                    randomIndices.Add(randomIndex);
                }
            }

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
            return false;
        }
    }

    // ==========================================
    // 4. Mine Bomb Skill (MonsterSkillData)
    // ==========================================
    [System.Serializable]
    public class MineBombSkill : MonsterSkillData
    {
        public MineBombSkill()
        {
            skillName = "지뢰 폭파";
            description = "맵에 있는 모든 지뢰를 폭파시켜 데미지를 줍니다.";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            base.Execute(source, targetUnits, targetTiles, diceValue);
        }
    }
}
