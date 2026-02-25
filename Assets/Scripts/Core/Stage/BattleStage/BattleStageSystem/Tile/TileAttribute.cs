using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Tile
{
    public enum TileAttributeType
    {
        None,
        LevelUp,
        RandMine,
        Bone,
    }

    /// <summary>
    /// 타일에 적용되는 속성 인스턴스
    /// </summary>
    public class TileAttribute : ICombatReactor
    {
        public TileAttributeType Type;
        public int Value;
        public int Duration;
        public bool IsStackable;

        public int Priority => 5;

        public TileAttribute(TileAttributeType type, int value, int duration, bool isStackable = false)
        {
            Type = type;
            Value = value;
            Duration = duration;
            IsStackable = isStackable;
        }

        public void AddStack(int value)
        {
            Value += value;
        }

        public void RefreshDuration(int duration)
        {
            if (Duration == -1 || duration == -1)
            {
                Duration = -1;
            }
            else
            {
                Duration = Mathf.Max(Duration, duration);
            }
        }

        // ICombatReactor Implementation
        public virtual void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (Owner == null) return;

            // 공통 로직: 턴 종료 시 지속시간 감소
            if (context.Action.Type==ActionType.OnEndTurn && context.IsTiling == true)
            {
                if (Duration > 0)
                {
                    Duration--;
                }
            }
        }

        public virtual void OnArrive(Core.Character character)
        {
            
        }

        public virtual void OnTraverse(Core.Character character)
        {

        }

        public virtual void OnEndTurn(Core.Character character)
        {

        }

        // Owner를 주입받아야 함
        public TileData Owner { get; set; }

        public void SetOwner(TileData owner)
        {
            Owner = owner;
        }
    }
}