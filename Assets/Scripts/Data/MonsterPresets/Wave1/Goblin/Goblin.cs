using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Visuals;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Goblin
{
    // ==========================================
    // 웨이브 단위 지뢰밭 정리 훅
    // ==========================================
    /// <summary>
    /// 지뢰는 고블린 사망 후에도 유지되고, 웨이브 종료(=다음 웨이브 시작) 시에만 정리된다.
    /// WaveManager.OnWaveStart 에 한 번만 구독해 잔여 지뢰 타일을 제거한다.
    /// </summary>
    public static class MineFieldCleaner
    {
        private static WaveManager hookedManager;

        public static void EnsureWaveHook()
        {
            var wm = WaveManager.Instance;
            if (wm == null) return;
            if (hookedManager == wm) return;

            if (hookedManager != null) hookedManager.OnWaveStart -= OnWaveStart;
            wm.OnWaveStart += OnWaveStart;
            hookedManager = wm;
        }

        private static void OnWaveStart(int wave)
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit?.Tiles == null) return;

            foreach (var tile in orbit.Tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.RandMine);
        }
    }

    // ==========================================
    // 패턴 1 [몽둥이 질]
    // ==========================================
    /// <summary>
    /// 턴 시작 기준 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해.
    /// 대상/범위는 MonsterSkill 설정으로 결정 (RandomCharacter + Tiles + range 2).
    /// </summary>
    [System.Serializable]
    public class GoblinClubSwing : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public GoblinClubSwing()
        {
            skillName = "몽둥이 질";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 패턴 2 [지뢰 폭발]
    // ==========================================
    /// <summary>
    /// 설치된 지뢰 타일 + 좌우 각각 한 칸에 피해를 주고, 폭발한 지뢰 타일을 제거한다.
    /// 대상 타일은 MonsterSkill 설정으로 결정 (TilesWithAttribute=RandMine + range 1).
    /// </summary>
    [System.Serializable]
    public class MineBombSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 10;
        [SerializeField] private DiceOrbit.Visuals.CombatVfxProfile vfxProfile;

        public MineBombSkill()
        {
            skillName = "지뢰 폭발";
            description = "설치된 지뢰 타일 + 좌우 각각 한 칸에 피해 (발동 후 지뢰 삭제)";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;

            var affectedTiles = targetTiles != null
                ? targetTiles.Where(tile => tile != null).Distinct().ToList()
                : new List<TileData>();

            var mineTiles = affectedTiles
                .Where(tile => tile.HasAttribute(TileAttributeType.RandMine))
                .ToList();

            if (affectedTiles.Count == 0) return;

            VfxManager.PlayCast(vfxProfile, source);
            foreach (var tile in affectedTiles)
                VfxManager.PlayTile(vfxProfile, tile);

            foreach (var character in partyManager.GetAliveCharacters())
            {
                if (character == null || !character.IsAlive) continue;
                if (character.CurrentTile == null || !affectedTiles.Contains(character.CurrentTile)) continue;

                var context = new AttackContext(source, character, SkillName, damage);
                if (vfxProfile != null) context.AddTag("CustomVfx");
                CombatPipeline.Instance?.Process(context);

                if (context.IsEffected) VfxManager.PlayHit(vfxProfile, character);
            }

            // 폭발 후 지뢰 속성 제거
            foreach (var mineTile in mineTiles)
            {
                var mines = mineTile.GetAttributes()
                    .Where(attr => attr != null && attr.Type == TileAttributeType.RandMine)
                    .ToList();
                foreach (var mine in mines)
                    mineTile.RemoveAttribute(mine);
            }
        }
    }

    // ==========================================
    // 패시브 [지뢰 설치]
    // ==========================================
    /// <summary>
    /// 매 턴 시작 시 무작위 타일 2개에 지뢰를 설치한다.
    /// 같은 타일에 겹쳐 설치되면 피해가 누적된다(중첩).
    /// 지뢰는 고블린 사망 후에도 유지되고 웨이브 종료 시 정리된다(MineFieldCleaner).
    /// </summary>
    [System.Serializable]
    public class PlantMinePassive : PassiveAbility
    {
        [Header("Mine Settings")]
        [Tooltip("설치할 지뢰의 피해량")]
        [SerializeField] private int mineDamage = 30;

        [Tooltip("매 턴 설치할 지뢰 개수")]
        [SerializeField] private int minesPerTurn = 2;

        public PlantMinePassive()
        {
            passiveName = "지뢰 설치";
            description = "매 턴 시작 시 무작위 타일에 지뢰를 설치합니다. 지나가거나 턴 종료 시 피해, 발동 후 삭제";
            priority = 10;
            isStackable = false;
        }

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            MineFieldCleaner.EnsureWaveHook();
        }

        public override string GetDynamicDescription()
            => $"매 턴 시작 시 무작위 타일 {minesPerTurn}개에 {mineDamage} 피해 지뢰 설치 (중첩 가능)";

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context is TurnEventContext { Phase: EventPhase.TurnStart } && context.SourceUnit == owner && trigger == CombatTrigger.OnPreAction)
            {
                PlantMines();
            }
        }

        private void PlantMines()
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null) return;

            int tileCount = orbitManager.TileCount;
            if (tileCount <= 0) return;

            var chosen = new HashSet<int>();
            int target = Mathf.Min(minesPerTurn, tileCount);
            int safety = 0;
            while (chosen.Count < target && safety++ < tileCount * 4)
                chosen.Add(Random.Range(0, tileCount));

            foreach (var index in chosen)
            {
                var tile = orbitManager.GetTile(index);
                if (tile == null) continue;

                // 이미 지뢰가 있으면 피해를 누적(중첩), 없으면 새로 설치
                var existing = tile.GetAttributes().FirstOrDefault(a => a != null && a.Type == TileAttributeType.RandMine);
                if (existing != null)
                {
                    existing.AddStack(mineDamage);
                }
                else
                {
                    tile.AddAttribute(new RandMineTile(TileAttributeType.RandMine, mineDamage, -1, isStackable: true));
                }
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    // ==========================================
    // 사망 효과
    // ==========================================
    /// <summary>
    /// 고블린 사망 효과. 지뢰는 사망 후에도 유지되므로 여기서 제거하지 않는다.
    /// (지뢰 정리는 웨이브 종료 시 MineFieldCleaner가 담당)
    /// </summary>
    [System.Serializable]
    public class GoblinDeath : DeathEffect
    {
        public GoblinDeath()
        {
            effectName = "Goblin Death";
            description = "고블린이 죽을 때 발동하는 효과";
        }

        public override void Execute(Monster deadMonster)
        {
            Debug.Log($"[GoblinDeath] {deadMonster.name} died. (지뢰는 웨이브 종료 시 정리)");
        }
    }
}
