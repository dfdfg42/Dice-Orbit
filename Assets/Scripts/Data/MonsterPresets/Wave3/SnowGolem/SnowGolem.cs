using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.MonsterPresets.Wave3.SnowMan;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave3.SnowGolem
{
    // ==========================================
    // 패턴 1 [눈강타]
    // ==========================================
    /// <summary>
    /// 이동 불가(눈감옥/빙결) 상태인 적이 속한 타일 + 좌우 각각 2칸에 피해.
    /// 그런 적이 없으면 무작위 적 1명이 속한 타일 + 좌우 2칸에 피해.
    /// 대상 타일은 GetCustomTiles에서 직접 선정한다.
    /// </summary>
    [System.Serializable]
    public class SnowSmash : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("중심 타일 기준 좌우 확장 칸 수")]
        [SerializeField] private int range = 2;

        public SnowSmash()
        {
            skillName = "눈강타";
            description = "이동 불가 적이 속한 타일 + 좌우 각각 2칸에 피해 (없으면 무작위 적 기준)";
        }

        public override int GetPreviewDamage() => damage;

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            var alive = PartyManager.Instance?.GetAliveCharacters();
            if (alive == null || alive.Count == 0) return new List<TileData>();

            // 1순위: 이동 불가(빙결) 상태인 적
            var frozen = alive.Where(c =>
                c != null && c.CurrentTile != null &&
                c.StatusEffects != null && c.StatusEffects.HasEffect(DiceOrbit.Data.EffectType.Frozen)).ToList();

            Character center = frozen.Count > 0
                ? frozen[Random.Range(0, frozen.Count)]
                : alive[Random.Range(0, alive.Count)];

            return center != null ? SnowSet.ExpandLR(center.CurrentTile, range) : new List<TileData>();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 패턴 2 [눈 방패]
    // ==========================================
    /// <summary>
    /// 모든 아군 몬스터(자신 포함)에게 일시 방어도를 부여한다.
    /// 타겟 없는 팀 버프이므로 MonsterSkill: TargetType=Self, IntentType=Defend.
    /// </summary>
    [System.Serializable]
    public class SnowShield : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int armorAmount = 5;

        public SnowShield()
        {
            skillName = "눈 방패";
            description = "모든 아군에게 일시 방어도 부여";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) return;

            foreach (var m in monsters)
            {
                if (m == null || !m.IsAlive || m.Stats == null) continue;
                m.Stats.TempArmor += armorAmount;
            }
            Debug.Log($"[눈 방패] 모든 아군 방어도 +{armorAmount}");
        }
    }

    // ==========================================
    // 사망 효과
    // ==========================================
    [System.Serializable]
    public class SnowGolemDeath : DeathEffect
    {
        public SnowGolemDeath()
        {
            effectName = "SnowGolem Death";
            description = "눈 골렘이 사망 시, 눈 감옥 타일들이 전부 사라집니다.";
        }

        public override void Execute(Monster deadMonster)
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager?.Tiles == null) return;
            foreach (var tile in orbitManager.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.SnowPrison);
        }
    }

    // ==========================================
    // 패시브 [눈감옥]
    // ==========================================
    /// <summary>
    /// 턴 시작 시 무작위 타일 1개와 좌우 1칸에 눈감옥 타일을 설치한다.
    /// 해당 타일에서 턴 종료 시 눈감옥(다음 턴 이동 불가) 상태이상이 부여되며,
    /// 타일은 지속시간이 끝나면 사라진다. 몬스터 사망 시 SnowGolemDeath가 모두 제거.
    /// </summary>
    [System.Serializable]
    public class SnowGolemPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("타일 지속 턴")]
        [SerializeField] private int tileDuration = 1;
        [Tooltip("부여되는 빙결(이동 불가) 지속 턴")]
        [SerializeField] private int frozenDuration = 1;

        public SnowGolemPassive()
        {
            passiveName = "눈 감옥";
            description = "턴 시작 시 무작위 타일 1개와 좌우 1칸에 눈감옥 설치. 그 위에서 턴 종료 시 다음 턴 이동 불가";
            priority = 10;
            isStackable = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context?.Action == null || owner == null) return;

            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.OnStartTurn &&
                context.SourceUnit == owner)
            {
                PlantSnowPrisonTiles();
            }
        }

        private void PlantSnowPrisonTiles()
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null) return;

            int total = orbitManager.TileCount;
            if (total <= 0) return;

            int centerIndex = Random.Range(0, total);
            int leftIndex = (centerIndex - 1 + total) % total;
            int rightIndex = (centerIndex + 1) % total;

            foreach (var index in new[] { leftIndex, centerIndex, rightIndex })
            {
                var tile = orbitManager.GetTile(index);
                if (tile == null || tile.HasAttribute(TileAttributeType.SnowPrison)) continue;

                tile.AddAttribute(new SnowPrisonTileAttribute(
                    TileAttributeType.SnowPrison,
                    frozenDuration + 1,
                    tileDuration + 1));
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
