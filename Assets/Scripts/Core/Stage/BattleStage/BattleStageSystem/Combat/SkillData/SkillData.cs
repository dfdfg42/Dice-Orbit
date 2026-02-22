using UnityEngine;
using System.Collections.Generic;

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
    /// 특수 스킬 효과 기본 클래스 (소환, 분열, 자폭 등)
    /// ScriptableObject로 만들어 드래그 앤 드롭 가능
    /// </summary>
    public abstract class SkillEffectBase : ScriptableObject
    {
        public abstract void Execute(Core.Unit source, List<Core.Unit> targetUnits, List<TileData> targetTiles);
    }
    
    /// <summary>
    /// 스킬 데이터
    /// </summary>
    [System.Serializable]
    public class SkillData
    {
        [Header("Basic Info")]
        public string SkillName = "Basic Attack";
        [TextArea]
        [Tooltip("플레이스홀더 사용 가능: {damage}, {heal}, {armor}, {buff}, {debuff}")]
        public string description = "스킬 설명";
        [HideInInspector] public string Description=>GetFormattedDescription();
        [HideInInspector] public SkillTargetType TargetType = SkillTargetType.SingleEnemy;

        [Header("Basic Effects - Inspector Editable")]
        public List<SkillEffect> BasicEffects = new List<SkillEffect>();

        [Header("Special Effects - Optional (Drag & Drop)")]
        public List<SkillEffectBase> SpecialEffects = new List<SkillEffectBase>();

        [Header("Legacy Effects - For Compatibility")]
        public List<EffectData> Effects = new List<EffectData>();

        [Header("Attribute")]
        public List<Tile.TileAttribute> TileAttributes = new List<Tile.TileAttribute>();

        [Header("Legacy Damage - For Compatibility")]
        [HideInInspector] public int DamageMultiplier = 1;
        [HideInInspector] public int BonusDamage = 0;
        [HideInInspector] public bool IgnoreDefense = false;
        
        /// <summary>
        /// 데미지 계산 (Legacy - 호환성 유지)
        /// </summary>
        public int CalculateDamage(int baseAttack, int diceValue)
        {
            return baseAttack * DamageMultiplier + diceValue + BonusDamage;
        }

        /// <summary>
        /// 포맷된 설명 반환 (플레이스홀더를 실제 값으로 치환)
        /// </summary>
        public string GetFormattedDescription()
        {
            string result = description;

            if (string.IsNullOrEmpty(result))
                return "";

            // BasicEffects에서 각 타입별 Amount 추출
            foreach (var effect in BasicEffects)
            {
                switch (effect.Type)
                {
                    case SkillEffectType.Damage:
                        result = result.Replace("{damage}", effect.Amount.ToString());
                        break;
                    case SkillEffectType.Heal:
                        result = result.Replace("{heal}", effect.Amount.ToString());
                        break;
                    case SkillEffectType.GetArmor:
                        result = result.Replace("{armor}", effect.Amount.ToString());
                        break;
                    case SkillEffectType.Buff:
                        result = result.Replace("{buff}", effect.Amount.ToString());
                        break;
                    case SkillEffectType.Debuff:
                        result = result.Replace("{debuff}", effect.Amount.ToString());
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// 스킬 실행 (BasicEffects + SpecialEffects)
        /// </summary>
        public void Execute(Core.Unit source, AttackIntent intent)
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

            // 1. BasicEffects 처리
            foreach (var effect in BasicEffects)
            {
                ApplyBasicEffect(effect, source, targetUnits);
            }

            // 2. SpecialEffects 처리
            foreach (var effect in SpecialEffects)
            {
                if (effect != null)
                    effect.Execute(source, targetUnits, targetTiles);
            }
        }

        /// <summary>
        /// 기본 효과 적용
        /// </summary>
        private void ApplyBasicEffect(SkillEffect effect, Core.Unit source, List<Core.Unit> targets)
        {
            switch (effect.Type)
            {
                case SkillEffectType.Damage:
                    ApplyDamage(source, targets, effect.Amount);
                    break;

                case SkillEffectType.Heal:
                    ApplyHeal(source, effect.Amount);
                    break;

                case SkillEffectType.GetArmor:
                    ApplyArmor(source, effect.Amount);
                    break;

                case SkillEffectType.Buff:
                    ApplyBuff(source, effect.Amount);
                    break;

                case SkillEffectType.Debuff:
                    ApplyDebuff(targets, effect.Amount);
                    break;
            }
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
