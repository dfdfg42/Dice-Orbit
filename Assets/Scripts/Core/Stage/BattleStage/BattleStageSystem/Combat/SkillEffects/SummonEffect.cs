//using UnityEngine;
//using System.Collections.Generic;
//using DiceOrbit.Data.Monsters;

//namespace DiceOrbit.Data.SkillEffects
//{
//    /// <summary>
//    /// 소환 효과 - 몬스터를 소환합니다
//    /// </summary>
//    [CreateAssetMenu(fileName = "New Summon Effect", menuName = "DiceOrbit/Skill Effects/Summon Effect")]
//    public class SummonEffect : SkillEffectBase
//    {
//        [Header("Summon Settings")]
//        [Tooltip("소환할 몬스터 프리셋")]
//        public MonsterPreset MinionPreset;
        
//        [Tooltip("소환할 몬스터 수")]
//        [Range(1, 5)]
//        public int SummonCount = 1;
        
//        [Tooltip("소환 위치 (비어있으면 랜덤)")]
//        public Transform SpawnPoint;

//        public override void Execute(Core.Unit source, List<Core.Unit> targetUnits, List<TileData> targetTiles)
//        {
//            if (MinionPreset == null)
//            {
//                Debug.LogWarning("[SummonEffect] MinionPreset is null!");
//                return;
//            }

//            Debug.Log($"[SummonEffect] Summoning {SummonCount} {MinionPreset.name}");

//            // TODO: 실제 소환 로직 구현
//            // WaveManager 또는 CombatManager를 통해 몬스터 생성
            
//            // 예시:
//            // for (int i = 0; i < SummonCount; i++)
//            // {
//            //     var position = SpawnPoint != null ? SpawnPoint.position : GetRandomSpawnPosition();
//            //     SpawnMonster(MinionPreset, position);
//            // }
//        }
//    }
//}
