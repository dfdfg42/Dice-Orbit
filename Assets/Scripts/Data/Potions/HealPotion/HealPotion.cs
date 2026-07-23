using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;

namespace DiceOrbit.Data.Potions
{
    [CreateAssetMenu(fileName = "NewHealPotion", menuName = "DiceOrbit/Potions/Heal Potion")]
    public class HealPotion : Potion
    {
        [Header("회복 설정")]
        public int healAmount = 10;

        private void Reset()
        {
            PotionName = "기본 회복 물약";
            Description = "선택한 아군의 체력을 10 회복시킵니다.";
            TargetType = PotionTargetType.Ally; // 아군만 타겟팅
            ShopPrice = 50;
        }

        public override bool Use(Unit target = null)
        {
            // 타겟이 아군(Character)인지 검증 후 회복
            if (target is Character character && character.IsAlive && character.Stats != null)
            {
                character.Stats.CurrentHP = Mathf.Min(character.Stats.MaxHP, character.Stats.CurrentHP + healAmount);
                return true; // 성공적으로 사용됨
            }
            
            return false; // 조건 불충족으로 사용 취소
        }
    }
}
