namespace DiceOrbit.Data
{
    /// <summary>
    /// 타일 효과 타입
    /// </summary>
    public enum TileEffectType
    {
        None,           // 효과 없음
        Heal,           // 회복
        Damage,         // 데미지 (함정)
        BuffAttack,     // 공격력 증가
        BuffDefense,    // 방어력 증가
        LevelUp,        // 레벨업
        Event           // 이벤트 발생
    }
    
    /// <summary>
    /// 타일 효과 데이터
    /// </summary>
    [System.Serializable]
    public class TileEffect
    {
        public TileEffectType Type;
        public int Value;
        public string Description;
        
        public TileEffect(TileEffectType type, int value, string desc = "")
        {
            Type = type;
            Value = value;
            Description = string.IsNullOrEmpty(desc) ? GenerateDescription() : desc;
        }
        
        private string GenerateDescription()
        {
            switch (Type)
            {
                case TileEffectType.Heal:
                    return $"Heal {Value} HP";
                case TileEffectType.Damage:
                    return $"Take {Value} damage";
                case TileEffectType.BuffAttack:
                    return $"+{Value} ATK";
                case TileEffectType.BuffDefense:
                    return $"+{Value} DEF";
                case TileEffectType.LevelUp:
                    return "Level Up!";
                default:
                    return Type.ToString();
            }
        }
    }
}
