using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data.MonsterPresets.Wave1.Goblin;
using DiceOrbit.Data.Skills.Effects;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 타겟 타입
    /// </summary>
    public enum TargetType
    {
        Characters,     // 단일 타겟
        Tiles,          // 특정 범위 (주변 N칸)
    }

    /// <summary>
    /// 타겟 선정 전략
    /// </summary>
    public enum TargetSelectionStrategy
    {
        RandomCharacter,  // 무작위 선택
        AllTargets,        // 모든 타겟
        RandomTiles,
    }

    /// <summary>
    /// 공격 의도 타입
    /// </summary>
    public enum IntentType
    {
        Attack,      // 공격
        Defend,      // 방어 버프
        Buff,        // 공격력 버프
        Special,     // 특수 행동
        Multi        // 다중 공격 (Deprecated: Use Attack with TargetType.Area/All)
    }

    /// <summary>
    /// 몬스터 스킬 (직렬화 가능, 에디터에서 수치 조정 가능)
    /// </summary>
    [System.Serializable]
    public class MonsterSkill
    {
        [Header("Skill Data")]
        public SkillData skillData;

        [Header("Targeting")]
        [SerializeField] private TargetSelectionStrategy targetStrategy = TargetSelectionStrategy.RandomCharacter;
        [SerializeField] private TargetType targetType = TargetType.Characters;
        [SerializeField] private int targetCount = 1; // 타겟 수
        [SerializeField] private IntentType intentType=IntentType.Attack;
        [Tooltip("표시할 스킬 아이콘")]
        [SerializeField] private Sprite intentIcon;

        // 런타임에 생성된 AttackIntent (캐싱용)
        private AttackIntent cachedIntent;

        /// <summary>
        /// 스킬 대상 선정 및 AttackIntent 생성
        /// </summary>
        public AttackIntent GenerateIntent(Monster owner)
        {
            if (skillData == null)
            {
                Debug.LogWarning("[MonsterSkill] SkillData is null!");
                return null;
            }

            // 타겟 선정
            List<Character> selectedTargets = new();

            // TileData 선정 (필요한 경우)
            List<TileData> targetTiles = new();

            switch (targetType)
            {
                case TargetType.Characters:
                    selectedTargets = SelectTargets();
                    break;
                case TargetType.Tiles:
                    targetTiles = SelectTiles();
                    break;
                default:
                    Debug.LogWarning("[MonsterSkill] Invalid TargetType specified!");
                    return null;
            }

            var resolvedTargetType = targetType;
            // 타일 프리뷰를 덮어쓰는 이펙트 탐색 (오버라이딩 로직)
            // (MineBomb 등 특정 클래스에 종속되지 않고 다형성을 활용)
            if (skillData is MonsterSkillData monsterSkillData && monsterSkillData.Effects != null)
            {
                foreach (var effect in monsterSkillData.Effects)
                {
                    if (effect is SkillEffectBase skillEffect)
                    {
                        var previewTiles = skillEffect.GetTargetTilesPreview(owner);
                        if (previewTiles != null && previewTiles.Count > 0)
                        {
                            targetTiles.AddRange(previewTiles);
                            resolvedTargetType = TargetType.Tiles;
                            selectedTargets.Clear();
                        }
                    }
                }
            }

            // IntentType 결정
            IntentType type = DetermineIntentType(); // To avoid variable shadowing

            // AttackIntent 생성
            var intent = new AttackIntent(
                type, 
                resolvedTargetType,
                selectedTargets,
                intentIcon // 아이콘 정보 전달
            );
            intent.TargetTiles = targetTiles;

            cachedIntent = intent;
            return intent;
        }

        /// <summary>
        /// 타겟 선정 로직
        /// </summary>
        private List<Character> SelectTargets()
        {
            // PartyManager에서 생존한 캐릭터 가져오기
            var candidates = PartyManager.Instance?.GetAliveCharacters();
            if (candidates == null || candidates.Count == 0)
            {
                Debug.LogWarning("[MonsterSkill] No alive characters found!");
                return null;
            }

            if (candidates == null || candidates.Count == 0)
                return new List<Character>();

            return SelectCharactersByTargetStrategy(targetStrategy);
        }

        private List<Character> SelectCharactersByTargetStrategy(TargetSelectionStrategy strategy)
        {
            var candidates = PartyManager.Instance?.GetAliveCharacters();
            List<Character> selected = new();
            switch (strategy)
            {
                case TargetSelectionStrategy.RandomCharacter:
                    int count = Mathf.Min(targetCount, candidates.Count);
                    List<Character> shuffled = new List<Character>(candidates);

                    for (int i = 0; i < count; i++)
                    {
                        int randomIndex = Random.Range(i, shuffled.Count);
                        Character temp = shuffled[i];
                        shuffled[i] = shuffled[randomIndex];
                        shuffled[randomIndex] = temp;
                    }

                    selected = shuffled.GetRange(0, count);
                    break;
                case TargetSelectionStrategy.AllTargets:
                    selected.AddRange(candidates);
                    break;
                case TargetSelectionStrategy.RandomTiles:
                    Debug.LogError("[MonsterSkill] TargetSelectionStrategy.RandomTiles is not valid for Character TargetType!");
                    break;
                default:
                    selected.Add(candidates[Random.Range(0, candidates.Count)]);
                    Debug.LogWarning("has an invalid TargetSelectionStrategy, defaulting to random selection.");
                    break;
            }
            return selected;
        }

        /// <summary>
        /// 타일 기반 타겟 선정 (MonsterTileActionModule 처리)
        /// </summary>
        private List<TileData> SelectTiles()
        {
            var tileList = GameManager.Instance?.GetOrbitManager().Tiles;
            List<TileData> selectedTiles = new();
            List<Character> candidates = PartyManager.Instance?.GetAliveCharacters();
            switch (targetStrategy)
            {
                case TargetSelectionStrategy.RandomCharacter:
                    selectedTiles = SelectTilesByCharacter(SelectCharactersByTargetStrategy(targetStrategy));
                    break;
                case TargetSelectionStrategy.AllTargets:
                    selectedTiles = SelectTilesByCharacter(SelectCharactersByTargetStrategy(targetStrategy));
                    break;
                case TargetSelectionStrategy.RandomTiles:
                    if (tileList != null && tileList.Count > 0)
                    {
                        int count = Mathf.Min(targetCount, tileList.Count);
                        List<TileData> shuffled = new List<TileData>(tileList);

                        for (int i = 0; i < count; i++)
                        {
                            int randomIndex = Random.Range(i, shuffled.Count);
                            TileData temp = shuffled[i];
                            shuffled[i] = shuffled[randomIndex];
                            shuffled[randomIndex] = temp;
                        }

                        selectedTiles = shuffled.GetRange(0, count);
                    }
                    break;
                default:
                    Debug.LogWarning("has an invalid TargetSelectionStrategy, defaulting to random selection.");
                    break;
            }
            return selectedTiles;
        }

        private List<TileData> SelectTilesByCharacter(List<Character> candidates)
        {
            List<TileData> selectedTiles = new();

            if (candidates == null || candidates.Count == 0)
                return selectedTiles;

            foreach (var character in candidates)
            {
                if (character != null && character.CurrentTile != null)
                {
                    if (!selectedTiles.Contains(character.CurrentTile))
                    {
                        selectedTiles.Add(character.CurrentTile);
                    }
                }
            }

            return selectedTiles;
        }

        /// <summary>
        /// SkillData에서 IntentType 추론
        /// </summary>
        private IntentType DetermineIntentType()
        {
            return intentType;
        }
    }
}
