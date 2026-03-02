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
    /// 기본 스킬 효과 타입 (Inspector에서 설정 가능)
    /// </summary>
    public enum SkillEffectType
    {
        Damage,         // 데미지
        Heal,           // 힐
        GetArmor,       // 방어막 획득
        Buff,           // 버프 (공격력 증가 등)
        Debuff          // 디버프 (공격력 감소 등)
    }

    /// <summary>
    /// 기본 스킬 효과 (Inspector에서 설정 가능)
    /// </summary>
    [System.Serializable]
    public class SkillEffect
    {
        public SkillEffectType Type;
        public int Amount;
    }

    /// <summary>
    /// 스킬 데이터
    /// </summary>
    [System.Serializable]
    public class SkillData
    {
        public virtual string SkillName  { get; set; }
        public virtual string description { get; set; }
        [HideInInspector] public string Description => description;

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
        /// 스킬 실행 (BasicEffects + SpecialEffects)
        /// </summary>
        public virtual void Execute(Core.Unit source, List<Core.Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {

        }

        /// <summary>
        /// 데미지 적용
        /// </summary>
        private void ApplyDamage(Core.Unit source, List<Core.Unit> targets, int amount)
        {
            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                var context = new Core.Pipeline.CombatContext(
                    source,
                    target,
                    new Core.Pipeline.CombatAction(SkillName, Core.Pipeline.ActionType.Attack, amount)
                );
                Core.Pipeline.CombatPipeline.Instance?.Process(context);
            }
        }

        /// <summary>
        /// 힐 적용
        /// </summary>
        private void ApplyHeal(Core.Unit source, int amount)
        {
            if (source == null) return;
            source.Heal(amount);
            Debug.Log($"[SkillData] {source.name} healed for {amount}");
        }

        /// <summary>
        /// 방어막 적용
        /// </summary>
        private void ApplyArmor(Core.Unit source, int amount)
        {
            if (source == null) return;
            source.Stats.TempArmor += amount;
            Debug.Log($"[SkillData] {source.name} gained {amount} armor");
        }

        /// <summary>
        /// 버프 적용 (공격력 증가)
        /// </summary>
        private void ApplyBuff(Core.Unit source, int amount)
        {
            if (source == null) return;
            source.Stats.Attack += amount;
            Debug.Log($"[SkillData] {source.name} gained {amount} attack");
        }

        /// <summary>
        /// 디버프 적용 (공격력 감소)
        /// </summary>
        private void ApplyDebuff(List<Core.Unit> targets, int amount)
        {
            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.Stats.Attack = Mathf.Max(0, target.Stats.Attack - amount);
                Debug.Log($"[SkillData] {target.name} lost {amount} attack");
            }
        }

    }
}
