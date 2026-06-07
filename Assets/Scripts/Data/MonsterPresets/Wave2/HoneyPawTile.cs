using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.MonsterPresets.Wave2;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 아기곰 [꿀 묻은 발]로 설치되는 꿀 타일.
    /// 캐릭터가 통과하면:
    ///  1) 그 캐릭터를 일정량 회복(미끼),
    ///  2) '먹은 꿀' 카운트 +1 (아기곰 피해 증가),
    ///  3) 한 턴에 꿀 타일을 bindThreshold개 이상 밟으면 이동 불가(빙결) 부여,
    ///  4) 자기 자신 제거.
    /// 몬스터가 죽어도 유지되며, 웨이브 종료 시 BearPackTracker가 정리한다.
    /// </summary>
    public class HoneyPawTile : TileAttribute
    {
        private readonly int healAmount;
        private readonly int bindThreshold;
        private readonly int bindDuration;

        public HoneyPawTile(int healAmount, int bindThreshold, int bindDuration)
            : base(TileAttributeType.Honey, healAmount, -1, false)
        {
            this.healAmount = healAmount;
            this.bindThreshold = bindThreshold;
            this.bindDuration = bindDuration;
        }

        public override void OnTraverse(Character character) => Activate(character);

        private void Activate(Character target)
        {
            if (target == null || !target.IsAlive) return;

            // 1) 통과한 캐릭터 회복 (미끼)
            if (healAmount > 0)
            {
                var heal = new CombatContext(null, target, new CombatAction("꿀", ActionType.Heal, healAmount));
                CombatPipeline.Instance?.Process(heal);
            }

            // 2) '먹은 꿀' 카운트 → 아기곰 피해 증가
            BearPackTracker.RegisterHoneyEaten();

            // 3) 한 턴에 꿀 타일을 bindThreshold개 이상 밟으면 이동 불가
            int turn = CombatManager.Instance != null ? CombatManager.Instance.TurnCount : 0;
            int stepped = BearPackTracker.RegisterHoneyStep(target, turn);
            if (stepped >= bindThreshold)
            {
                target.StatusEffects?.AddEffect(new FrozenDebuff(0, bindDuration));
            }

            // 4) 발동 후 삭제
            Owner?.RemoveAttribute(this);
        }

        public override string GetDescription()
            => $"통과 시 {healAmount} 회복. 한 턴에 {bindThreshold}개 이상 밟으면 이동 불가 (발동 후 삭제)";
    }
}
