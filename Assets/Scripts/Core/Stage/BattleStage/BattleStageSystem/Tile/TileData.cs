using UnityEngine;
using DiceOrbit.Visuals;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using System.Text;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 타일 타입 열거형
    /// </summary>
    public enum TileType
    {
        Normal,      // 일반 타일
        LevelUp,     // 레벨업 타일 (시작점)
        Special      // 특수 타일 (추후 확장용)
    }

    /// <summary>
    /// 개별 타일 데이터
    /// </summary>
    public class TileData : MonoBehaviour, UI.IHoverTooltipProvider
    {
        [Header("Tile Properties")]
        [SerializeField] private int tileIndex;

        [Header("Attribute")]
        [SerializeField] private List<TileAttribute> attributes = new();

        [Header("Connections")]
        [SerializeField] private TileData nextTile;
        [SerializeField] private TileData previousTile;
        
        [Header("Visual")]
        [SerializeField] private TileVisual tileVisual;

        //[Header("Attributes")]
        //[SerializeField] private 

        // Properties
        public int TileIndex => tileIndex;
        public TileData NextTile => nextTile;
        public TileData PreviousTile => previousTile;
        public Vector3 Position => transform.position;

        /// <summary>
        /// 타일 초기화
        /// </summary>
        public void Initialize(int index, TileType type, TileVisual visual = null)
        {
            tileIndex = index;
            tileVisual = visual;
            
            if (tileVisual != null)
            {
                tileVisual.SetTileType(type);
            }
        }
        
        /// <summary>
        /// 연결 설정 (다음 타일, 이전 타일)
        /// </summary>
        public void SetConnections(TileData next, TileData previous)
        {
            nextTile = next;
            previousTile = previous;
        }
        
        /// <summary>
        /// 타일 하이라이트 (선택, 공격 대상 등)
        /// </summary>
        public void Highlight(Color color)
        {
            if (tileVisual != null)
            {
                tileVisual.SetHighlight(true, color);
            }
        }
        
        /// <summary>
        /// 하이라이트 해제
        /// </summary>
        public void ClearHighlight()
        {
            if (tileVisual != null)
            {
                tileVisual.SetHighlight(false, Color.white);
            }
        }

        public void AddAttribute(TileAttribute attribue)
        {
            attributes.Add(attribue);
        }
        
        public void OnTraverse(Core.Character character)
        {
            foreach (var attribute in attributes)
            {
                attribute.OnTraverse(character);
            }
        }
        public void OnArrive(Core.Character character)
        {
            foreach (var attribute in attributes)
            {
                attribute.OnArrive(character);
            }
        }

        private void OnMouseEnter()
        {
            Debug.Log($"[Hover] Tile enter: {name}");
            UI.HoverTooltipUI.EnsureInstance();
            if (UI.HoverTooltipUI.Instance != null)
            {
                UI.HoverTooltipUI.Instance.Show(BuildTooltipText());
            }
        }

        private void OnMouseExit()
        {
            Debug.Log($"[Hover] Tile exit: {name}");
            if (UI.HoverTooltipUI.Instance != null)
            {
                UI.HoverTooltipUI.Instance.Hide();
            }
        }

        private string BuildTooltipText()
        {
            int traverseCount = 0;
            int arriveCount = 0;
            var detailLines = new List<string>();

            foreach (var attribute in attributes)
            {
                if (attribute == null) continue;
                traverseCount += attribute.TraverseCount;
                arriveCount += attribute.ArriveCount;
                detailLines.AddRange(attribute.GetTooltipDescriptions());
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Tile #{tileIndex}");
            sb.AppendLine($"Type: {ResolveTileTypeName()}");
            if (attributes.Count > 0)
            {
                sb.AppendLine($"Attributes: {attributes.Count}");
            }
            if (traverseCount > 0 || arriveCount > 0)
            {
                sb.AppendLine($"Effects: Traverse {traverseCount}, Arrive {arriveCount}");
            }
            if (detailLines.Count > 0)
            {
                sb.AppendLine("--- Details ---");
                foreach (var line in detailLines)
                {
                    sb.AppendLine(line);
                }
            }

            return sb.ToString().TrimEnd();
        }

        public string GetHoverTooltipText()
        {
            return BuildTooltipText();
        }

        private string ResolveTileTypeName()
        {
            if (tileIndex == 0) return TileType.LevelUp.ToString();
            return TileType.Normal.ToString();
        }

        //래거시 코드
        ///// <summary>
        ///// 타일 효과를 캐릭터에게 적용
        ///// </summary>
        //public void ApplyEffects(Core.Character character)
        //{
        //    if (effects == null || effects.Length == 0) return;

        //    foreach (var effect in effects)
        //    {
        //        ApplyEffect(effect, character);
        //    }
        //}

        ///// <summary>
        ///// 개별 효과 적용
        ///// </summary>
        //private void ApplyEffect(TileEffect effect, Core.Character character)
        //{
        //    switch (effect.Type)
        //    {
        //        case TileEffectType.Heal:
        //            character.Stats.Heal(effect.Value);
        //            Debug.Log($"[Tile #{tileIndex}] {character.Stats.CharacterName} healed {effect.Value} HP!");
        //            break;

        //        case TileEffectType.Damage:
        //            character.Stats.TakeDamage(effect.Value);
        //            Debug.Log($"[Tile #{tileIndex}] {character.Stats.CharacterName} took {effect.Value} damage from trap!");
        //            break;

        //        case TileEffectType.BuffAttack:
        //            character.Stats.Attack += effect.Value;
        //            Debug.Log($"[Tile #{tileIndex}] {character.Stats.CharacterName} gained +{effect.Value} ATK!");
        //            break;

        //        case TileEffectType.BuffDefense:
        //            character.Stats.Defense += effect.Value;
        //            Debug.Log($"[Tile #{tileIndex}] {character.Stats.CharacterName} gained +{effect.Value} DEF!");
        //            break;

        //        case TileEffectType.LevelUp:
        //            character.Stats.LevelUp();
        //            Debug.Log($"[Tile #{tileIndex}] {character.Stats.CharacterName} leveled up!");
        //            break;
        //    }
        //}
    }
}
