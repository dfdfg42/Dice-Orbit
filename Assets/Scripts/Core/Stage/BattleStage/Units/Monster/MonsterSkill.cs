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
        Self,           // 자기 자신 (버프, 회복 등)
        None            // 타겟 없음 (빈둥거림, 필드 전체 기믹 발동 등)
    }

    /// <summary>
    /// 타겟 선정 전략
    /// </summary>
    public enum TargetSelectionStrategy
    {
        RandomCharacter,  // 무작위 선택
        AllTargets,        // 모든 타겟
        RandomTiles,
        TilesWithAttribute, // 특정 속성이 있는 타일 중심
        Self               // 자기 자신
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
        [SerializeReference] // 다형성 지원 (각 스킬 클래스 선택 가능)
        public SkillData skillData;

        [Header("Targeting")]
        [SerializeField] private TargetSelectionStrategy targetStrategy = TargetSelectionStrategy.RandomCharacter;
        [SerializeField] private TargetType targetType = TargetType.Characters;

        [Tooltip("TargetStrategy가 TilesWithAttribute일 때 탐색할 타일 속성")]
        [SerializeField] private DiceOrbit.Data.Tile.TileAttributeType targetTileAttribute = DiceOrbit.Data.Tile.TileAttributeType.None;

        [SerializeField] private int targetCount = 1; // 타겟 수
        [SerializeField] private int targetRange = 0; // 타겟으로부터 범위 (타일 기반 타겟팅 시 사용)
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
            List<Unit> selectedTargets = new();

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
                case TargetType.Self:
                    selectedTargets.Add(owner);
                    break;
                case TargetType.None:
                    // 타겟 없음
                    break;
                default:
                    Debug.LogWarning($"[MonsterSkill] Invalid TargetType specified! {targetType}");
                    return null;
            }

            // IntentType 결정
            IntentType type = DetermineIntentType(); // To avoid variable shadowing

            // AttackIntent 생성
            var intent = new AttackIntent(
                type, 
                targetType,
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
                case TargetSelectionStrategy.TilesWithAttribute:
                    Debug.LogError($"[MonsterSkill] {strategy} is not valid for Character TargetType!");
                    break;
                case TargetSelectionStrategy.Self:
                    // Self 처리는 위에서 TargetType.Self로 우회하므로 여기선 할 일이 없으나, 만일을 대비해 빈 리스트 반환
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
                case TargetSelectionStrategy.TilesWithAttribute:
                    if (tileList != null && tileList.Count > 0)
                    {
                        // 1. 해당 속성을 가진 타일들을 모두 찾기
                        var matchedTiles = tileList.Where(t => t != null && t.HasAttribute(targetTileAttribute)).ToList();

                        // 2. 찾아낸 타일들을 중심으로 지정된 targetRange 만큼 범위 확장
                        selectedTiles = ExpandTilesRange(matchedTiles, targetRange);
                    }
                    break;
                case TargetSelectionStrategy.Self:
                    Debug.LogError("[MonsterSkill] TargetSelectionStrategy.Self is not valid for Area Tile TargetType!");
                    break;
                default:
                    Debug.LogWarning("has an invalid TargetSelectionStrategy, defaulting to random selection.");
                    break;
            }
            
            return selectedTiles;
        }

        /// <summary>
        /// 주어진 타일들의 양옆(range) 범위를 포함하여 중복 없이 타일 목록을 반환합니다.
        /// </summary>
        private List<TileData> ExpandTilesRange(List<TileData> centerTiles, int range)
        {
            var expandedTiles = new HashSet<TileData>();
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null || centerTiles == null || centerTiles.Count == 0)
                return expandedTiles.ToList();

            const int TotalTiles = 20; // 타일 총 개수

            foreach (var centerTile in centerTiles)
            {
                if (centerTile != null)
                {
                    int centerIndex = centerTile.TileIndex;

                    for (int offset = -range; offset <= range; offset++)
                    {
                        int targetIndex = (centerIndex + offset + TotalTiles) % TotalTiles;
                        var tile = orbitManager.GetTile(targetIndex);
                        if (tile != null)
                        {
                            expandedTiles.Add(tile);
                        }
                    }
                }
            }

            return expandedTiles.ToList();
        }

        private List<TileData> SelectTilesByCharacter(List<Character> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return new List<TileData>();

            var centerTiles = new List<TileData>();
            foreach(var c in candidates)
            {
                if(c != null && c.CurrentTile != null)
                {
                    centerTiles.Add(c.CurrentTile);
                }
            }

            return ExpandTilesRange(centerTiles, targetRange);
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
