using UnityEngine;
using DiceOrbit.Data.MonsterAI;

namespace DiceOrbit.Data.Monsters
{
    [CreateAssetMenu(fileName = "MonsterPreset", menuName = "Dice Orbit/Monsters/Monster Preset")]
    public class MonsterPreset : ScriptableObject
    {
        [Header("Basic Info")]
        public string MonsterName = "Monster";
        public Sprite MonsterSprite;
        public Color SpriteColor = Color.red;

        [Header("Combat Stats")]
        public int MaxHP = 50;
        public int Attack = 8;
        public int Defense = 2;

        [Header("AI Pattern")]
        public MonsterPattern AIPattern;

        public MonsterStats CreateStats()
        {
            return new MonsterStats
            {
                MonsterName = MonsterName,
                Level = 1,
                MaxHP = MaxHP,
                CurrentHP = MaxHP,
                Attack = Attack,
                Defense = Defense,
                MonsterSprite = MonsterSprite,
                SpriteColor = SpriteColor
            };
        }
    }
}
