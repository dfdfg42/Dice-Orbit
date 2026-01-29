using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 캐릭터 프리셋 (선택 가능한 캐릭터)
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterPreset", menuName = "DiceOrbit/Character Preset")]
    public class CharacterPreset : ScriptableObject
    {
        [Header("Basic Info")]
        public string CharacterName = "Hero";
        public Sprite Portrait;
        
        [Header("Description")]
        [TextArea(3, 5)]
        public string Description;
        
        [Header("Base Stats")]
        public int MaxHP = 30;
        public int Attack = 5;
        public int Defense = 0;
        public Sprite CharacterSprite;
        public Color SpriteColor = Color.white;
        
        [Header("Fixed Loadout (Refactor 2.0)")]
        public CharacterSkill BasicAttack; // Universal Basic Attack (Dice x 1.0)
        public CharacterSkill ActiveSkill; // Unique Active
        public Data.Passives.PassiveAbility PassiveSkill; // Unique Passive

        // Deprecated Lists removed
        
        /// <summary>
        /// CharacterStats 생성
        /// </summary>
        public CharacterStats CreateStats()
        {
            var stats = new CharacterStats
            {
                CharacterName = this.CharacterName,
                Level = 1,
                MaxHP = this.MaxHP,
                CurrentHP = this.MaxHP,
                Attack = this.Attack,
                Defense = this.Defense,
                CharacterSprite = this.CharacterSprite,
                SpriteColor = this.SpriteColor
            };
            
            // Fixed Loadout Initialization
            if(BasicAttack != null) stats.RuntimeActiveSkills.Add(new RuntimeSkill(BasicAttack));
            if(ActiveSkill != null) stats.RuntimeActiveSkills.Add(new RuntimeSkill(ActiveSkill));
            
            // Passive Handling (Need Runtime wrapper for PassiveAbility? 
            // Current system: 'RuntimePassiveSkills' stores 'RuntimeSkill' which wraps 'CharacterSkill'.
            // But 'PassiveAbility' is a DIFFERENT system (MonoBehaviour-like hooks).
            // We need to register PassiveAbility to PassiveManager, NOT RuntimePassiveSkills list (which was for draftable attributes).
            // Actually, in Phase 1 I made PassiveManager trigger PassiveAbility.
            // So we just need to pass the PassiveAbility to the Character instance later.
            // CharacterStats doesn't hold 'PassiveAbility' instances directly for logic, 
            // but for UI/Persistence it might be good.
            // Let's store it in a new list or use the existing NativePassives concept logic.
            // Wait, I removed NativePassives list above.
            // I should add a 'AvailablePassives' or similar list to Stats to hold the data reference.
            // CharacterStats has 'RuntimePassiveSkills' (list of RuntimeSkill).
            // But 'PassiveSkill' field here is 'PassiveAbility' class.
            // I will add a 'FixedPassive' field to CharacterStats to hold this reference.
            // Or just rely on Character.Initialize adding it to PassiveManager.
            // Let's add 'StartingPassive' to CharacterStats to carry it over.
            if(PassiveSkill != null) stats.StartingPassive = PassiveSkill;
            
            stats.SourcePreset = this;
            
            return stats;
        }
    }
}
