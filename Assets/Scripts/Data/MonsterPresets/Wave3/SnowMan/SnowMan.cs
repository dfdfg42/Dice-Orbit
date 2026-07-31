using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.MonsterPresets.Wave3.SnowMan
{
    /// <summary>눈사람 세트 공유 헬퍼.</summary>
    public static class SnowSet
    {
        public static IEnumerable<Monster> OtherAliveMonsters(Monster owner)
        {
            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) yield break;
            foreach (var m in monsters)
                if (m != null && m != owner && m.IsAlive) yield return m;
        }

        public static List<TileData> ExpandLR(TileData center, int range)
        {
            var result = new List<TileData>();
            if (center == null) return result;
            var set = new HashSet<TileData> { center };
            var t = center;
            for (int i = 0; i < range && t?.NextTile != null; i++) { t = t.NextTile; set.Add(t); }
            t = center;
            for (int i = 0; i < range && t?.PreviousTile != null; i++) { t = t.PreviousTile; set.Add(t); }
            result.AddRange(set);
            return result;
        }

        // 눈사람이 이번 라운드(직전 플레이어 턴) 동안 받은 피해 누적
        private static readonly Dictionary<Monster, int> DamageTakenThisRound = new();

        public static void AddDamageTaken(Monster snowman, int amount)
        {
            if (snowman == null) return;
            DamageTakenThisRound.TryGetValue(snowman, out int cur);
            DamageTakenThisRound[snowman] = cur + Mathf.Max(0, amount);
        }

        public static int GetDamageTaken(Monster snowman)
        {
            if (snowman == null) return 0;
            DamageTakenThisRound.TryGetValue(snowman, out int cur);
            return cur;
        }

        public static void ResetDamageTaken(Monster snowman)
        {
            if (snowman != null) DamageTakenThisRound[snowman] = 0;
        }
    }

    /// <summary>[진창눈] 무작위 대상 1명에게 damage 피해. 눈사람이 이번 라운드 cancelDamageThreshold 이상 받았으면 취소.</summary>
    [System.Serializable]
    public class ThrowSnow : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 25;
        [Tooltip("이 피해 이상 받으면 공격 취소")]
        [SerializeField] private int cancelDamageThreshold = 20;

        public ThrowSnow()
        {
            skillName = "진창눈";
            description = "무작위 대상 1명에게 피해. 이번 라운드 일정 피해 이상 받으면 취소";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var snowman = source as Monster;
            if (snowman != null && SnowSet.GetDamageTaken(snowman) >= cancelDamageThreshold)
            {
                Debug.Log($"[진창눈] {source.name} 피해 {SnowSet.GetDamageTaken(snowman)} ≥ {cancelDamageThreshold} → 취소");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }

    /// <summary>[눈보라] 무작위 대상 2명이 속한 타일 + 좌우 각각 1칸에 피해. (RandomCharacter + Tiles + count 2 + range 1)</summary>
    [System.Serializable]
    public class SnowStorm : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 25;

        public SnowStorm()
        {
            skillName = "눈보라";
            description = "무작위 대상 2명이 속한 타일 + 좌우 각각 1칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>
    /// [행복한 눈사람] 눈사람의 공격이 적중한 적은 다음 턴 이동 불가(빙결)가 된다.
    /// 또한 눈사람이 받은 피해를 누적해 [진창눈]의 취소 판정에 쓰고, 턴 종료 시 초기화한다.
    /// </summary>
    [System.Serializable]
    public class HappySnowmanPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("피격된 적 이동불가(빙결) 지속 턴")]
        [SerializeField] private int immobilizeDuration = 2;

        public HappySnowmanPassive()
        {
            passiveName = "행복한 눈사람";
            description = "눈사람에게 피격된 적은 다음 턴 이동 불가";
            priority = 10;
            isStackable = false;
        }

        public override string GetDynamicDescription()
            => $"눈사람에게 피격된 적 다음 턴 이동 불가 ({immobilizeDuration}턴)";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnHit || context.IsSimulation || !context.IsEffected) return;

            // 눈사람이 받은 피해 누적 (진창눈 취소 판정용)
            if (context.Target == owner)
                SnowSet.AddDamageTaken(owner as Monster, Mathf.RoundToInt(context.OutputValue));

            // 눈사람 공격이 적중한 적 → 다음 턴 이동 불가
            if (context.SourceUnit == owner && context.Target is Character victim && victim.IsAlive)
                victim.StatusEffects?.AddEffect(new FrozenDebuff(0, immobilizeDuration));
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger == CombatTrigger.OnPostAction && context.Phase == EventPhase.TurnEnd && context.SourceUnit == owner)
                SnowSet.ResetDamageTaken(owner as Monster);
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
