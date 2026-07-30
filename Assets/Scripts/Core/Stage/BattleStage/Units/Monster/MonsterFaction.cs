namespace DiceOrbit.Data.Monsters
{
    /// <summary>
    /// 몬스터 세트(진영) 식별자. 세트 지원 로직(가호/흑점/만월 등)이
    /// 같은 진영끼리 대상을 고를 때 사용한다. (Wave5 불꽃도 재사용)
    /// </summary>
    public enum MonsterFaction
    {
        None,
        Sun,
        Moon,
        Flame
    }
}
