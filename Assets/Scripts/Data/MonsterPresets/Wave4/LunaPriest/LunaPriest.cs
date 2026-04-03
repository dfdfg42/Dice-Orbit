using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Monsters;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;
using static UnityEngine.UI.GridLayoutGroup;

namespace DiceOrbit.Data.MonsterPresets.Wave4.LunaPriest
{
    // ==========================================
    // 1. 루나 프리스트 스킬 구현
    // ==========================================
    [System.Serializable]
    public class LunaPriestSkill1 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 20;

        public LunaPriestSkill1()
        {
            skillName = "월식";
            description = $"무작위 짝수 타일 + 좌우 2칸에 {damage} 피해";
        }

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null) return new List<TileData>();

            var allTiles = orbitManager.Tiles;
            var evenTiles = allTiles.Where(t => t.TileIndex % 2 == 0).ToList();

            if (evenTiles.Count == 0) return new List<TileData>();

            var selectedTile = evenTiles[Random.Range(0, evenTiles.Count)];
            int centerIndex = selectedTile.TileIndex;
            int totalTiles = allTiles.Count;

            List<TileData> targetTiles = new List<TileData>();

            // center를 기준으로 좌우 2칸 (총 5칸) 
            for (int i = -2; i <= 2; i++)
            {
                // 음수 인덱스 처리 및 타일 개수에 따른 모듈로 연산
                int targetIndex = (centerIndex + i) % totalTiles;
                if (targetIndex < 0) targetIndex += totalTiles;

                targetTiles.Add(orbitManager.GetTile(targetIndex));
            }

            return targetTiles.Distinct().ToList();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            AttackTiles(source, targetTiles, damage);
        }
    }

    [System.Serializable]
    public class LunaPriestSkill2 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 10;
        [Tooltip("달의 기사 방어도 부여량")]
        [SerializeField] private int shieldAmount = 20;

        public LunaPriestSkill2()
        {
            skillName = "서늘한 달빛";
            description = $"짝수 타일에 있는 무작위 적에게 {damage}피해 및 달의 기사 일시 방어도 {shieldAmount} 부여";
        }

        // 사용자가 외부에서 구현할 함수 뼈대
        private Unit FindLunaKnight()
        {
            foreach (var monster in CombatManager.Instance?.ActiveMonsters)
            {
                if (monster.Stats.MonsterName == "달의 기사") // 이름이나 태그 등으로 식별
                {
                    return monster;
                }
            }
            return null;
        }

        private void ApplyShield(Unit target, int amount)
        {
            if (target == null) return;
            target.Stats.TempArmor += amount;
            Debug.Log($"[{SkillName}] Applied {amount} shield to {target.name}");
        }

        public override List<Unit> GetCustomTargets(MonsterSkill skill, Monster owner)
        {
            var aliveCharacters = PartyManager.Instance?.GetAliveCharacters();
            if (aliveCharacters == null) return new List<Unit>();

            // 짝수 타일에 있는 적 (Character) 찾기
            var evenTileChars = aliveCharacters.Where(c => c.CurrentTile != null && c.CurrentTile.TileIndex % 2 == 0).ToList();
            if (evenTileChars.Count > 0)
            {
                // 무작위로 한 명을 선택
                var randomChar = evenTileChars[Random.Range(0, evenTileChars.Count)];
                return new List<Unit> { randomChar };
            }

            return new List<Unit>();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            // 1. 짝수 타일 타겟에게 데미지 (존재할 경우에만 실행)
            if (targetUnits != null && targetUnits.Count > 0)
            {
                AttackUnits(source, targetUnits, damage);
            }

            // 2. 달의 기사 방어도 부여
            Unit lunaKnight = FindLunaKnight();
            if (lunaKnight != null && lunaKnight.IsAlive)
            {
                ApplyShield(lunaKnight, shieldAmount);
            }
        }
    }

    // ==========================================
    // 2. 루나 프리스트 패시브 구현
    // ==========================================
    [System.Serializable]
    public class LunaPriestPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("적 1명당 부여할 피해량 증가 비율 (5 = 5%)")]
        [SerializeField] private int damageBuffPerEnemy = 5;

        public LunaPriestPassive()
        {
            passiveName = "만월";
            description = $"턴 시작시, 짝수 타일에 있는 적의 수 X {damageBuffPerEnemy}% 만큼 본인 및 달의 기사에게 피해량 증가 버프 부여";
            priority = 10;
            isStackable = false;
        }

        // 사용자가 외부에서 구현할 함수 뼈대
        private Unit FindLunaKnight()
        {
            foreach (var monster in CombatManager.Instance?.ActiveMonsters)
            {
                if (monster.Stats.MonsterName == "달의 기사")
                {
                    return monster;
                }
            }
            return null;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context?.Action == null) return;

            // 턴 시작 시점 실행
            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.OnStartTurn &&
                context.SourceUnit == owner)
            {
                var aliveCharacters = PartyManager.Instance?.GetAliveCharacters();
                if (aliveCharacters == null) return;

                // 짝수 타일에 있는 적의 수 계산
                int evenTileEnemyCount = aliveCharacters.Count(c => c.CurrentTile != null && c.CurrentTile.TileIndex % 2 == 0);

                if (evenTileEnemyCount > 0)
                {
                    int totalBuffAmount = evenTileEnemyCount * damageBuffPerEnemy;

                    // 본인 버프 부여
                    ApplyDamageBuff(owner, totalBuffAmount);

                    // 달의 기사 버프 부여
                    Unit lunaKnight = FindLunaKnight();
                    if (lunaKnight != null && lunaKnight.IsAlive)
                    {
                        ApplyDamageBuff(lunaKnight, totalBuffAmount-1);
                    }
                }
            }
        }

        private void ApplyDamageBuff(Unit target, int buffAmount)
        {
            if (target == null) return;
            target.StatusEffects.AddEffect(new LunaPriestBuff(buffAmount, 2));
            Debug.Log($"[{PassiveName}] Applied {(buffAmount * 100)}% damage increase buff to {target.name}");
        }

        public override bool AllowSamePassive(PassiveAbility incoming)
        {
            return false;
        }
    }
}

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 공격력 버프 (데미지 계산 시 추가)
    /// </summary>
    public class LunaPriestBuff : StatusEffect
    {
        public LunaPriestBuff(int value, int duration) : base(EffectType.BuffAttack, value, duration)
        {
            IsStackable = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            base.OnReact(trigger, context);
            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.Attack &&
                context.SourceUnit == Owner)
            {
                context.OutputValue += context.OutputValue * (Value / 100f);
            }
        }
    }
}