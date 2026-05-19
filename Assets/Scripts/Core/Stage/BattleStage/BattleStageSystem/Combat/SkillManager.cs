using UnityEngine;
using DiceOrbit.Data;
using System.Collections.Generic;
using DiceOrbit.Data.Skills;
using DiceOrbit.UI;
using System.Collections;

namespace DiceOrbit.Core
{
    public class SkillManager : MonoBehaviour
    {
        public static SkillManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void PrepareSkill(Character source, int skillIndex, DiceData dice)
        {
            if (source == null || dice == null) return;

            if (skillIndex < 0 || skillIndex >= source.Stats.ActiveAbilityCount)
            {
                Debug.LogWarning($"[SkillManager] Invalid skill index: {skillIndex}");
                CharacterActionUI.Instance?.ReturnDiceElement();
                return;
            }

            ActiveSkillSlot runtimeAbility = source.Stats.GetActiveAbilityByIndex(skillIndex);
            if (runtimeAbility == null || runtimeAbility.BaseSkill == null)
            {
                CharacterActionUI.Instance?.ReturnDiceElement();
                return;
            }

            if (!runtimeAbility.CanUse(dice.Value))
            {
                Debug.LogWarning($"[SkillManager] Cannot use {runtimeAbility.BaseSkill.SkillName}. Requirement/Cooldown not met.");
                CharacterActionUI.Instance?.ReturnDiceElement();
                return;
            }

            Debug.Log($"[SkillManager] Preparing {runtimeAbility.BaseSkill.SkillName} for {source.Stats.CharacterName}");

            CharacterSkillTargetType targetType = runtimeAbility.TargetType;

            if (IsImmediateTargetType(targetType))
            {
                ConfirmSkillExecution(source, runtimeAbility, dice,
                    ResolveAllUnits(targetType),
                    ResolveAllTiles(targetType));
            }
            else
            {
                dice.State = DiceState.Reserved;
                DiceUI.Instance?.RefreshDiceVisual(dice);

                var targetSelector = SkillTargetSelector.Instance;
                if (targetSelector != null)
                {
                    targetSelector.StartTargetSelection(source, runtimeAbility, dice);
                    CharacterActionUI.Instance?.Hide();
                }
                else
                {
                    Debug.LogError("[SkillManager] SkillTargetSelector not found!");
                    CharacterActionUI.Instance?.ReturnDiceElement();
                }
            }
        }

        public void ConfirmSkillExecution(Character source, ActiveSkillSlot ability, DiceData dice, List<Unit> targets, List<TileData> tiles)
        {
            var combatManager = CombatManager.Instance;
            var diceManager = DiceManager.Instance;

            if (dice == null || source == null || ability == null || combatManager == null || diceManager == null)
            {
                CharacterActionUI.Instance?.ReturnDiceElement();
                return;
            }

            // TargetType이 None이나 AllTiles일 때는 여기서 처음 AssignDice를 호출함.
            // OneEnemy나 OneTile일 때는 이미 StartTargetSelection에서 락(Reserved)을 걸어두었음.
            if (dice.State != DiceState.Reserved)
            {
                bool success = diceManager.AssignDice(dice, source, DiceOrbit.Core.Pipeline.ActionType.Skill);
                if (!success)
                {
                    CharacterActionUI.Instance?.ReturnDiceElement();
                    return;
                }
            }

            if (!combatManager.TrySpendAction(source))
            {
                diceManager.UnassignDice(dice);
                CharacterActionUI.Instance?.ReturnDiceElement();
                return;
            }

            ActionQueueManager.Instance.EnqueueAction(
                FinalExecutionRoutine(source, ability, targets, tiles, dice)
            );

            DiceUI.Instance?.MarkDiceAsUsed(dice);
            CharacterActionUI.Instance?.ReturnDiceElement();
            CharacterActionUI.Instance?.Hide();
        }

        private IEnumerator FinalExecutionRoutine(Character source, ActiveSkillSlot ability, List<Unit> targets, List<TileData> tiles, DiceData dice)
        {
            if (source != null && source.IsAlive)
            {
                source.OnSkillExecutionStarted();
                ability.Execute(source, targets ?? new List<Unit>(), tiles ?? new List<TileData>(), dice.Value);
                source.OnSkillResolved();
            }

            yield return new WaitForSeconds(0.5f);
        }

        private static bool IsImmediateTargetType(CharacterSkillTargetType t) =>
            t == CharacterSkillTargetType.None      ||
            t == CharacterSkillTargetType.AllTiles  ||
            t == CharacterSkillTargetType.AllEnemies ||
            t == CharacterSkillTargetType.AllAllies;

        private static List<Unit> ResolveAllUnits(CharacterSkillTargetType t)
        {
            var list = new List<Unit>();
            if (t == CharacterSkillTargetType.AllEnemies)
            {
                var monsters = CombatManager.Instance?.ActiveMonsters;
                if (monsters != null) foreach (var m in monsters) if (m != null) list.Add(m);
            }
            else if (t == CharacterSkillTargetType.AllAllies)
            {
                var chars = PartyManager.Instance?.GetAliveCharacters();
                if (chars != null) foreach (var c in chars) if (c != null) list.Add(c);
            }
            return list;
        }

        private List<TileData> ResolveAllTiles(CharacterSkillTargetType t)
        {
            var list = new List<TileData>();
            if (t == CharacterSkillTargetType.AllTiles)
            {
                var orbitManager = FindAnyObjectByType<OrbitManager>();
                if (orbitManager != null) list.AddRange(orbitManager.Tiles);
            }
            return list;
        }
    }
}
