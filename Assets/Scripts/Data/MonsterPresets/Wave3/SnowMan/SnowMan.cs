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
    // ==========================================
    // 눈사람 세트 공유 헬퍼
    // ==========================================
    public static class SnowSet
    {
        /// <summary>owner를 제외한 살아있는 아군 몬스터.</summary>
        public static IEnumerable<Monster> OtherAliveMonsters(Monster owner)
        {
            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) yield break;
            foreach (var m in monsters)
                if (m != null && m != owner && m.IsAlive) yield return m;
        }

        /// <summary>중심 타일에서 좌우 range칸 확장 (순환).</summary>
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

    // ==========================================
    // 패턴 1 [눈덩이 던지기]
    // ==========================================
    /// <summary>
    /// 무작위 대상 1명에게 눈감옥(다음 턴 이동 불가) 디버프 부여.
    /// 눈사람이 직전 플레이어 턴 동안 10 이상 피해를 받았으면 공격을 취소한다.
    /// 대상 선정은 MonsterSkill: RandomCharacter + Characters + count 1.
    /// </summary>
    [System.Serializable]
    public class ThrowSnow : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("눈감옥(이동 불가) 지속 턴")]
        [SerializeField] private int prisonDuration = 2;
        [Tooltip("이 피해 이상 받으면 공격 취소")]
        [SerializeField] private int cancelDamageThreshold = 10;

        public ThrowSnow()
        {
            skillName = "눈덩이 던지기";
            description = "무작위 대상 1명에게 눈감옥(다음 턴 이동 불가) 부여. 10 이상 피해를 받으면 공격 취소";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var snowman = source as Monster;

            // 받은 피해가 임계치 이상이면 공격 취소
            if (snowman != null && SnowSet.GetDamageTaken(snowman) >= cancelDamageThreshold)
            {
                Debug.Log($"[눈덩이 던지기] {source.name} 피해 {SnowSet.GetDamageTaken(snowman)} ≥ {cancelDamageThreshold} → 공격 취소");
                return;
            }

            bool appliedDebuff = false;
            if (targetUnits != null)
            {
                foreach (var target in targetUnits)
                {
                    if (target == null || !target.IsAlive) continue;
                    target.StatusEffects?.AddEffect(new FrozenDebuff(0, prisonDuration));
                    appliedDebuff = true;
                }
            }

            // [행복한 눈사람] 디버프 부여 성공 → 다른 아군 몬스터 +3 회복
            if (appliedDebuff && snowman != null)
                HappySnowmanPassive.HealAllies(snowman);
        }
    }

    // ==========================================
    // 패턴 2 [눈보라]
    // ==========================================
    /// <summary>
    /// 무작위 대상 2명이 속한 타일 + 좌우 각각 1칸에 피해.
    /// 대상은 MonsterSkill: RandomCharacter + Tiles + count 2 + range 1.
    /// </summary>
    [System.Serializable]
    public class SnowStorm : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;

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

    // ==========================================
    // 패시브 [행복한 눈사람]
    // ==========================================
    /// <summary>
    /// 눈사람의 공격이 성공(피해 적중)하면 다른 아군 몬스터의 체력을 +3 회복한다.
    /// (디버프 부여 시 회복은 ThrowSnow 스킬에서 직접 호출)
    /// 또한 눈사람이 받은 피해를 누적해 [눈덩이 던지기]의 공격 취소 판정에 사용한다.
    /// </summary>
    [System.Serializable]
    public class HappySnowmanPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("아군 1명당 회복량")]
        [SerializeField] private int healAmount = 3;

        // 외부(스킬)에서 호출하기 위한 정적 회복량 캐시
        private static int s_healAmount = 3;

        public HappySnowmanPassive()
        {
            passiveName = "행복한 눈사람";
            description = "눈사람이 적에게 디버프를 부여하거나 공격에 성공하면 다른 아군 +3 회복";
            priority = 10;
            isStackable = false;
        }

        public override string GetDynamicDescription()
            => $"눈사람이 디버프 부여/공격 성공 시 다른 아군 체력 +{healAmount}";

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnHit || context.IsSimulation || !context.IsEffected) return;
            s_healAmount = healAmount;

            // 눈사람이 받은 피해 누적 (공격 취소 판정용)
            if (context.Target == owner)
            {
                SnowSet.AddDamageTaken(owner as Monster, Mathf.RoundToInt(context.OutputValue));
            }

            // 눈사람의 공격이 적중하면 다른 아군 회복
            if (context.SourceUnit == owner)
            {
                HealAllies(owner as Monster);
            }
        }

        // 턴 종료 시 누적 피해 초기화
        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger == CombatTrigger.OnPostAction &&
                context.Phase == EventPhase.TurnEnd &&
                context.SourceUnit == owner)
            {
                SnowSet.ResetDamageTaken(owner as Monster);
            }
        }

        /// <summary>owner를 제외한 모든 아군 몬스터를 healAmount만큼 회복.</summary>
        public static void HealAllies(Monster snowman)
        {
            if (snowman == null) return;
            foreach (var ally in SnowSet.OtherAliveMonsters(snowman))
                ally.Heal(s_healAmount);
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
