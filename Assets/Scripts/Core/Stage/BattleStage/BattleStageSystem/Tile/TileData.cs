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
    public class TileData : MonoBehaviour, ICombatReactor
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

        /// <summary>타일 타입. 현재 규칙: 0번 타일 = LevelUp, 나머지 = Normal (ResolveTileTypeName과 동일).</summary>
        public TileType Type => tileIndex == 0 ? TileType.LevelUp : TileType.Normal;
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

                // 타일 설치 연출: 속성 타입별 큐(없으면 상위 "tile" 폴백). 플레이 중 + opt-in 속성만.
                // (공격/방어처럼 기본 탑재라 매 타일 설치되는 건 PlaysInstallVfx=false로 제외)
                if (Application.isPlaying && attribue.PlaysInstallVfx)
                    VfxService.Play("tile." + attribue.Type, Position);
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

        public void RemoveAttributeType(TileAttributeType attributeType)
        {
            if (attributes.ContainsKey(attributeType))
            {
                var attribute = attributes[attributeType];
                RemoveAttribute(attribute);
            }
        }


        public IReadOnlyCollection<TileAttribute> GetAttributes()
        {
            return attributes.Values.ToList().AsReadOnly();
        }

        public bool HasAttribute(TileAttributeType attributeType)
        {
            return attributes.ContainsKey(attributeType);
        }

        /// <summary>정보 패널용 타일 구조화 데이터.</summary>
        public UI.TileInfoData GetTileInfo() => UI.UnitInfoBuilder.Build(this);

        void ICombatReactor.OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 각 속성의 반응 로직 실행 (TileAttribute가 스스로 Duration 관리)
            foreach (var attribute in attributes.Values.ToList())
            {
                attribute.OnReact(trigger, context);
            }
            // 지속시간 감소/만료 정리는 TickTurnEnd()(직접 틱)에서 처리한다.
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

        /// <summary>라운드 종료 시 모든 속성 지속시간을 1 감소시키고 만료된 속성을 제거한다.</summary>
        public void TickTurnEnd()
        {
            if (attributes.Count == 0) return;

            foreach (var attribute in attributes.Values.ToList())
                attribute?.TickDuration();

            CleanupExpiredAttributes();
        }

        public List<Character> GetCharactersOnTile()
        {
            var characters = new List<Character>();
            if (Core.PartyManager.Instance != null)
            {
                foreach (var ally in Core.PartyManager.Instance.Party)
                {
                    if (ally != null && ally.CurrentTile == this)
                    {
                        characters.Add(ally);
                    }
                }
            }
            return characters;
        }

        public void OnArrive(Core.Character character)
        {
            DiceOrbit.Visuals.VfxService.PlayTileEvent(this, DiceOrbit.Visuals.TileVfxTrigger.OnArrive);
            if (tileIndex == 0)
                DiceOrbit.Visuals.VfxService.PlayOn(DiceOrbit.Visuals.VfxTags.LevelUp, this);
            foreach (var attribute in attributes.Values.ToList())
            {
                if (attribute == null) continue;
                attribute.OnArrive(character);
            }
        }

        internal void OnTraverse(Character character)
        {
            DiceOrbit.Visuals.VfxService.PlayTileEvent(this, DiceOrbit.Visuals.TileVfxTrigger.OnTraverse);
            foreach (var attribute in attributes.Values.ToList())
            {
                if (attribute == null) continue;
                attribute.OnTraverse(character);
            }
        }

        public void OnEndTurn(Core.Character character)
        {
            DiceOrbit.Visuals.VfxService.PlayTileEvent(this, DiceOrbit.Visuals.TileVfxTrigger.OnEndTurn);
            foreach (var attribute in attributes.Values.ToList())
            {
                if (attribute == null) continue;
                attribute.OnEndTurn(character);
            }
        }
    }
}
