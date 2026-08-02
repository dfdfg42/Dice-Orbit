using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.MonsterPresets.Wave4.Shared
{
    /// <summary>
    /// 진영 지원 스킬 (흑점=방어도 / 만월=회복 공용). 공격 대신 같은 진영 몬스터 전원을 지원한다.
    /// MonsterSkill 설정 권장: TargetStrategy=Self, TargetType=Self, IntentType=Defend(흑점)/Buff(만월).
    /// </summary>
    [System.Serializable]
    public class FactionSupportSkill : SkillData
    {
        public enum SupportKind { Armor, Heal }

        [Header("Support Settings")]
        [Tooltip("스킬 표시 이름 (예: 흑점, 만월)")]
        [SerializeField] private string skillLabel = "지원";
        [Tooltip("Armor=같은 진영 일시 방어도, Heal=같은 진영 체력 회복")]
        [SerializeField] private SupportKind kind = SupportKind.Armor;
        [Tooltip("방어도/회복량")]
        [SerializeField] private int amount = 10;

        public override string SkillName => string.IsNullOrEmpty(skillLabel) ? "지원" : skillLabel;
        public override string Description => kind == SupportKind.Armor
            ? $"같은 진영 전체에 일시 방어도 +{amount}"
            : $"같은 진영 전체 체력 +{amount} 회복";

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var faction = (source as Monster)?.Faction ?? MonsterFaction.None;
            if (faction == MonsterFaction.None) return;

            var monsters = CombatManager.Instance?.ActiveMonsters;
            if (monsters == null) return;

            foreach (var m in monsters)
            {
                if (m == null || !m.IsAlive || m.Stats == null) continue;
                if (m.Faction != faction) continue;

                if (kind == SupportKind.Armor)
                    m.Stats.TempArmor += amount;
                else
                    m.Heal(amount);
            }
            Debug.Log($"[{SkillName}] {faction} 진영 {(kind == SupportKind.Armor ? $"방어도 +{amount}" : $"체력 +{amount}")}");
        }
    }
}
