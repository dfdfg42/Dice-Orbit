using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;
using System.Collections.Generic;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave1.Skeleton
{
    /// <summary>[뼈 검] 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해. (RandomCharacter + Tiles + range 2)</summary>
    [System.Serializable]
    public class SkeletonWhip : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public SkeletonWhip()
        {
            skillName = "뼈 검";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    /// <summary>[뼈 화살] 무작위 대상 1명에게 피해. 이번 라운드 뼈무덤 발동(BoneMark) 시 취소. (RandomCharacter + Characters)</summary>
    [System.Serializable]
    public class BoneArrowSkill : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public BoneArrowSkill()
        {
            skillName = "뼈 화살";
            description = "무작위 대상 1명에게 피해 (이번 라운드 뼈무덤 발동 시 취소)";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (source?.StatusEffects != null && source.StatusEffects.HasEffect(EffectType.BoneMark))
            {
                Debug.Log("[뼈 화살] 취소 — 이번 라운드 뼈무덤 발동");
                return;
            }
            AttackUnits(source, targetUnits, damage);
        }
    }

    /// <summary>
    /// [뼈 무덤] 웨이브 시작 시 4·10·16 타일에 뼈 타일. 통과/턴 종료 시 해골병사 방어도 +armorAmount(영구, 사망 시 삭제).
    /// 해골병사 턴 종료 시 BoneMark 마커를 리셋한다(이번 라운드 판정 종료).
    /// </summary>
    [System.Serializable]
    public class PlantBonePassive : PassiveAbility
    {
        [Header("Bone Settings")]
        [Tooltip("뼈 타일이 부여하는 일시 방어도")]
        [SerializeField] private int armorAmount = 5;

        private CombatManager hookedManager;

        public PlantBonePassive()
        {
            passiveName = "뼈 무덤";
            description = "웨이브 시작 시 4·10·16 타일에 뼈 타일 생성. 통과/턴 종료 시 해골 병사 방어도 +5";
            priority = 10;
            isStackable = false;
        }

        public override string Description => $"웨이브 시작 시 뼈 타일 생성. 통과/턴 종료 시 해골 병사 방어도 +{armorAmount}";

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            SubscribeCombatStart();
            if (CombatManager.Instance != null && CombatManager.Instance.InCombat)
                PlantBones();
        }

        private void SubscribeCombatStart()
        {
            var cm = CombatManager.Instance;
            if (cm == null) return;
            if (hookedManager == cm) return;
            if (hookedManager != null) hookedManager.OnCombatStart -= HandleCombatStart;
            cm.OnCombatStart += HandleCombatStart;
            hookedManager = cm;
        }

        private void HandleCombatStart()
        {
            if (owner == null || !owner.IsAlive) return;
            PlantBones();
        }

        private void PlantBones()
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null) return;

            var skeleton = owner as Monster;
            foreach (int index in new[] { 4, 10, 16 })
            {
                var tile = orbitManager.GetTile(index);
                if (tile == null) continue;
                if (tile.HasAttribute(TileAttributeType.Bone)) continue;
                tile.AddAttribute(new BoneTile(TileAttributeType.Bone, armorAmount, -1, skeleton));
            }
        }

        public override void OnTurnEvent(CombatTrigger trigger, TurnEventContext context)
        {
            // 해골병사 턴 종료 시 이번 라운드 뼈무덤 발동 마커 제거
            if (owner != null && trigger == CombatTrigger.OnPostAction
                && context.Phase == EventPhase.TurnEnd && context.SourceUnit == owner)
            {
                owner.StatusEffects?.RemoveEffect(EffectType.BoneMark);
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    /// <summary>해골 병사 사망 시 모든 뼈 타일을 제거한다.</summary>
    [System.Serializable]
    public class SkelettonDeath : DeathEffect
    {
        public SkelettonDeath()
        {
            effectName = "Skeleton Death";
            description = "해골 병사가 죽을 때 발동하는 효과";
        }

        public override void Execute(Monster deadMonster)
        {
            Debug.Log($"[SkeletonDeath] {deadMonster.name} died. 뼈 타일 제거.");
            var tiles = GameManager.Instance?.GetOrbitManager()?.Tiles;
            if (tiles == null) return;
            foreach (var tile in tiles)
                if (tile != null) tile.RemoveAttributeType(TileAttributeType.Bone);
        }
    }
}

namespace DiceOrbit.Systems.Effects
{
    /// <summary>뼈무덤 발동 마커: 존재 여부만 사용(전투 효과 없음). 해골병사 TurnEnd에 RemoveEffect로 제거.</summary>
    public class BoneMarkStatus : StatusEffect
    {
        public BoneMarkStatus() : base(DiceOrbit.Data.EffectType.BoneMark, 0, -1) { }
    }
}
