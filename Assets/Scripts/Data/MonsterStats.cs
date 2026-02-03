using UnityEngine;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 몬스터 스탯 데이터
    /// </summary>
    [System.Serializable]
    public class MonsterStats : Stats
    {
        [Header("Basic Info")]
        public string MonsterName = "Slime";
        public int Level = 1;
        
        [Header("Visual")]
        public Sprite MonsterSprite;
        public Color SpriteColor = Color.red;
        
        
    }
}
