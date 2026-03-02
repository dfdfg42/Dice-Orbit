using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;

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
    }
}
