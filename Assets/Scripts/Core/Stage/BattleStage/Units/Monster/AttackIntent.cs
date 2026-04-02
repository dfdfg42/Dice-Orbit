using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 몬스터 공격 의도 데이터
    /// </summary>
    [System.Serializable]
    public class AttackIntent
    {
        public IntentType Type;
        public TargetType TargetType = TargetType.Characters;
        public int AreaRadius = 0;   // 0: 단일, 1: 좌우 1칸 (총 3칸), etc.
        public Sprite Icon { get; private set; } // 추가된 아이콘 필드

        // 선정된 타겟들 (Unit 리스트)
        private List<Core.Unit> originTargets = new List<Core.Unit>();
        private List<Core.Unit> selectedTargets = new List<Core.Unit>();

        // 타겟 타일들 (몬스터 스킬이 타일 기반일 경우)
        private List<TileData> originTiles;
        private List<TileData> targetedTiles;

        [System.NonSerialized]
        private MonsterSkill skill; // 의도를 생성한 스킬 참조 (타겟 선정에 필요할 수 있음)
        /// <summary>
        /// 선정된 타겟 캐릭터들
        /// </summary>
        public List<Core.Unit> Targets
        {
            get => selectedTargets;
            set => selectedTargets = value ?? new List<Core.Unit>();
        }

        /// <summary>
        /// 타겟 타일들
        /// </summary>
        public List<TileData> TargetTiles
        {
            get => targetedTiles;
            set => targetedTiles = value;
        }

        public AttackIntent(IntentType type, int damage = 0, string desc = "")
        {
            Type = type;
        }

        /// <summary>
        /// 생성자 오버로드 (타겟 포함)
        /// </summary>
        public AttackIntent(IntentType type, TargetType targetType,List<Core.Unit> targets, Sprite icon = null)
        {
            Type = type;
            TargetType = targetType;
            selectedTargets = targets ?? new List<Core.Unit>();
            Icon = icon;
        }

        /// <summary>
        /// 생성자 오버로드 (MonsterSkill 기반 타겟 포함)
        /// </summary>
        public AttackIntent(MonsterSkill skill, Monster owner)
        {
            this.skill = skill;
            Type = skill.IntentType;
            TargetType = skill.TargetType;
            Icon = skill.IntentIcon;

            selectedTargets = new List<Unit>();
            targetedTiles = new List<TileData>();

            switch (TargetType)
            {
                case TargetType.Characters:
                    selectedTargets = SelectTargets(skill);
                    break;
                case TargetType.Tiles:
                    targetedTiles = SelectTiles(skill);
                    break;
                case TargetType.Self:
                    selectedTargets.Add(owner);
                    break;
                case TargetType.None:
                    // 타겟 없음
                    break;
                default:
                    Debug.LogWarning($"[AttackIntent] Invalid TargetType specified! {TargetType}");
                    break;
            }
        }

        private List<Unit> SelectTargets(MonsterSkill skill)
        {
            var candidates = PartyManager.Instance?.GetAliveCharacters()?.Cast<Unit>().ToList();
            if (candidates == null || candidates.Count == 0)
            {
                Debug.LogWarning("[AttackIntent] No alive characters found!");
                return new List<Unit>();
            }

            return SelectUnitsByTargetStrategy(skill.TargetStrategy, skill.TargetCount);
        }

        private List<Unit> SelectUnitsByTargetStrategy(TargetSelectionStrategy strategy, int targetCount)
        {
            var candidates = PartyManager.Instance?.GetAliveCharacters()?.Cast<Unit>().ToList();
            List<Unit> selected = new();
            if (candidates == null) return selected;

            switch (strategy)
            {
                case TargetSelectionStrategy.RandomCharacter:
                    int count = Mathf.Min(targetCount, candidates.Count);
                    List<Unit> shuffled = new List<Unit>(candidates);

                    for (int i = 0; i < count; i++)
                    {
                        int randomIndex = Random.Range(i, shuffled.Count);
                        Unit temp = shuffled[i];
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
                    Debug.LogError($"[AttackIntent] {strategy} is not valid for Character TargetType!");
                    break;
                case TargetSelectionStrategy.Self:
                    // Self 처리는 위에서 TargetType.Self로 우회하므로
                    break;
                default:
                    selected.Add(candidates[Random.Range(0, candidates.Count)]);
                    Debug.LogWarning("[AttackIntent] has an invalid TargetSelectionStrategy, defaulting to random selection.");
                    break;
            }
            return selected;
        }

        private List<TileData> SelectTiles(MonsterSkill skill)
        {
            var tileList = GameManager.Instance?.GetOrbitManager().Tiles;
            List<TileData> selectedTiles = new();
            switch (skill.TargetStrategy)
            {
                case TargetSelectionStrategy.RandomCharacter:
                case TargetSelectionStrategy.AllTargets:
                    selectedTiles = SelectTilesByUnit(SelectUnitsByTargetStrategy(skill.TargetStrategy, skill.TargetCount), skill.TargetRange);
                    break;
                case TargetSelectionStrategy.RandomTiles:
                    if (tileList != null && tileList.Count > 0)
                    {
                        int count = Mathf.Min(skill.TargetCount, tileList.Count);
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
                        var matchedTiles = tileList.Where(t => t != null && t.HasAttribute(skill.TargetTileAttribute)).ToList();
                        selectedTiles = ExpandTilesRange(matchedTiles, skill.TargetRange);
                    }
                    break;
                case TargetSelectionStrategy.Self:
                    Debug.LogError("[AttackIntent] TargetSelectionStrategy.Self is not valid for Area Tile TargetType!");
                    break;
                default:
                    Debug.LogWarning("[AttackIntent] has an invalid TargetSelectionStrategy, defaulting to random selection.");
                    break;
            }
            return selectedTiles;
        }

        private List<TileData> ExpandTilesRange(List<TileData> centerTiles, int range)
        {
            var expandedTiles = new HashSet<TileData>();
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null || centerTiles == null || centerTiles.Count == 0)
                return expandedTiles.ToList();

            const int TotalTiles = 20;

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

        private List<TileData> SelectTilesByUnit(List<Unit> candidates, int range)
        {
            if (candidates == null || candidates.Count == 0)
                return new List<TileData>();

            var centerTiles = new List<TileData>();
            foreach(var c in candidates)
            {
                if(c is Character character && character.CurrentTile != null)
                {
                    centerTiles.Add(character.CurrentTile);
                }
            }

            return ExpandTilesRange(centerTiles, range);
        }

        /// <summary>
        /// 타겟 새로고침 (죽은 캐릭터 제거)
        /// </summary>
        public void RefreshTargets()
        {
            if (selectedTargets == null) return;
            selectedTargets = selectedTargets.Where(t => t != null && t.IsAlive).ToList();
            // 임시로 타일 타입 기반 스킬일 때만 타일 새로고침 로직 추가
            // 필요하다면 나중에 TargetType 별로 타겟 새로고침 로직을 분리할 수 있음
            if (TargetType == TargetType.Tiles && skill.TargetStrategy == TargetSelectionStrategy.TilesWithAttribute)
            {
                TargetTiles = SelectTiles(skill);
            }
        }
    }
}
