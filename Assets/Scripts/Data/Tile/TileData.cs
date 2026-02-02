using UnityEngine;
using DiceOrbit.Visuals;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;

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
    public class TileData : MonoBehaviour
    {
        [Header("Tile Properties")]
        [SerializeField] private int tileIndex;
        [SerializeField] private TileType type; // Added Type field

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
        public TileType Type => type; // Exposed Property
        public TileData NextTile => nextTile;
        public TileData PreviousTile => previousTile;
        public Vector3 Position => transform.position;

        /// <summary>
        /// 타일 초기화
        /// </summary>
        public void Initialize(int index, TileType type, TileVisual visual = null)
        {
            tileIndex = index;
            this.type = type; // Store type
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
