using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data;
using System.Linq;

namespace DiceOrbit.Core
{
    public class SkillDraftManager : MonoBehaviour
    {
        public static SkillDraftManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public struct DraftOption
        {
            public CharacterSkill Skill;
            public bool IsNew;
            public int CurrentLevel; // 0 if new
        }

        public List<DraftOption> DraftSkills(CharacterStats characterStats)
        {
            var options = new List<DraftOption>();
            var preset = characterStats.SourcePreset;

            if (preset == null || preset.AvailableSkills == null)
            {
                Debug.LogWarning($"[Draft] Character {characterStats.CharacterName} has no preset or skill pool.");
                return options;
            }

            // Separate pools logic if needed, but for now we pick Actives and Passives from the single list based on Type.
            var pool = preset.AvailableSkills;

            // Pick 2 Actives
            var activePool = pool.Where(s => s.Type == CharacterSkillType.Active).ToList();
            var chosenActives = PickRandom(activePool, 2);
            
            // Pick 2 Passives
            var passivePool = pool.Where(s => s.Type == CharacterSkillType.Passive).ToList();
            var chosenPassives = PickRandom(passivePool, 2);

            // Combine
            var allChosen = new List<CharacterSkill>();
            allChosen.AddRange(chosenActives);
            allChosen.AddRange(chosenPassives);

            // Convert to DraftOption
            foreach (var skill in allChosen)
            {
                // Check if character already has this skill
                RuntimeSkill existing = FindRuntimeSkill(characterStats, skill);
                
                // If max level, maybe skip? For now, we allow it (could be placeholder for 'Heal' or 'Gold') or just filter it out before picking.
                // Let's simple logic: Just show it. UI can handle "Maxed Out" display or we filter here.
                // Filter Max Level Logic:
                if (existing != null && existing.IsMaxLevel)
                {
                    // For prototype, skip maxed skills? Or show as "Max"?
                    // Let's assume unlimited levels or user handles it. 
                    // Actually, let's just create the option.
                }

                options.Add(new DraftOption
                {
                    Skill = skill,
                    IsNew = existing == null,
                    CurrentLevel = existing != null ? existing.CurrentLevel : 0
                });
            }

            return options;
        }

        private List<CharacterSkill> PickRandom(List<CharacterSkill> pool, int count)
        {
            if (pool.Count <= count) return new List<CharacterSkill>(pool);

            var picked = new HashSet<CharacterSkill>();
            while (picked.Count < count)
            {
                var r = pool[Random.Range(0, pool.Count)];
                picked.Add(r);
            }
            return picked.ToList();
        }

        private RuntimeSkill FindRuntimeSkill(CharacterStats stats, CharacterSkill skill)
        {
            if (skill.Type == CharacterSkillType.Active)
            {
                return stats.RuntimeActiveSkills.Find(s => s.BaseSkill == skill);
            }
            else
            {
                return stats.RuntimePassiveSkills.Find(s => s.BaseSkill == skill);
            }
        }
    }
}
