using UnityEngine;

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
        
        [Header("Visual")]
        public Sprite MonsterSprite;
        public Color SpriteColor = Color.red;

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
            copy.SpriteColor = this.SpriteColor;

            return copy;
        }
    }
}
