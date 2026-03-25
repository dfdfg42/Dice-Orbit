using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 타일에 부여되는 특수한 속성(함정, 버프, 기믹 등)을 구현하는 뼈대 클래스입니다.
    /// TileAttribute를 상속받으며, 캐릭터가 타일을 밟거나 지나갈 때의 이벤트를 처리합니다.
    /// </summary>
    public class HoneyTileAttribute : TileAttribute
    {
        /// <summary>
        /// 속성 생성자. (보통 패시브나 스킬을 통해 타일에 부착될 때 호출됩니다)
        /// </summary>
        /// <param name="type">속성의 타입(enum)</param>
        /// <param name="value">데미지나 방어도 등의 수치 (기본값 설정 가능)</param>
        /// <param name="duration">기믹이 지속되는 턴 수 (-1이면 무한)</param>
        /// <param name="isStackable">같은 타일에 여러 개 중복 설치 가능한지</param>
        public HoneyTileAttribute(TileAttributeType type, int value, int duration, bool isStackable = false) 
            : base(type, value, duration, isStackable)
        {
        }

        /// <summary>
        /// 캐릭터가 이 타일을 지나갈 때 발동합니다. (도착 시점 포함)
        /// </summary>
        /// <param name="character">이 타일을 밟은 캐릭터 객체</param>
        public override void OnTraverse(Character character)
        {
            // 예시: 밟는 순간 효과 발동을 위해 Activate 호출
            Activate(character);
        }

        /// <summary>
        /// 캐릭터가 이동을 마치고 이 타일 위에서 턴을 종료할 때 발동합니다.
        /// </summary>
        public override void OnEndTurn(Character character)
        {
            // 예시: 턴을 마쳤을 때도 한 번 더 발동할 경우 사용
            Activate(character);
        }

        /// <summary>
        /// 실제 효과를 처리하는 커스텀 로직 (피해 주기, 상태 부여 등)
        /// </summary>
        public void Activate(Character target)
        {
            // 타겟이 죽은 상태이거나 없으면 무시
            if (target == null || !target.IsAlive) return;

            //TODO: 이동력 감소 등 디버프 로직 구현
            Debug.Log($"[HoneyTileAttribute] {target.name} 유닛이 꿀 타일을 밟아 끈적해졌습니다!");
            target.StatusEffects.AddEffect(new DiceOrbit.Systems.Effects.HoneyDebuff(1,1));
            Owner.RemoveAttribute(this);
        }

        /// <summary>
        /// UI나 툴팁에서 타일의 설명을 보여줄 때 호출되는 문자열입니다.
        /// </summary>
        public override string GetDescription()
        {
            string durationText = Duration < 0 ? "영구" : $"{Duration}턴";
            return $"통과시 이동량이 {Value} 감소합니다";
        }
    }
}

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 공격력 버프 (데미지 계산 시 추가)
    /// </summary>
    public class HoneyDebuff : StatusEffect
    {
        public HoneyDebuff(int value, int duration) : base(EffectType.Honey, value, duration)
        {
            IsStackable = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            base.OnReact(trigger, context);

            if (Owner == null) return;

            // OnCalculateOutput: 데미지 계산 시점에 개입
            if (trigger == CombatTrigger.OnCalculateOutput)
            {
                // 소유자가 공격자이며, 공격 액션일 때
                if (context.SourceUnit == Owner && context.Action.Type == ActionType.Attack)
                {
                    context.OutputValue += Value;
                    // Debug.Log($"[BuffAttack] Added {Value} damage to {context.Action.Name}");
                }
            }
        }


        public override void EffectApplied()
        {
            if (Owner.Stats is CharacterStats c)
            {
                c.MoveDebuff += Value;
            }
        }

        public override void EffectExpired()
        {
            if (Owner.Stats is CharacterStats c)
            {
                c.MoveDebuff -= Value;
            }
        }
    }
}