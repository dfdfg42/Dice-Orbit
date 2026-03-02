using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Data.Skills.Effects
{
    public abstract class SkillEffectBase : ScriptableObject
    {
        [Header("VFX")]
        [SerializeField] protected DiceOrbit.Visuals.CombatVfxProfile vfxProfile;

        public DiceOrbit.Visuals.CombatVfxProfile VfxProfile => vfxProfile;

        /// <summary>
        /// (선택적) 타일 공격 등 특정 범위를 하이라이트/프리뷰 해야 할 때 오버라이드.
        /// 기본적으로는 아무 추가 타일도 반환하지 않음.
        /// </summary>
        public virtual List<TileData> GetTargetTilesPreview(Unit source)
        {
            return new List<TileData>();
        }

        /// <summary>
        /// 스킬의 실제 효과를 실행.
        /// </summary>
        /// <param name="source">스킬 시전자</param>
        /// <param name="targets">선택된 타겟 캐릭터들</param>
        /// <param name="targetTiles">선택된/영향받는 타일들</param>
        /// <param name="diceValue">굴린 주사위 눈금 (패시브 등 필요없는 경우 0)</param>
        public abstract void Execute(Unit source, List<Unit> targets, List<TileData> targetTiles, int diceValue);
    }
}
