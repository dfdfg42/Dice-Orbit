using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Tile;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.MonsterPresets.Wave5.FlameFairy
{
    /// <summary>[불장난] 턴시작: 무작위 2타일에 불꽃 설치. + 요정에 피해누적 상태 부여(불짚이기용).</summary>
    [System.Serializable]
    public class PlayingWithFirePassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [SerializeField] private int tileCount = 2;
        [SerializeField] private int fireDamage = 35;

        public PlayingWithFirePassive()
        {
            passiveName = "불장난";
            description = "턴 시작 시 무작위 2타일에 불꽃 설치";
            priority = 10; isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            // 불짚이기용 이번-턴-피해 누적 상태를 최초 1회 부여(영구, 자기-리셋). 이미 있으면 갱신(무해).
            if (Owner?.StatusEffects != null)
                Owner.StatusEffects.AddEffect(new FireDamageTakenStatus());
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            if (owner == null) return;
            if (trigger != CombatTrigger.OnPreAction || context.Phase != EventPhase.TurnStart || context.SourceUnit != owner) return;

            var orbit = GameManager.Instance?.GetOrbitManager();
            if (orbit == null) return;
            var candidates = orbit.Tiles.Where(t => t != null && !t.HasAttribute(TileAttributeType.Flame)).ToList();
            int place = Mathf.Min(tileCount, candidates.Count);
            for (int i = 0; i < place; i++)
            {
                int r = Random.Range(0, candidates.Count);
                candidates[r].AddAttribute(new FireTile(fireDamage));
                candidates.RemoveAt(r);
            }
            Debug.Log($"[불장난] 불꽃 {place}개 설치");
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>[불짚이기] 이번 턴 요정이 받은 누적 피해가 threshold↑면 취소, 아니면 무작위 1명에게 damage.</summary>
    [System.Serializable]
    public class KindlingSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 30;
        [Tooltip("이번 턴 받은 누적 피해가 이 값 이상이면 취소")]
        [SerializeField] private int cancelThreshold = 25;

        public KindlingSkill() { skillName = "불짚이기"; description = "무작위 1명 30 피해(이번 턴 25↑ 피해 시 취소)"; }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            int taken = source?.StatusEffects != null ? source.StatusEffects.GetEffectValue(EffectType.FireDamageTaken) : 0;
            if (taken >= cancelThreshold)
            {
                Debug.Log($"[불짚이기] 취소 — 이번 턴 누적 피해 {taken}");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }
}
