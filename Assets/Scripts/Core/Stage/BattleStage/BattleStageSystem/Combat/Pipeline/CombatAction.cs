namespace DiceOrbit.Core.Pipeline
{
    public enum ActionType
    {
        Attack,
        Heal,
        Utility, // Buff, Debuff only, Move, etc.
        None,
        Skill,
        Move,
        OnArrive,
        OnTreaverse,
        OnStartTurn,
        OnEndTurn,
    }

    /// <summary>
    /// 효과 정보 구조체 (버프/디버프 등)
    /// </summary>
    public struct ActionEffectInfo
    {
        public DiceOrbit.Data.EffectType Type;
        public int Value;
        public int Duration;

        public ActionEffectInfo(DiceOrbit.Data.EffectType type, int value, int duration)
        {
            Type = type;
            Value = value;
            Duration = duration;
        }
    }
}
