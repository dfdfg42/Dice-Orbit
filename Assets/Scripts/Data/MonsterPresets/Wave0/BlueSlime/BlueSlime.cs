using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.MonsterPresets.Wave0.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave0.BlueSlime
{
    /// <summary>
    /// [점액] 턴 종료 시 무작위 tileCount 타일에 점액 설치. + 파란 슬라임이 받은 피해를 누적([박치기] 취소용),
    /// 턴 종료 시 누적 초기화.
    /// </summary>
    [System.Serializable]
    public class PlantSlimePassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int tileCount = 3;
        [SerializeField] private int weakenPercent = 20;
        [SerializeField] private int weakenDuration = 2;

        public PlantSlimePassive()
        {
            passiveName = "점액";
            description = "턴 종료 시 무작위 3타일에 점액 설치(밟으면 쇠약)";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            SlimeSet.EnsureWaveHook();
        }

        public override void OnAttack(CombatTrigger trigger, AttackContext context)
        {
            if (owner == null) return;
            // 파란 슬라임이 실제로 피해를 받으면 누적
            if (trigger == CombatTrigger.OnHit && !context.IsSimulation && context.IsEffected && context.Target == owner)
                SlimeSet.AddDamageTaken(owner as Monster, Mathf.RoundToInt(context.OutputValue));
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPostAction || context.Phase != EventPhase.TurnEnd || context.SourceUnit != owner) return;

            PlantSlime();
            SlimeSet.ResetDamageTaken(owner as Monster);
        }

        private void PlantSlime()
        {
            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Slime)).ToList();
            int place = Mathf.Min(tileCount, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new SlimeTile(weakenPercent, weakenDuration));
                candidates.RemoveAt(r);
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[박치기] 무작위 대상 1명에게 damage. 이번 라운드 받은 누적 피해 ≥ cancelThreshold면 취소.</summary>
    [System.Serializable]
    public class BodySlamSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;
        [Tooltip("이 피해 이상 받으면 취소")]
        [SerializeField] private int cancelThreshold = 10;

        public BodySlamSkill()
        {
            skillName = "박치기";
            description = "무작위 대상 1명에게 피해 (이번 라운드 10 이상 받으면 취소)";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var slime = source as Monster;
            if (slime != null && SlimeSet.GetDamageTaken(slime) >= cancelThreshold)
            {
                Debug.Log($"[박치기] {source.name} 이번 라운드 피해 {SlimeSet.GetDamageTaken(slime)} ≥ {cancelThreshold} → 취소");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }

    /// <summary>[점액 분사] 설치된 점액 타일 + 좌우 각각 1칸에 damage 피해. (TilesWithAttribute=Slime + range 1)</summary>
    [System.Serializable]
    public class SlimeSpraySkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public SlimeSpraySkill()
        {
            skillName = "점액 분사";
            description = "설치된 점액 타일 + 좌우 각각 한 칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }
}
