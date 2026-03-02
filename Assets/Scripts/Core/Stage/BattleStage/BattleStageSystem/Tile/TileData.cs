using UnityEngine;
using DiceOrbit.Visuals;
using DiceOrbit.Data.Tile;
using DiceOrbit.Core.Pipeline;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using DiceOrbit.Core;
using System;

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
    public class TileData : MonoBehaviour, UI.IHoverTooltipProvider, ICombatReactor
    {
        [Header("Tile Properties")]
        [SerializeField] private int tileIndex;

        [Header("Attribute")]
        private Dictionary<TileAttributeType, TileAttribute> attributes = new Dictionary<TileAttributeType, TileAttribute>();

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
        int ICombatReactor.Priority => 11;

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
            if (attribue == null) return;

            if (!attributes.ContainsKey(attribue.Type))
            {
                attribue.SetOwner(this);
                attributes.Add(attribue.Type, attribue);
                UI.TileAttributeBubbleManager.EnsureInstance();
                UI.TileAttributeBubbleManager.Instance?.RefreshTile(this);
            }
        }

        public void RemoveAttribute(TileAttribute attribute)
        {
            if (attribute == null) return;

            if (attributes.ContainsKey(attribute.Type) && attributes[attribute.Type] == attribute)
            {
                attributes.Remove(attribute.Type);
                attribute.SetOwner(null);
                Debug.Log($"[TileAttribute] {attribute.Type} removed from Tile #{tileIndex}.");
                UI.TileAttributeBubbleManager.EnsureInstance();
                UI.TileAttributeBubbleManager.Instance?.RefreshTile(this);
            }
        }

        public IReadOnlyCollection<TileAttribute> GetAttributes()
        {
            return attributes.Values.ToList().AsReadOnly();
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
            var detailLines = new List<string>();

            foreach (var attribute in attributes)
            {
                if (attribute.Value == null) continue;
                detailLines.AddRange(attribute.Value.GetTooltipDescriptions());
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Tile #{tileIndex}");
            sb.AppendLine($"Type: {ResolveTileTypeName()}");
            if (attributes.Count > 0)
            {
                sb.AppendLine($"Attributes: {attributes.Count}");
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

        void ICombatReactor.OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 각 속성의 반응 로직 실행 (TileAttribute가 스스로 Duration 관리)
            foreach (var attribute in attributes.Values.ToList())
            {
                attribute.OnReact(trigger, context);
            }

            // 턴 시작 시, 반응 처리 후 만료된 속성 정리
            if (context.Action.Type==ActionType.OnStartTurn && context.IsTiling == true)
            {
                CleanupExpiredAttributes();
            }
        }

        private void CleanupExpiredAttributes()
        {
            var attributesToRemove = attributes.Values.Where(attr => attr.Duration != -1 && attr.Duration <= 0).ToList();
            foreach (var attribute in attributesToRemove)
            {
                Debug.Log($"[TileAttribute] {attribute.Type} expired on Tile #{tileIndex}.");
                RemoveAttribute(attribute);
            }
            UI.TileAttributeBubbleManager.EnsureInstance();
            UI.TileAttributeBubbleManager.Instance?.RefreshTile(this);
        }

        public void OnArrive(Core.Character character)
        {
            TileVfxManager.PlayTileEvent(this, TileVfxTrigger.OnArrive, character);
            foreach (var attribute in attributes.Values.ToList())
            {
                if (attribute == null) continue;
                attribute.OnArrive(character);
            }
        }

        internal void OnTraverse(Character character)
        {
            TileVfxManager.PlayTileEvent(this, TileVfxTrigger.OnTraverse, character);
            foreach (var attribute in attributes.Values.ToList())
            {
                if (attribute == null) continue;
                attribute.OnTraverse(character);
            }
        }

        public void OnEndTurn(Core.Character character)
        {
            TileVfxManager.PlayTileEvent(this, TileVfxTrigger.OnEndTurn, character);
            foreach (var attribute in attributes.Values.ToList())
            {
                if (attribute == null) continue;
                attribute.OnEndTurn(character);
            }
        }
    }
}
