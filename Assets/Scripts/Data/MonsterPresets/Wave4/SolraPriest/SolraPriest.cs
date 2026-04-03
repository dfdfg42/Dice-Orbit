using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave4.SolraPriest
{
    // ==========================================
    // 1. 솔라 프리스트 스킬 구현
    // ==========================================
    [System.Serializable]
    public class SolraPriestSkill1 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 20;
        [Tooltip("스킬 범위")]
        [SerializeField] private int range = 2;
        public SolraPriestSkill1()
        {
            skillName = "일식";
            description = $"무작위 홀수 타일 + 좌우 {range}칸에 {damage}피해";
        }

        public override List<TileData> GetCustomTiles(MonsterSkill skill, Monster owner)
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null) return new List<TileData>();

            var allTiles = orbitManager.Tiles;
            var oddTiles = allTiles.Where(t => t.TileIndex % 2 == 1).ToList();

            if (oddTiles.Count == 0) return new List<TileData>();

            var selectedTile = oddTiles[Random.Range(0, oddTiles.Count)];
            int centerIndex = selectedTile.TileIndex;
            int totalTiles = allTiles.Count;

            List<TileData> targetTiles = new List<TileData>();

            // center를 기준으로 좌우 2칸 (총 5칸) 
            for (int i = -range; i <= range; i++)
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
    public class SolraPriestSkill2 : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 10;
        [Tooltip("기사 회복량")]
        [SerializeField] private int healAmount = 20;

        public SolraPriestSkill2()
        {
            skillName = "태양의 축복";
            description = $"홀수 타일에 있는 무작위 적에게 {damage}피해 및 태양의 기사 체력 {healAmount} 회복\n(홀수 타일에 적이 없으면 체력 회복만 진행)";
        }

        // 사용자가 외부에서 구현할 함수 뼈대
        private Unit FindSolraKnight()
        {
            foreach(var monster in CombatManager.Instance?.ActiveMonsters) {
                if (monster.Stats.MonsterName == "태양의 기사") // 이름이나 태그 등으로 식별
                {
                    return monster;
                }
            }
            return null;
        }

        public override List<Unit> GetCustomTargets(MonsterSkill skill, Monster owner)
        {
            var aliveCharacters = PartyManager.Instance?.GetAliveCharacters();
            if (aliveCharacters == null) return new List<Unit>();

            // 홀수 타일에 있는 적 (Character) 찾기
            var oddTileChars = aliveCharacters.Where(c => c.CurrentTile != null && c.CurrentTile.TileIndex % 2 == 1).ToList();
            if (oddTileChars.Count > 0)
            {
                // 무작위로 한 명을 선택
                var randomChar = oddTileChars[Random.Range(0, oddTileChars.Count)];
                return new List<Unit> { randomChar };
            }

            return new List<Unit>();
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            // 1. 홀수 타일 타겟에게 데미지 (존재할 경우에만 실행)
            if (targetUnits != null && targetUnits.Count > 0)
            {
                AttackUnits(source, targetUnits, damage);
            }

            // 2. 태양의 기사 체력 회복
            Unit solraKnight = FindSolraKnight();
            if (solraKnight != null && solraKnight.IsAlive)
            {
                solraKnight.Heal(healAmount);
                Debug.Log($"[{SkillName}] Solra Knight healed for {healAmount}");
            }
        }
    }

    // ==========================================
    // 2. 솔라 프리스트 패시브 구현
    // ==========================================
    [System.Serializable]
    public class SolraPriestPassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("적 1명당 부여할 방어도")]
        [SerializeField] private int shieldPerEnemy = 5;

        public SolraPriestPassive()
        {
            passiveName = "태양의 가호";
            description = $"턴 종료시, 홀수 타일에 있는 적의 수 X {shieldPerEnemy} 만큼 본인 및 태양의 기사에게 일시 방어도 부여";
            priority = 10;
            isStackable = false;
        }

        // 사용자가 외부에서 구현할 함수 뼈대
        private Unit FindSolraKnight()
        {
            // TODO: 태양의 기사를 맵이나 보스 파티에서 탐색하여 반환하는 로직 작성
            return null;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context?.Action == null) return;

            // 턴 시작 시점 실행
            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.OnEndTurn &&
                context.SourceUnit == owner)
            {
                var aliveCharacters = PartyManager.Instance?.GetAliveCharacters();
                if (aliveCharacters == null) return;

                // 홀수 타일에 있는 적의 수 계산
                int oddTileEnemyCount = aliveCharacters.Count(c => c.CurrentTile != null && c.CurrentTile.TileIndex % 2 == 1);
                int totalShield = oddTileEnemyCount * shieldPerEnemy;

                if (totalShield > 0)
                {
                    // 본인 방어도 부여
                    ApplyShield(owner, totalShield);

                    // 태양의 기사 방어도 부여
                    Unit solraKnight = FindSolraKnight();
                    if (solraKnight != null && solraKnight.IsAlive)
                    {
                        ApplyShield(solraKnight, totalShield);
                    }
                }
            }
        }

        private void ApplyShield(Unit target, int shieldAmount)
        {
            if (target == null) return;
            target.Stats.TempArmor += shieldAmount;
            Debug.Log($"[{PassiveName}] Applied {shieldAmount} shield to {target.name}");
        }

        public override bool AllowSamePassive(PassiveAbility incoming)
        {
            return false;
        }
    }
}
