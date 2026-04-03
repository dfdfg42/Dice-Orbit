using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using static Unity.VisualScripting.Member;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 스킬 타겟 타입
    /// </summary>
    public enum SkillTargetType
    {
        SingleEnemy,
        AllEnemies,
        Self,
        Ally,
        AllAllies,
        Tiles
    }

    /// <summary>
    /// 스킬 데이터 (다형성 지원 - [SerializeReference]로 사용)
    /// </summary>
    [System.Serializable]
    public abstract class SkillData
    {
        [SerializeField] protected string skillName = "";
        [SerializeField] protected string description = "";

        public virtual string SkillName => skillName;
        public virtual string Description => description;

        /// <summary>
        /// TargetSelectionStrategy가 Custom일 때 호출되어 타겟 유닛들을 가져옵니다.
        /// 파생 클래스에서 오버라이드하여 독자적인 타겟팅을 구현하세요.
        /// </summary>
        public virtual List<Core.Unit> GetCustomTargets(MonsterSkill skill, Core.Monster owner)
        {
            return new List<Core.Unit>();
        }

        /// <summary>
        /// TargetSelectionStrategy가 Custom일 때 호출되어 타겟 타일들을 가져옵니다.
        /// 파생 클래스에서 오버라이드하여 독자적인 타겟팅을 구현하세요.
        /// </summary>
        public virtual List<TileData> GetCustomTiles(MonsterSkill skill, Core.Monster owner)
        {
            return new List<TileData>();
        }

        public void ExecuteSkillWithIntent(Core.Unit source, AttackIntent intent)
        {
            if (source == null || intent == null)
            {
                Debug.LogWarning("[SkillData] Execute called with null source or intent");
                return;
            }

            // Intent에서 타겟 정보 추출
            var targetUnits = new List<Core.Unit>();
            if (intent.Targets != null)
            {
                foreach (var character in intent.Targets)
                {
                    if (character != null && character.IsAlive)
                        targetUnits.Add(character);
                }
            }

            var targetTiles = intent.TargetTiles ?? new List<TileData>();
            Execute(source, targetUnits, targetTiles, 0);
        }

        /// <summary>
        /// 스킬 실행
        /// </summary>
        public virtual void Execute(Core.Unit source, List<Core.Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            
        }

        public virtual void AttackUnits(Unit source, List<Core.Unit> targetUnits, int damage)
        {
            if (source == null || !source.IsAlive) return;
            foreach (var target in targetUnits)
            {
                if (target == null || !target.IsAlive) continue;

                var context = new CombatContext(
                    source,
                    target,
                    new CombatAction(SkillName, ActionType.Attack, damage)
                );
                CombatPipeline.Instance?.Process(context);

                Debug.Log($"[{SkillName}] {source.name} attacks {target.name} for {damage} damage");
            }
        }

        public virtual void AttackTiles(Unit source, List<TileData> targetTiles, int damage)
        {
            if (source == null || !source.IsAlive) return;
            List<Core.Unit> targets = new();
            foreach (var tile in targetTiles)
            {
                if (tile == null) continue;
                tile.GetCharactersOnTile()?.ForEach(c =>
                {
                    if (c != null && c.IsAlive)
                        targets.Add(c);
                });
            }
            // 타겟에 있는 유닛들 중복 제거
            targets = new List<Core.Unit>(new HashSet<Core.Unit>(targets));
            foreach (var character in targets)
            {
                if (character == null || !character.IsAlive) continue;
                var context = new CombatContext(
                    source,
                    character,
                    new CombatAction(SkillName, ActionType.Attack, damage)
                );
                CombatPipeline.Instance?.Process(context);
                Debug.Log($"[{SkillName}] {source.name} attacks {character.name} on tile for {damage} damage");
            }
        }
    }
}
