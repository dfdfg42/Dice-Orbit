using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;
using DiceOrbit.Data.MonsterPresets.Wave5.Shared;

namespace DiceOrbit.Data.MonsterPresets.Wave5.FlameMusicBox
{
    /// <summary>[불의 노래] 턴시작: 무작위 기존 불꽃 타일 하나의 좌우 ±1(2타일)에 불꽃 설치. 불꽃 없으면 skip.</summary>
    [System.Serializable]
    public class FlameSongPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int fireDamage = 35;

        public FlameSongPassive()
        {
            passiveName = "불의 노래";
            description = "턴 시작 시 무작위 불꽃 타일 좌우 1칸에 불꽃 설치";
            priority = 10; isStackable = false;
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            var flames = orbit.Tiles.Where(t => t != null && t.HasAttribute(TileAttributeType.Flame)).ToList();
            if (flames.Count == 0) return; // 기존 불꽃 없으면 skip

            int total = orbit.Tiles.Count;
            var center = flames[Random.Range(0, flames.Count)];
            foreach (int off in new[] { -1, 1 })
            {
                int idx = (center.TileIndex + off) % total;
                if (idx < 0) idx += total;
                var tile = orbit.GetTile(idx);
                if (tile != null && !tile.HasAttribute(TileAttributeType.Flame))
                    tile.AddAttribute(new FireTile(fireDamage));
            }
            Debug.Log("[불의 노래] 불꽃 좌우 확산");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[타오르는 열기] 불꽃 소녀 + 무작위 아군 1명의 피해량 +amount 영구.</summary>
    [System.Serializable]
    public class BurningHeatSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int amount = 3;

        public BurningHeatSkill() { skillName = "타오르는 열기"; description = "불꽃 소녀와 무작위 아군의 피해량 +3 (영구)"; }

        private void Buff(Monster m)
        {
            if (m == null || !m.IsAlive || m.StatusEffects == null) return;
            m.StatusEffects.AddEffect(new BuffAttackStatus(amount, -1) { IsStackable = true });
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            Buff(FlameSet.FindBoss());
            var monsters = CombatManager.Instance?.ActiveMonsters?.Where(m => m != null && m.IsAlive).ToList();
            if (monsters != null && monsters.Count > 0)
                Buff(monsters[Random.Range(0, monsters.Count)]);
            Debug.Log($"[타오르는 열기] 보스+무작위 아군 피해량 +{amount}");
        }
    }
}
