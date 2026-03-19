using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.SamplePreset
{
    // ==========================================
    // 1. 샘플 스킬 구현
    // ==========================================
    /// <summary>
    /// 몬스터가 사용할 스킬입니다. SkillData를 상속받습니다.
    /// 에디터의 "AI Pattern" 섹션에서 설정할 수 있습니다.
    /// </summary>
    [System.Serializable]
    public class SampleAttack : SkillData
    {
        [Header("Skill Settings")]
        [Tooltip("스킬 사용 시 입힐 피해량")]
        [SerializeField] private int damage = 10;

        /// <summary>
        /// 생성자에서는 스킬의 이름과 설명을 초기화해야 합니다.
        /// 이를 생략하면 게임 내 툴팁 등에서 내용이 비어보입니다.
        /// </summary>
        public SampleAttack()
        {
            skillName = "샘플 공격";
            description = "대상에게 데미지를 입히는 샘플 스킬입니다.";
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
            foreach (var target in targetUnits)
            {
                if (target == null || !target.IsAlive) continue;

                // 1. 공격 정보를 담을 CombatAction 생성
                // (이름, 타입, 매개변수(damage 등))
                var action = new CombatAction(SkillName, ActionType.Attack, damage);
                
                // 2. 파이프라인에 전달할 CombatContext 생성
                var context = new CombatContext(source, target, action);
                
                // 3. 전투 파이프라인(CombatPipeline)을 통해 처리를 요쳥 (패시브 등이 중간에 개입할 수 있음)
                CombatPipeline.Instance?.Process(context);

                Debug.Log($"[{SkillName}] {source.name} attacks {target.name} for {damage} damage");
            }
        }
    }

    // ==========================================
    // 2. 샘플 사망 효과 구현
    // ==========================================
    /// <summary>
    /// 몬스터가 사망했을 때 발생하는 효과입니다. DeathEffect를 상속받습니다.
    /// </summary>
    [System.Serializable]
    public class SampleDeath : DeathEffect
    {
        public SampleDeath()
        {
            effectName = "Sample Death";
            description = "몬스터가 죽을 때 발동하는 샘플 효과입니다.";
        }

        /// <summary>
        /// 사망 효과가 발동되는 로직입니다.
        /// </summary>
        /// <param name="deadMonster">죽은 몬스터 유닛 객체</param>
        public override void Execute(Monster deadMonster)
        {
            // 예시: 몬스터가 죽을 때 맵에 설치한 특정 타일 효과를 모두 지운다거나 아군에게 버프를 줄 수 있습니다.
            Debug.Log($"[SampleDeath] {deadMonster.name} died! Executing death effect...");
        }
    }

    // ==========================================
    // 3. 샘플 패시브 구현
    // ==========================================
    /// <summary>
    /// 조건이 맞을 때 자동으로 발동되는 패시브입니다. PassiveAbility를 상속받습니다.
    /// 에디터의 "Starting Passives" 섹션에 추가할 수 있습니다.
    /// </summary>
    [System.Serializable]
    public class SamplePassive : PassiveAbility
    {
        [Header("Passive Settings")]
        [Tooltip("매 턴 회복할 체력")]
        [SerializeField] private int healAmount = 5;

        public SamplePassive()
        {
            passiveName = "재생";
            description = "매 턴 시작 시 체력을 회복합니다.";
            
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
                Debug.Log($"[SamplePassive] 턴 시작 트리거 발동 - 힐 적용");
                owner.Heal(healAmount);
            }
        }

        public override bool AllowSamePassive(PassiveAbility incoming)
        {
            return false;
        }
    }
}
