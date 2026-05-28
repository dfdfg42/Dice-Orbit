using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave2.MommyBear
{
    // ==========================================
    // 패턴 1 [울부 짖기]
    // ==========================================
    /// <summary>
    /// 무작위 타일(MonsterSkill 설정: RandomTiles + count 8)에 피해.
    /// </summary>
    [System.Serializable]
    public class MommyBearRoar : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;

        public MommyBearRoar()
        {
            skillName = "울부 짖기";
            description = "무작위 타일 8개에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 패턴 2 [곰은 사람을 찢어]
    // ==========================================
    /// <summary>
    /// 지난 턴에 아기곰을 마지막으로 공격한 캐릭터가 속한 타일 + 좌우 각각 2칸에 피해.
    /// 대상 타일은 GetCustomTiles에서 BearPackTracker.LastBabyBearAttacker 기준으로 직접 선정.
    /// </summary>
    [System.Serializable]
    public class MommyBearTear : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("중심 타일 기준 좌우 확장 칸 수")]
        [SerializeField] private int range = 2;

        public MommyBearTear()
        {
            skillName = "곰은 사람을 찢어";
            description = "지난 턴 아기곰을 마지막으로 공격한 캐릭터가 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            var result = new List<TileData>();

            var attacker = BearPackTracker.LastBabyBearAttacker;
            TileData center = (attacker != null && attacker.IsAlive) ? attacker.CurrentTile : null;

            // 마지막 공격자가 없거나 사망 시 무작위 생존 캐릭터로 폴백
            if (center == null)
            {
                var alive = PartyManager.Instance?.GetAliveCharacters();
                if (alive != null && alive.Count > 0)
                {
                    var pick = alive[Random.Range(0, alive.Count)];
                    center = pick != null ? pick.CurrentTile : null;
                }
            }

            if (center == null) return result;

            var set = new HashSet<TileData> { center };

            var t = center;
            for (int i = 0; i < range && t != null && t.NextTile != null; i++)
            {
                t = t.NextTile;
                set.Add(t);
            }

            t = center;
            for (int i = 0; i < range && t != null && t.PreviousTile != null; i++)
            {
                t = t.PreviousTile;
                set.Add(t);
            }

            result.AddRange(set);
            return result;
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 패시브 [분노]
    // ==========================================
    /// <summary>
    /// 아기곰이 피격당한 횟수당 엄마곰의 피해량이 일정량씩 영구 증가.
    /// </summary>
    [System.Serializable]
    public class RagePassive : PassiveAbility
    {
        [Header("Designer Tuning")]
        [Tooltip("아기곰 피격 1회당 추가 피해")]
        [SerializeField] private int damagePerHit = 3;

        public RagePassive()
        {
            passiveName = "분노";
            description = "아기곰이 피격당한 횟수당 피해량이 3씩 영구 증가";
            priority = 10;
            isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            BearPackTracker.EnsureWaveHook();
        }

        public override string GetDynamicDescription()
            => $"아기곰 피격 1회당 피해 +{damagePerHit} (현재 +{damagePerHit * BearPackTracker.BabyBearHits})";

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context?.Action == null || owner == null) return;

            if (trigger == CombatTrigger.OnCalculateOutput &&
                context.Action.Type == ActionType.Attack &&
                context.SourceUnit == owner)
            {
                context.OutputValue += damagePerHit * BearPackTracker.BabyBearHits;
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }
}
