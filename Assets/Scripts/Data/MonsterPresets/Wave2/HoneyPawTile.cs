using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.MonsterPresets.Wave2;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 아기곰 [꿀 묻은 발]로 설치되는 꿀 타일.
    /// 캐릭터가 통과하거나 그 위에서 턴을 종료하면:
    ///  1) 그 캐릭터를 일정량 회복(미끼),
    ///  2) 꿀 디버프 부여(이미 있으면 지속시간 초기화),
    ///  3) '먹은 꿀' 카운트 +1 (아기곰 피해 증가),
    ///  4) 자기 자신 제거.
    /// 몬스터가 죽어도 유지되며, 웨이브 종료 시 BearPackTracker가 정리한다.
    /// </summary>
    public class HoneyPawTile : TileAttribute
    {
        private readonly int healAmount;
        private readonly int debuffDuration;

        public HoneyPawTile(int healAmount, int debuffDuration)
            : base(TileAttributeType.Honey, healAmount, -1, false)
        {
            this.healAmount = healAmount;
            this.debuffDuration = debuffDuration;
        }

        public override void OnTraverse(Character character) => Activate(character);

        public override void OnEndTurn(Character character) => Activate(character);

        private void Activate(Character target)
        {
            if (target == null || !target.IsAlive) return;

            // 1) 통과한 캐릭터 회복 (미끼)
            if (healAmount > 0)
            {
                var heal = new CombatContext(null, target, new CombatAction("꿀", ActionType.Heal, healAmount));
                CombatPipeline.Instance?.Process(heal);
            }

            // 2) 꿀 디버프 부여 (이미 있으면 AddEffect가 지속시간을 갱신)
            target.StatusEffects?.AddEffect(new HoneyDebuff(1, debuffDuration));

            // 3) '먹은 꿀' 카운트 → 아기곰 피해 증가
            BearPackTracker.RegisterHoneyEaten();

            // 4) 발동 후 삭제
            Owner?.RemoveAttribute(this);
        }

        public override string GetDescription()
            => $"통과 시 {healAmount} 회복하지만 꿀 디버프가 부여됩니다 (발동 후 삭제)";
    }
}
