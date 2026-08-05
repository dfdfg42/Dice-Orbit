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
        [SerializeField] private int damage = 25;
        [Tooltip("중심 타일 기준 좌우 확장 칸 수")]
        [SerializeField] private int range = 3;

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
                c.StatusEffects != null && c.StatusEffects.HasEffect(DiceOrbit.Data.EffectType.Bound)).ToList();

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
    // 패턴 2 [눈 주먹]
    // ==========================================
    /// <summary>[눈 주먹] 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해. (RandomCharacter + Tiles + range 2)</summary>
    [System.Serializable]
    public class SnowFistSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 25;

        public SnowFistSkill()
        {
            skillName = "눈 주먹";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 조건부 AI [SnowGolemPattern]
    // ==========================================
    /// <summary>
    /// 눈골렘 AI. availableSkills 순서 = [0 눈강타, 1 눈주먹].
    /// 살아있는 적 중 이동불가(Frozen)가 하나라도 있으면 눈강타, 없으면 눈주먹.
    /// </summary>
    [System.Serializable]
    public class SnowGolemPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;

            var alive = PartyManager.Instance?.GetAliveCharacters();
            bool anyFrozen = false;
            if (alive != null)
                foreach (var c in alive)
                    if (c != null && c.StatusEffects != null &&
                        c.StatusEffects.HasEffect(DiceOrbit.Data.EffectType.Bound)) { anyFrozen = true; break; }

            if (anyFrozen) return availableSkills[0];                              // 눈강타
            return availableSkills.Count >= 2 ? availableSkills[1] : availableSkills[0]; // 눈주먹
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
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Bind);
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

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;

            if (trigger == CombatTrigger.OnPreAction &&
                context.Phase == EventPhase.TurnStart &&
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
                if (tile == null || tile.HasAttribute(TileAttributeType.Bind)) continue;

                tile.AddAttribute(new BindTileAttribute(
                    TileAttributeType.Bind,
                    frozenDuration + 1,
                    tileDuration + 1));
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
