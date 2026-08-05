using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave7.Shared
{
    /// <summary>[성장의 활력] 식물 몬스터 패시브. 최초 활력 스택 initialVitality 부여.
    /// 활력 ≤7이면 받는 피해 +20%는 VitalityStatus가 직접 처리한다.</summary>
    [System.Serializable]
    public class GrowthVitalityPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int initialVitality = 15;

        public GrowthVitalityPassive()
        {
            passiveName = "성장의 활력";
            description = "최초 활력 15, 활력 7 이하면 받는 피해 +20%";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            FarmSet.EnsureWaveHook();
            FarmSet.EnsureVitality(Owner, initialVitality);
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[농장의 활력] 난쟁이 농부 패시브. 매 턴 시작 무작위 타일 tilesPerTurn개에 활력 타일 설치.
    /// 쇠스랑 취소 판정용 받은-피해 상태(BiteDamageTakenStatus)도 최초 1회 시드.</summary>
    [System.Serializable]
    public class FarmVitalityPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int tilesPerTurn = 1;

        public FarmVitalityPassive()
        {
            passiveName = "농장의 활력";
            description = "턴 시작 시 무작위 타일에 활력 타일 설치 (통과·턴 종료 시 모든 식물 몬스터 활력 -1)";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            FarmSet.EnsureWaveHook();
            if (Owner != null && Owner.StatusEffects != null && !Owner.StatusEffects.HasEffect(EffectType.BiteDamageTaken))
                Owner.StatusEffects.AddEffect(new BiteDamageTakenStatus());
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null || orbit.Tiles == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Vitality)).ToList();
            int place = Mathf.Min(tilesPerTurn, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new VitalityTile());
                candidates.RemoveAt(r);
            }
            if (place > 0) Debug.Log($"[농장의 활력] 활력 타일 {place}개 설치");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>공용 타일 피해 스킬 — 대상 타일(MonsterSkill 타깃팅)에 damage. 포식/몸통박치기/허수아비 때리기/포자 살포 등.</summary>
    [System.Serializable]
    public class FarmDamageSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private string skillLabel = "공격";
        [SerializeField] private int damage = 20;

        public override string SkillName => string.IsNullOrEmpty(skillLabel) ? "공격" : skillLabel;
        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
            => AttackTiles(source, targetTiles, damage);
    }

    /// <summary>[비료] 모든 아군(활력 보유 몬스터)의 활력 스택 +amount.</summary>
    [System.Serializable]
    public class FertilizerSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int amount = 1;

        public FertilizerSkill() { skillName = "비료"; description = "모든 아군의 활력 스택 +1"; }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
            => FarmSet.ChangeVitalityAll(amount);
    }

    /// <summary>[농장 지키기] 체력이 가장 낮은 아군에게 일시 방어도 armor.</summary>
    [System.Serializable]
    public class FarmGuardSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int armor = 5;

        public FarmGuardSkill() { skillName = "농장 지키기"; description = "체력이 가장 낮은 아군에게 일시 방어도 +5"; }

        public override int GetPreviewDamage() => 0;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var ally = FarmSet.FindLowestHpAlly(source as Monster);
            if (ally != null && ally.Stats != null)
            {
                ally.Stats.TempArmor += armor;
                Debug.Log($"[농장 지키기] {ally.Stats.MonsterName} 방어도 +{armor}");
            }
        }
    }

    /// <summary>[덩굴 묶기] 활력 타일 및 좌우 range칸의 적에게 다음 턴 이동 불가(Bind). Custom 타깃팅.</summary>
    [System.Serializable]
    public class VineBindSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int bindDuration = 2;
        [SerializeField] private int range = 2;

        public VineBindSkill() { skillName = "덩굴 묶기"; description = "활력 타일 및 좌우 2칸의 적에게 다음 턴 이동 불가"; }

        public override int GetPreviewDamage() => 0;

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null || orbit.Tiles == null) return new List<TileData>();
            int total = orbit.Tiles.Count;
            var result = new HashSet<TileData>();
            foreach (var vt in orbit.Tiles.Where(t => t != null && t.HasAttribute(TileAttributeType.Vitality)))
                for (int i = -range; i <= range; i++)
                {
                    int idx = (vt.TileIndex + i) % total;
                    if (idx < 0) idx += total;
                    var tile = orbit.GetTile(idx);
                    if (tile != null) result.Add(tile);
                }
            return result.ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (targetTiles == null) return;
            var tileSet = new HashSet<TileData>(targetTiles);
            var chars = PartyManager.Instance?.GetAliveCharacters();
            if (chars == null) return;
            foreach (var c in chars)
                if (c != null && c.IsAlive && c.CurrentTile != null && tileSet.Contains(c.CurrentTile) && c.StatusEffects != null)
                    c.StatusEffects.AddEffect(new BindStatus(0, bindDuration));
        }
    }

    /// <summary>[쇠스랑] 무작위 대상 1명에게 damage. 단, 이번 턴 받은 누적 피해 ≥ cancelThreshold면 취소. (깨물기와 동일 메커니즘)</summary>
    [System.Serializable]
    public class PitchforkSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 20;
        [Tooltip("이번 턴 받은 누적 피해가 이 값 이상이면 취소")]
        [SerializeField] private int cancelThreshold = 15;

        public PitchforkSkill() { skillName = "쇠스랑"; description = "무작위 1명 20 피해(이번 턴 15↑ 피해 시 취소)"; }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            int taken = source != null && source.StatusEffects != null
                ? source.StatusEffects.GetEffectValue(EffectType.BiteDamageTaken) : 0;
            if (taken >= cancelThreshold)
            {
                Debug.Log($"[쇠스랑] 취소 — 이번 턴 누적 피해 {taken}");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }

    /// <summary>아군 사망 분기 패턴(허수아비/농부). 아군 사망 전 → index 0 ONLY, 사망 후 → [0]/[1] 랜덤.</summary>
    [System.Serializable]
    public class AllyDeathGuardPattern : DiceOrbit.Data.MonsterAI.MonsterAI
    {
        public override MonsterSkill GetNextSkill()
        {
            if (availableSkills == null || availableSkills.Count == 0) return null;
            if (!FarmSet.HasAnyAllyDied()) return availableSkills[0];
            int idx = availableSkills.Count >= 2 ? Random.Range(0, 2) : 0;
            return availableSkills[idx];
        }
    }
}
