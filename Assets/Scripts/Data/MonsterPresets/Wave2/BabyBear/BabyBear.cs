using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave2.BabyBear
{
    // ==========================================
    // 1. 아기곰 스킬 구현
    // ==========================================
    /// <summary>
    /// 몬스터가 사용할 스킬입니다. SkillData를 상속받습니다.
    /// 에디터의 "AI Pattern" 섹션에서 설정할 수 있습니다.
    /// </summary>
    [System.Serializable]
    public class BabyBearAttack : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 15;

        /// <summary>
        /// 생성자에서는 스킬의 이름과 설명을 초기화해야 합니다.
        /// 이를 생략하면 게임 내 툴팁 등에서 내용이 비어보입니다.
        /// </summary>
        public BabyBearAttack()
        {
            skillName = "영역 침범";
            description = $"설치된 꿀 타일 + 좌우 한칸에 {damage} 피해.";
        }

        /// <summary>
        /// 실제 스킬이 발동될 때 실행되는 로직입니다.
        /// </summary>
        /// <param name="source">스킬을 사용하는 주체 (몬스터)</param>
        /// <param name="targetUnits">타겟팅 된 유닛들 리스트</param>
        /// <param name="targetTiles">타겟팅 된 타일들 리스트</param>
        /// <param name="diceValue">스킬 발동 시 계산된 주사위 값 (필요시 사용)</param>
        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;

            // 1. 해당 영역(꿀 타일 + 반경) 타일 목록이 없으면 무시
            if (targetTiles == null || targetTiles.Count == 0) return;

            // 2. 살아있는 모든 아군(Character) 탐색
            var aliveCharacters = partyManager.GetAliveCharacters();
            
            foreach (var character in aliveCharacters)
            {
                if (character == null || !character.IsAlive) continue;
                
                // 3. 캐릭터가 서 있는 타일이, 앞서 타겟으로 지정된 targetTiles 범주 안에 들어있다면 데미지 적용!
                if (character.CurrentTile != null && targetTiles.Contains(character.CurrentTile))
                {
                    var action = new CombatAction(SkillName, ActionType.Attack, damage);
                    var context = new CombatContext(source, character, action);
                    
                    CombatPipeline.Instance?.Process(context);

                    Debug.Log($"[{SkillName}] {source.name} attacks {character.name} for {damage} damage");
                }
            }
        }
    }

    [System.Serializable]
    public class EatHoney : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("체력 회복량")]
        [SerializeField] private int healamount = 15;

        /// <summary>
        /// 생성자에서는 스킬의 이름과 설명을 초기화해야 합니다.
        /// 이를 생략하면 게임 내 툴팁 등에서 내용이 비어보입니다.
        /// </summary>
        public EatHoney()
        {
            skillName = "꿀 먹기";
            description = $"설치된 꿀 타일 중 하나를 소모해 체력을 {healamount} 회복한다.";
        }

        /// <summary>
        /// 실제 스킬이 발동될 때 실행되는 로직입니다.
        /// </summary>
        /// <param name="source">스킬을 사용하는 주체 (몬스터)</param>
        /// <param name="targetUnits">타겟팅 된 유닛들 리스트</param>
        /// <param name="targetTiles">타겟팅 된 타일들 리스트</param>
        /// <param name="diceValue">스킬 발동 시 계산된 주사위 값 (필요시 사용)</param>
        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            foreach (var target in targetTiles)
            {
                if (target == null) continue;
                // 타일에 꿀 타일이 있는지 확인
                if (target.HasAttribute(TileAttributeType.Honey))
                {
                    // 꿀 타일 제거
                    target.RemoveAttributeType(TileAttributeType.Honey);
                    Debug.Log($"[EatHoney] {source.name} consumes a honey tile at index {target.TileIndex}");
                    break; // 하나만 소비하므로 루프 종료
                }
            }
            foreach (var target in targetUnits)
            {
                if (target == null || !target.IsAlive) continue;

                // 1. 공격 정보를 담을 CombatAction 생성
                // (이름, 타입, 매개변수(damage 등))
                var action = new CombatAction(SkillName, ActionType.Heal, healamount);

                // 2. 파이프라인에 전달할 CombatContext 생성
                var context = new CombatContext(source, target, action);

                // 3. 전투 파이프라인(CombatPipeline)을 통해 처리를 요쳥 (패시브 등이 중간에 개입할 수 있음)
                CombatPipeline.Instance?.Process(context);

                Debug.Log($"[{SkillName}] {source.name} heals");
            }
        }
    }

    // ==========================================
    // 2. 아기곰 사망 효과 구현
    // ==========================================
    /// <summary>
    /// 몬스터가 사망했을 때 발생하는 효과입니다. DeathEffect를 상속받습니다.
    /// </summary>
    //[System.Serializable]
    //public class BabyBearDeath : DeathEffect
    //{
    //    public BabyBearDeath()
    //    {
    //        effectName = "Baby Bear Death";
    //        description = "몬스터가 죽을 때 발동하는 아기곰 효과입니다.";
    //    }

    //    /// <summary>
    //    /// 사망 효과가 발동되는 로직입니다.
    //    /// </summary>
    //    /// <param name="deadMonster">죽은 몬스터 유닛 객체</param>
    //    public override void Execute(Monster deadMonster)
    //    {
    //        // 예시: 몬스터가 죽을 때 맵에 설치한 특정 타일 효과를 모두 지운다거나 아군에게 버프를 줄 수 있습니다.
    //        Debug.Log($"[BabyBearDeath] {deadMonster.name} died! Executing death effect...");
    //    }
    //}

    // ==========================================
    // 3. 아기곰 패시브 구현
    // ==========================================
    /// <summary>
    /// 조건이 맞을 때 자동으로 발동되는 패시브입니다. PassiveAbility를 상속받습니다.
    /// 에디터의 "Starting Passives" 섹션에 추가할 수 있습니다.
    /// </summary>
    [System.Serializable]
    public class BabyBearPassive : PassiveAbility
    {
        public BabyBearPassive()
        {
            passiveName = "아기 곰은 꿀을 좋아해";
            description = " 매 턴 시작 시, 무작위 타일 3개에 꿀 타일을 설치합니다.";

            // Priority(우선순위)가 높을수록 같은 타이밍에 겹쳤을 때 먼저 실행됩니다.
            priority = 10; 

            // stackable이 false면 중첩되지 않습니다 (동일 효과 불가).
            isStackable = false;
        }

        /// <summary>
        /// 전투 중에 발생하는 각종 이벤트(CombatTrigger) 신호를 감지하고 반응합니다.
        /// </summary>
        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 예외 1) 맥락이나 액션이 비어 있으면 무시
            if (context?.Action == null) return;

            // 예외 2) 내가 일으킨(ourceUnit == owner) 이벤트이면서,
            //         "턴 시작" 시점의 액션(ActionType.OnStartTurn)이 "실행되기 직전"(OnPreAction)일 때 감지합니다.
            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.OnStartTurn && 
                context.SourceUnit == owner)
            {
                Debug.Log($"[BabyBearPassive] 꿀 타일 설치");
                PlantHoneyTiles();
            }
        }

        private void PlantHoneyTiles()
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null) return;

            // 0~19 사이의 랜덤한 3개 정수를 뽑기
            var randomIndices = new List<int>();
            while (randomIndices.Count < 3)
            {
                int randomIndex = Random.Range(0, 20);
                if (!randomIndices.Contains(randomIndex))
                {
                    randomIndices.Add(randomIndex);
                }
            }

            // 해당 인덱스의 타일에 꿀(HoneyTileAttribute) 부여
            foreach (var index in randomIndices)
            {
                var tile = orbitManager.GetTile(index);
                if (tile != null)
                {
                    var honeyAttribute = new HoneyTileAttribute(
                        TileAttributeType.Honey,
                        1, // 꿀은 데미지가 없으므로 Value는 0
                        1
                    );

                    tile.AddAttribute(honeyAttribute);
                    Debug.Log($"Honey Tile Generated at random index: {index}");
                }
            }
        }

        public override bool AllowSamePassive(PassiveAbility incoming)
        {
            return false;
        }
    }
}
