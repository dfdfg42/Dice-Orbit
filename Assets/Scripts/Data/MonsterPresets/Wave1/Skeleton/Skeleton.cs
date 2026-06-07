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
    // ==========================================
    // 패턴 1 [뼈 화살]
    // ==========================================
    /// <summary>
    /// 턴 시작 기준 무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해.
    /// 대상/범위는 MonsterSkill 설정으로 결정 (RandomCharacter + Tiles + range 2).
    /// </summary>
    [System.Serializable]
    public class SkeletonWhip : SkillData
    {
        [Header("Skill Settings")]
        [SerializeField] private int damage = 15;

        public SkeletonWhip()
        {
            skillName = "뼈 화살";
            description = "무작위 대상 1명이 속한 타일 + 좌우 각각 2칸에 피해";
        }

        public override int GetPreviewDamage() => damage;

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    // ==========================================
    // 패턴 2 [칼슘 충전]
    // ==========================================
    /// <summary>
    /// 자기 강화: 다음 턴 피해량 +5(공격력 버프) 및 일시 방어도 +5.
    /// 타겟 없는 자기 버프이므로 MonsterSkill 설정: TargetType=Self, IntentType=Buff.
    /// </summary>
    [System.Serializable]
    public class CalciumChargeSkill : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("다음 턴까지 부여할 공격력 버프")]
        [SerializeField] private int attackBuff = 5;
        [Tooltip("부여할 일시 방어도")]
        [SerializeField] private int armorGain = 5;

        public CalciumChargeSkill()
        {
            skillName = "칼슘 충전";
            description = "다음 턴 피해량 +5 및 일시 방어도 +5 부여";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            if (source == null || !source.IsAlive) return;

            // 다음 턴 공격까지 살아남도록 duration 2 (자기 턴 시작 시 1 감소 → 다음 공격 적용 → 그 다음 턴 시작 시 만료)
            source.StatusEffects?.AddEffect(StatusEffectManager.CreateEffect(EffectType.BuffAttack, attackBuff, 2));

            // 일시 방어도 부여 (다음 자기 턴 시작 시 초기화됨)
            if (source.Stats != null)
                source.Stats.TempArmor += armorGain;

            Debug.Log($"[칼슘 충전] {source.name} 공격력 +{attackBuff}, 방어도 +{armorGain}");
        }
    }

    // ==========================================
    // 패시브 [뼈 무덤]
    // ==========================================
    /// <summary>
    /// 웨이브 시작 시 4, 9, 14, 19 타일에 뼈 타일을 생성한다.
    /// 캐릭터가 지나가거나 턴 종료 시 해골 병사에게 일시 방어도를 부여한다.
    /// 타일은 영구 유지되며 해골 병사 사망 시 SkeletonDeath가 제거한다.
    /// </summary>
    [System.Serializable]
    public class PlantBonePassive : PassiveAbility
    {
        [Header("Bone Settings")]
        [Tooltip("뼈 타일이 부여하는 일시 방어도")]
        [SerializeField] private int armorAmount = 10;

        private WaveManager hookedManager;

        public PlantBonePassive()
        {
            passiveName = "뼈 무덤";
            description = "웨이브 시작 시 4, 9, 14, 19 타일에 뼈 타일 생성. 통과/턴 종료 시 해골 병사 방어도 +10";
            priority = 10;
            isStackable = false;
        }

        public override string Description => $"웨이브 시작 시 뼈 타일 생성. 통과/턴 종료 시 해골 병사 방어도 +{armorAmount}";

        public override void Initialize(Unit Owner)
        {
            base.Initialize(Owner);
            SubscribeWaveStart();

            // 웨이브 진행 중에 합류한 경우 즉시 설치
            if (WaveManager.Instance != null && WaveManager.Instance.IsWaveActive)
                PlantBones();
        }

        private void SubscribeWaveStart()
        {
            var wm = WaveManager.Instance;
            if (wm == null) return;
            if (hookedManager == wm) return;

            if (hookedManager != null) hookedManager.OnWaveStart -= HandleWaveStart;
            wm.OnWaveStart += HandleWaveStart;
            hookedManager = wm;
        }

        private void HandleWaveStart(int wave)
        {
            if (owner == null || !owner.IsAlive) return;
            PlantBones();
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context) { }

        private void PlantBones()
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null) return;

            var skeleton = owner as Monster;

            for (int index = 4; index < 20; index += 5)
            {
                var tile = orbitManager.GetTile(index);
                if (tile == null) continue;
                if (tile.HasAttribute(TileAttributeType.Bone)) continue;

                tile.AddAttribute(new BoneTile(TileAttributeType.Bone, armorAmount, -1, skeleton));
            }
        }

        public override bool AllowSamePassive(IPassive incoming) => false;
    }

    // ==========================================
    // 사망 효과
    // ==========================================
    /// <summary>
    /// 해골 병사 사망 시 모든 뼈 타일을 제거한다.
    /// </summary>
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
