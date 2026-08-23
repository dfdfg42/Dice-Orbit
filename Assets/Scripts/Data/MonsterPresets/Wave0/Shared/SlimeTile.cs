using DiceOrbit.Core;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// [점액]으로 설치되는 점액 타일. 캐릭터가 통과하거나 그 위에서 턴을 종료하면
    /// 그 캐릭터에 둔화(다음 턴 -slowAmount 이동, slowDuration턴)를 부여하고 자기 자신을 제거한다.
    /// 타일당 1개, 영구(몬스터 사망 후 유지), 웨이브 종료 시 SlimeSet이 정리.
    /// </summary>
    public class SlimeTile : TileAttribute
    {
        private readonly int slowAmount;
        private readonly int slowDuration;

        public SlimeTile(int slowAmount, int slowDuration)
            : base(TileAttributeType.Slime, slowAmount, -1, false)
        {
            this.slowAmount = slowAmount;
            this.slowDuration = slowDuration;
        }

        public override void OnTraverse(Core.Character character) => Activate(character);
        public override void OnEndTurn(Core.Character character) => Activate(character);

        private void Activate(Core.Character target)
        {
            if (target == null || !target.IsAlive || target.StatusEffects == null) return;
            target.StatusEffects.AddEffect(new SlowStatus(slowAmount, slowDuration));
            Owner?.RemoveAttribute(this);
        }

        public override string GetDescription()
            => $"지나가거나 이 타일에서 턴을 마치면, {slowDuration}턴 동안 이동할 수 있는 칸 수가 {slowAmount} 감소합니다. 발동 후 사라집니다.";
    }
}
