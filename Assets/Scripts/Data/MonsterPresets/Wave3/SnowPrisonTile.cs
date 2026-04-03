using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 타일에 부여되는 특수한 속성(함정, 버프, 기믹 등)을 구현하는 클래스입니다.
    /// 캐릭터가 타일 위에서 턴을 종료하면 빙결(Frozen) 상태이상을 부여합니다.
    /// </summary>
    public class SnowPrisonTileAttribute : TileAttribute
    {
        public SnowPrisonTileAttribute(TileAttributeType type, int value, int duration, bool isStackable = false) 
            : base(type, value, duration, isStackable)
        {
            
        }

        public override void OnTraverse(Character character)
        {
            // 통과할 때는 효과 없음. 필요시 로직 추가 가능.
        }

        public override void OnEndTurn(Character character)
        {
            Activate(character);
        }

        public void Activate(Character target)
        {
            if (target == null || !target.IsAlive) return;

            Debug.Log($"[SnowPrisonTileAttribute] {target.name} 유닛이 눈 감옥 타일에서 턴을 종료해 빙결되었습니다!");
            target.StatusEffects.AddEffect(new DiceOrbit.Systems.Effects.FrozenDebuff(0, Value));
        }

        public override string GetDescription()
        {
            return $"타일 위에서 턴 종료 시 1턴 동안 빙결 상태가 됩니다.";
        }
    }
}

namespace DiceOrbit.Systems.Effects
{
    /// <summary>
    /// 빙결 디버프 (행동 불가 등의 처리)
    /// </summary>
    public class FrozenDebuff : StatusEffect
    {
        // 빙결 중에는 행동을 건너뛰도록 전투 파이프라인(CombatPipeline)이나 유닛 턴 로직 내에서 
        // EffectType.Frozen 을 감지하여 처리하도록 구현할 수 있습니다.
        public FrozenDebuff(int value, int duration) : base(EffectType.Frozen, value, duration)
        {
            IsStackable = false;
        }

        public override void EffectApplied()
        {
            if (Owner.Stats is CharacterStats c)
            {
                c.BindDebuff++;
            }
            
            // 빙결이 적용되는 순간 특수한 로직이 필요하다면 구현
            Debug.Log($"[FrozenDebuff] {Owner.name} 빙결되었습니다! (지속: {Duration}턴)");
        }

        public override void EffectExpired()
        {
            if (Owner.Stats is CharacterStats c)
            {
                c.BindDebuff--;
            }
            // 빙결이 해제될 때의 로직
            Debug.Log($"[FrozenDebuff] {Owner.name} 빙결이 해제되었습니다.");
        }
    }
}
