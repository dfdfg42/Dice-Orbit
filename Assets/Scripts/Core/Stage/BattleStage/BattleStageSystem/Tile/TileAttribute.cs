using UnityEngine;
using DiceOrbit.Core.Pipeline;
using System.Collections.Generic;

namespace DiceOrbit.Data.Tile
{
    public enum TileAttributeType
    {
        None,
        LevelUp,
        RandMine,
        Bone,
        Honey,
        Bind,           // 속박: 그 위 턴 종료 시 이동 불가 (구 SnowPrison)
        Cloud,          // 구름 타일 (기상학자 패시브)
        ScoutHeal,      // 정찰병 치유 타일
        Reagent,        // 시약 타일 (연금술사 패시브)
        Sharp,          // 예리함: 이 타일에서 공격 시 피해 +V% (이벤트)
        Dull,           // 약화: 이 타일에서 공격 시 피해 -V% (이벤트)
        Sturdy,         // 단단함: 통과/턴 종료 시 방어도 +V (이벤트)
        Harmony,        // 조화: 턴 종료 시 최대체력 V% 회복 (이벤트)
        Disharmony,     // 부조화: 턴 종료 시 최대체력 V% 피해 (이벤트)
        Flame,          // 불꽃: 턴 종료 시 V 피해 / 통과 시 소화 (Wave5)
        Slime,          // 점액: 통과/턴 종료 시 쇠약 부여 (Wave0)
        Amethyst,       // 자수정: 통과/턴 종료 시 수정 핵에 수정 중첩 +1, 영구 (Wave3 수정)
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

        /// <summary>라운드 종료 시 지속시간 1 감소 (-1은 영구라 감소하지 않음). CombatManager.TileTurnEnd에서 호출.</summary>
        public void TickDuration()
        {
            if (Duration > 0) Duration--;
        }

        // ICombatReactor Implementation
        public virtual void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (Owner == null) return;
            // 지속시간 감소는 TickDuration()(직접 틱)에서 처리한다.
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

        public virtual string GetDisplayName()
        {
            return Type switch
            {
                TileAttributeType.LevelUp => "레벨업 타일",
                TileAttributeType.RandMine => "지뢰",
                TileAttributeType.Bone => "뼈 방패",
                TileAttributeType.Honey => "꿀",
                TileAttributeType.Cloud => "구름 타일",
                TileAttributeType.Reagent => "시약 타일",
                TileAttributeType.Sharp => "예리함",
                TileAttributeType.Dull => "약화",
                TileAttributeType.Sturdy => "단단함",
                TileAttributeType.Harmony => "조화",
                TileAttributeType.Disharmony => "부조화",
                TileAttributeType.Flame => "불꽃",
                TileAttributeType.Slime => "점액",
                TileAttributeType.Amethyst => "자수정",
                TileAttributeType.Bind => "속박",
                _ => Type.ToString()
            };
        }

        public virtual string GetDescription()
        {
            string durationText = Duration < 0 ? "영구" : $"{Duration}턴";
            return $"값: {Value}, 지속: {durationText}";
        }

        public virtual IEnumerable<string> GetTooltipDescriptions()
        {
            yield return $"{GetDisplayName()}: {GetDescription()}";
        }
    }
}
