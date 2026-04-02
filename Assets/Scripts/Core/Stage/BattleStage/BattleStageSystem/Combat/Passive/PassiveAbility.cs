using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Passives
{
    /// <summary>
    /// 패시브 타입
    /// </summary>
    [System.Serializable]
    public class PassiveType
    {
        public string Name;
        public bool IsStackable;
    }

    /// <summary>
    /// 모든 패시브의 기본 클래스
    /// [System.Serializable]로 정의되며, ICombatReactor를 구현합니다.
    /// SkillData와 동일한 패턴을 따릅니다.
    /// </summary>
    [System.Serializable]
    public abstract class PassiveAbility : ICombatReactor
    {
        [SerializeField] protected string passiveName = "";
        [SerializeField] [TextArea] protected string description = "패시브 설명";
        [SerializeField] protected int priority = 0;
        [SerializeField] protected Sprite icon;
        [SerializeField] protected bool isStackable = true;
        [SerializeField] [Min(1)] protected int currentLevel = 1;
        
        protected Unit owner;

        // 가상 프로퍼티 (각 패시브에서 override 가능)
        public virtual string PassiveName => passiveName;
        public virtual string Description => description;
        public virtual int Priority => priority;
        public virtual Sprite Icon => icon;
        public virtual bool IsStackable => isStackable;
        public int CurrentLevel => currentLevel;

        public void ConfigureMetadata(string name, string desc, Sprite iconSprite = null)
        {
            if (!string.IsNullOrWhiteSpace(name)) passiveName = name;
            if (!string.IsNullOrWhiteSpace(desc)) description = desc;
            if (iconSprite != null) icon = iconSprite;
        }

        /// <summary>
        /// 초기화 (필요 시)
        /// </summary>
        public virtual void Initialize(Unit Owner) 
        { 
            owner = Owner;
            ApplyLevel(currentLevel);
        }

        public void SetLevel(int level)
        {
            int normalized = Mathf.Max(1, level);
            if (currentLevel == normalized) return;
            currentLevel = normalized;
            // 레벨이 바뀌면 런타임 수치를 다시 계산합니다.
            ApplyLevel(currentLevel);
        }

        /// <summary>
        /// 패시브 레벨이 변경되었을 때 수치 재계산.
        /// </summary>
        protected virtual void ApplyLevel(int level)
        {
        }

        /// <summary>
        /// 패시브 복제 (공유 상태 오염 방지)
        /// </summary>
        public virtual PassiveAbility Clone()
        {
            return (PassiveAbility)this.MemberwiseClone();
        }

        /// <summary>
        /// 전투 파이프라인 반응 로직
        /// </summary>
        public abstract void OnReact(CombatTrigger trigger, CombatContext context);

        /// <summary>
        /// 같은 패시브가 중첩될 때 처리 로직
        /// </summary>
        public virtual bool AllowSamePassive(PassiveAbility incoming) 
        {
            return IsStackable;
        }
    }
}   
