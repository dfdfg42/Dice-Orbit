using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Skeleton
{
    // ==========================================
    // 1. Skeleton Attack
    // ==========================================
    [System.Serializable]
    public class SkeletonAttack : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int basicDamage = 30;

        public SkeletonAttack()
        {
            skillName = "타격";
            description = "상대에게 30 + 자신의 방어도만큼의 데미지를 줍니다";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            int damage = basicDamage + source.Stats.TempArmor;
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
    // 2. Skeleton Whip
    // ==========================================
    [System.Serializable]
    public class SkeletonWhip : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int basicDamage = 20;

        public SkeletonWhip()
        {
            skillName = "휩쓸기";
            description = "무작위 대상 1명이 서있는 타일 + 좌우 2칸에 20 피해";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            int damage = basicDamage;
            foreach (var tile in targetTiles)
            {
                if (tile == null) continue;
                var targetsOnTile = GameManager.Instance?.GetOrbitManager().GetCharactersOnTile(tile);
                foreach (var target in targetsOnTile)
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
    }

    // ==========================================
    // 3. Skeleton Death
    // ==========================================
    [System.Serializable]
    public class SkelettonDeath : DeathEffect
    {
        public SkelettonDeath()
        {
            effectName = "Skeleton Death";
            description = "스켈레톤이 죽을 때 발동하는 효과";
        }

        public override void Execute(Monster deadMonster)
        {
            Debug.Log($"[SkelettonDeath] {deadMonster.name} died! Executing death effect...");

            var tiles = GameManager.Instance.GetOrbitManager().Tiles;
            foreach (var tile in tiles)
            {
                tile.RemoveAttributeType(DiceOrbit.Data.Tile.TileAttributeType.Bone);
            }
        }
    }

    // ==========================================
    // 4. Plant Bone Passive
    // ==========================================
    [System.Serializable]
    public class PlantBonePassive : PassiveAbility
    {
        [Header("Bone Settings")]
        [SerializeField] private int armorAmount = 10;

        [Tooltip("지속 턴 (-1은 영구)")]
        [SerializeField] private int duration = -1;
        bool activated = false;

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
            if (context.Action.Type == ActionType.OnStartTurn && context.SourceUnit == owner && trigger == CombatTrigger.OnPreAction)
            {
                if (activated) return;
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

        public override bool AllowSamePassive(IPassive incoming)
        {
            return false;
        }
    }
}
