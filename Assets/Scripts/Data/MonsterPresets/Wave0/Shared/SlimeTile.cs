using DiceOrbit.Core;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// [점액]으로 설치되는 점액 타일. 캐릭터가 통과하거나 그 위에서 턴을 종료하면
    /// 그 캐릭터에 쇠약(가하는 피해 -weakenPercent%, weakenDuration턴)을 부여하고 자기 자신을 제거한다.
    /// 타일당 1개, 영구(몬스터 사망 후 유지), 웨이브 종료 시 SlimeSet이 정리.
    /// </summary>
    public class SlimeTile : TileAttribute
    {
        private readonly int weakenPercent;
        private readonly int weakenDuration;

        public SlimeTile(int weakenPercent, int weakenDuration)
            : base(TileAttributeType.Slime, weakenPercent, -1, false)
        {
            this.weakenPercent = weakenPercent;
            this.weakenDuration = weakenDuration;
        }

        public override void OnTraverse(Core.Character character) => Activate(character);
        public override void OnEndTurn(Core.Character character) => Activate(character);

        private void Activate(Core.Character target)
        {
            if (target == null || !target.IsAlive || target.StatusEffects == null) return;
            target.StatusEffects.AddEffect(new WeakStatus(weakenPercent, weakenDuration));
            Owner?.RemoveAttribute(this);
        }

        public override string GetDescription()
            => $"통과·턴 종료 시 쇠약(가하는 피해 -{weakenPercent}%, {weakenDuration}턴) 부여 (발동 후 삭제)";
    }
}
