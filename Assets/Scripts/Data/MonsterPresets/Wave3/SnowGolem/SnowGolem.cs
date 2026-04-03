using UnityEngine;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using System.Collections.Generic;
using DiceOrbit.Data.Monsters;

namespace DiceOrbit.Data.MonsterPresets.Wave3.SnowGolem
{
    // ==========================================
    // 1. 스노우 골렘 스킬 구현
    // ==========================================
    /// <summary>
    /// 스노우 골렘이 사용할 스킬 틀입니다. SkillData를 상속받습니다.
    /// 구체적인 수치나 로직은 필요에 따라 채워넣으세요.
    /// </summary>
    [System.Serializable]
    public class SnowGolemSkill1 : SkillData
    {
        public SnowGolemSkill1()
        {
            skillName = "눈 강타";
            description = "";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            // TODO: 스노우 골렘 스킬 효과 구현 (데미지나 빙결, 방어막 등)
        }
    }

    [System.Serializable]
    public class SnowGolemSkill2 : SkillData
    {
        public SnowGolemSkill2()
        {
            skillName = "스노우 골렘 스킬";
            description = "스노우 골렘 스킬의 설명입니다.";
        }

        public override void Execute(Unit source, List<Unit> targetUnits, List<TileData> targetTiles, int diceValue)
        {
            // TODO: 스노우 골렘 스킬 효과 구현 (데미지나 빙결, 방어막 등)
        }
    }

    // ==========================================
    // 2. 스노우 골렘 사망 효과 구현 (필요 시 주석 해제)
    // ==========================================
    [System.Serializable]
    public class SnowGolemDeath : DeathEffect
    {
        public SnowGolemDeath()
        {
            effectName = "사망 시 효과 디폴트 이름";
            description = "눈 골렘이 사망 시, 눈 감옥 타일들이 전부 사라집니다.";
        }

        public override void Execute(Monster deadMonster)
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            foreach (var tile in orbitManager?.Tiles ?? new List<TileData>())
            {
                tile.RemoveAttributeType(TileAttributeType.SnowPrison);
            }
        }
    }

    // ==========================================
    // 3. 스노우 골렘 패시브 구현
    // ==========================================
    /// <summary>
    /// 스노우 골렘의 고유 패시브 스킬 틀입니다. PassiveAbility를 상속받습니다.
    /// </summary>
    [System.Serializable]
    public class SnowGolemPassive : PassiveAbility
    {
        int tileDuration = 1, frozenDuration = 1; // 타일 지속 시간 (1턴)
        public SnowGolemPassive()
        {
            passiveName = "눈 감옥 생성";
            description = "매 턴 시작 시, 무작위 타일 1개와 그 좌우 타일에 눈 감옥을 설치합니다.";
            priority = 10; 
            isStackable = false;
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 예외 방지
            if (context?.Action == null) return;

            // 턴 시작 시점 감지
            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.OnStartTurn && 
                context.SourceUnit == owner)
            {
                Debug.Log($"[SnowGolemPassive] 눈 감옥 타일 설치 발동");
                PlantSnowPrisonTiles();
            }
        }

        private void PlantSnowPrisonTiles()
        {
            var orbitManager = GameManager.Instance?.GetOrbitManager();
            if (orbitManager == null) return;

            // 0~19 중앙 타일 1개 선정 (20개 타일 기준)
            int centerIndex = Random.Range(0, 20);
            // 좌우 타일 계산 (순환 구조에 맞게 % 연산 활용)
            int leftIndex = (centerIndex - 1 + 20) % 20;
            int rightIndex = (centerIndex + 1) % 20;

            int[] targetIndices = new int[] { leftIndex, centerIndex, rightIndex };

            foreach (var index in targetIndices)
            {
                var tile = orbitManager.GetTile(index);
                if (tile != null)
                {
                    // 눈 감옥 타일 속성 생성 (데미지 0, 1턴 지속)
                    var snowPrisonAttribute = new SnowPrisonTileAttribute(
                        TileAttributeType.SnowPrison, 
                        frozenDuration + 1,
                        tileDuration + 1
                    );

                    tile.AddAttribute(snowPrisonAttribute);
                    Debug.Log($"[SnowGolemPassive] SnowPrison Tile Generated at index: {index}");
                }
            }
        }

        public override bool AllowSamePassive(PassiveAbility incoming)
        {
            return false;
        }
    }
}
