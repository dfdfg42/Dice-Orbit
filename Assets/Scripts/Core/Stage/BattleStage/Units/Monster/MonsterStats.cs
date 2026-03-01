using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 몬스터 스탯 데이터
    /// </summary>
    [System.Serializable]
    public class MonsterStats : UnitStats
    {
        [Header("Basic Info")]
        public string MonsterName = "Slime";
        public int Level = 1;
        public int Speed = 1;

        [Header("Visual")]
        [HideInInspector] public Sprite MonsterSprite;
        [HideInInspector] public Sprite AttackSprite;
        [HideInInspector] public Sprite DamageSprite;
        [HideInInspector] public Color SpriteColor = Color.red;

        /// <summary>
        /// MonsterStats 깊은 복사
        /// </summary>
        public MonsterStats DeepCopy()
        {
            MonsterStats copy = new MonsterStats();
            CopyBaseTo(copy);

            copy.MonsterName = this.MonsterName;
            copy.Level = this.Level;
            copy.MonsterSprite = this.MonsterSprite;
            copy.AttackSprite = this.AttackSprite;
            copy.DamageSprite = this.DamageSprite;
            copy.SpriteColor = this.SpriteColor;
            copy.Speed = this.Speed;
            return copy;
        }
    }
}
