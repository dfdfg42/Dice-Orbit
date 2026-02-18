using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 타겟 타입
    /// </summary>
    public enum TargetType
    {
        Characters,     // 단일 타겟
        Tiles,       // 특정 범위 (주변 N칸)
    }

    /// <summary>
    /// 타겟 선정 전략
    /// </summary>
    public enum TargetSelectionStrategy
    {
        Random,           // 무작위 선택
        AllTargets        // 모든 타겟
    }

    /// <summary>
    /// 공격 의도 타입
    /// </summary>
    public enum IntentType
    {
        Attack,      // 공격
        Defend,      // 방어 버프
        Buff,        // 공격력 버프
        Special,     // 특수 행동
        Multi        // 다중 공격 (Deprecated: Use Attack with TargetType.Area/All)
    }

    [CreateAssetMenu(fileName = "MonsterSkill", menuName = "Monster/MonsterSkill")]
    public class MonsterSkill : ScriptableObject
    {
        [Header("Skill Data")]
        public SkillData skillData;

        [Header("Targeting")]
        [SerializeField] private TargetSelectionStrategy targetStrategy = TargetSelectionStrategy.Random;
        [SerializeField] TargetType TargetType = TargetType.Characters;

        // 런타임에 생성된 AttackIntent (캐싱용)
        private AttackIntent cachedIntent;

        /// <summary>
        /// 스킬 대상 선정 및 AttackIntent 생성
        /// </summary>
        public AttackIntent GenerateIntent(Monster owner)
        {
            if (skillData == null)
            {
                Debug.LogWarning("[MonsterSkill] SkillData is null!");
                return null;
            }

            // PartyManager에서 생존한 캐릭터 가져오기
            var aliveCharacters = PartyManager.Instance?.GetAliveCharacters();
            if (aliveCharacters == null || aliveCharacters.Count == 0)
            {
                Debug.LogWarning("[MonsterSkill] No alive characters found!");
                return null;
            }

            // 타겟 선정
            List<Character> selectedTargets = new();

            // TileData 선정 (필요한 경우)
            List<TileData> targetTiles = new();

            switch (TargetType)
            {
                case TargetType.Characters:
                    selectedTargets = SelectTargets(aliveCharacters, owner);
                    break;
                case TargetType.Tiles:
                    if (targetTiles == null || targetTiles.Count() == 0)
                    {
                        targetTiles = SelectTiles();
                    }
                    break;
                default:
                    Debug.LogWarning("[MonsterSkill] Invalid TargetType specified!");
                    return null;
            }

            // IntentType 결정
            IntentType intentType = DetermineIntentType();

            // Damage 계산
            int damage = skillData.CalculateDamage(owner.Stats.Attack, 0);

            // AttackIntent 생성
            var intent = new AttackIntent(
                intentType, 
                TargetType,
                damage, 
                selectedTargets, 
                skillData.Description
            );
            intent.TargetTiles = targetTiles;

            cachedIntent = intent;
            return intent;
        }

        /// <summary>
        /// 타겟 선정 로직
        /// </summary>
        private List<Character> SelectTargets(List<Character> candidates, Monster owner)
        {
            if (candidates == null || candidates.Count == 0)
                return new List<Character>();

            // SkillData.TargetType에 따라 분기
            switch (skillData.TargetType)
            {
                case SkillTargetType.SingleEnemy:
                    return SelectSingleTarget(candidates);
                case SkillTargetType.AllEnemies:
                    return candidates; // 모든 적
                default:
                    return new List<Character>();
            }
        }

        /// <summary>
        /// 단일 타겟 선정 (전략 기반)
        /// </summary>
        private List<Character> SelectSingleTarget(List<Character> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return new List<Character>();

            Character selected = null;

            switch (targetStrategy)
            {
                case TargetSelectionStrategy.Random:
                case TargetSelectionStrategy.AllTargets:
                default:
                    selected = candidates[Random.Range(0, candidates.Count)];
                    break;
            }

            return new List<Character> { selected };
        }

        /// <summary>
        /// 타일 기반 타겟 선정 (MonsterTileActionModule 처리)
        /// </summary>
        private List<TileData> SelectTiles()
        {
            return new(); // 현재는 타일 선택 로직이 없으므로 빈 배열 반환
        }

        /// <summary>
        /// SkillData에서 IntentType 추론
        /// </summary>
        private IntentType DetermineIntentType()
        {
            //if (skillData.Type == SkillType.Passive)
            //    return IntentType.Defend;

            //if (skillData.TargetType == SkillTargetType.Self || 
            //    skillData.TargetType == SkillTargetType.Ally)
            //    return IntentType.Buff;

            //if (skillData.TargetType == SkillTargetType.AllEnemies)
            //    return IntentType.Multi;

            return IntentType.Attack;
        }
    }
}