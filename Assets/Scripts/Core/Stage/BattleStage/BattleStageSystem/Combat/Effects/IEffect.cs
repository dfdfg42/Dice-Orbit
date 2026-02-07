namespace DiceOrbit.Systems
{
    /// <summary>
    /// 모든 효과의 기반 인터페이스
    /// </summary>
    public interface IEffect
    {
        string EffectName { get; }
        string Description { get; }
        
        /// <summary>
        /// 캐릭터에게 효과 적용
        /// </summary>
        void Apply(Core.Character target, int value);
        
        /// <summary>
        /// 몬스터에게 효과 적용
        /// </summary>
        void Apply(Core.Monster target, int value);
    }
}
