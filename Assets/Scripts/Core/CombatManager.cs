using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 전투 관리자 (싱글톤)
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }
        
        [Header("Combat State")]
        [SerializeField] private bool inCombat = false;
        [SerializeField] private List<Monster> activeMonsters = new List<Monster>();
        
        // Events
        public System.Action OnCombatStart;
        public System.Action OnCombatEnd;
        public System.Action<Monster> OnMonsterDeath;
        
        // Properties
        public bool InCombat => inCombat;
        public List<Monster> ActiveMonsters => activeMonsters;
        
        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("Multiple CombatManagers detected! Destroying duplicate.");
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // Scene의 몬스터 자동 감지
            // AutoDetectMonsters(); // Refactor 2.0: Let GameFlowManager handle start
        }
        
        /// <summary>
        /// Scene의 몬스터 자동 감지
        /// </summary>
        private void AutoDetectMonsters()
        {
            var monsters = Object.FindObjectsByType<Monster>(FindObjectsSortMode.None);


            foreach (var monster in monsters)
            {
                if (!activeMonsters.Contains(monster))
                {
                    activeMonsters.Add(monster);
                }
            }
            
            if (activeMonsters.Count > 0)
            {
                StartCombat();
            }
        }
        
        /// <summary>
        /// 전투 시작
        /// </summary>
        public void StartCombat()
        {
            if (inCombat) return;
            
            inCombat = true;
            Debug.Log($"Combat started! {activeMonsters.Count} monster(s)");
            
            OnCombatStart?.Invoke();
        }
        
        /// <summary>
        /// 전투 종료
        /// </summary>
        public void EndCombat(bool victory)
        {
            if (!inCombat) return;
            
            inCombat = false;
            
            if (victory)
            {
                Debug.Log("Victory! All monsters defeated!");
            }
            else
            {
                Debug.Log("Defeat! Party wiped out!");
            }
            
            OnCombatEnd?.Invoke();
        }
        
        /// <summary>
        /// 몬스터 격파 처리
        /// </summary>
        public void OnMonsterDefeated(Monster monster)
        {
            activeMonsters.Remove(monster);
            OnMonsterDeath?.Invoke(monster);
            
            // 모든 몬스터 격파 확인
            if (activeMonsters.All(m => !m.IsAlive))
            {
                EndCombat(true);
            }
        }
        
        /// <summary>
        /// 몬스터 턴 실행
        /// </summary>
        public void ExecuteMonsterTurn()
        {
            if (!inCombat) return;
            
            Debug.Log("=== Monster Turn ===");
            
            foreach (var monster in activeMonsters)
            {
                if (monster.IsAlive)
                {
                    monster.ExecuteIntent();
                }
            }
            
            // 파티 전멸 체크
            var partyManager = PartyManager.Instance;
            if (partyManager != null && partyManager.IsPartyWiped())
            {
                EndCombat(false);
            }
        }
        
        /// <summary>
        /// 캐릭터가 몬스터 공격
        /// </summary>
        public void AttackMonster(Monster target, int damage, bool ignoreDefense = false)
        {
            if (target == null || !target.IsAlive) return;
            
            target.TakeDamage(damage, ignoreDefense);
        }
        
        /// <summary>
        /// 모든 몬스터 공격 (범위 공격)
        /// </summary>
        public void AttackAllMonsters(int damage, bool ignoreDefense = false)
        {
            foreach (var monster in activeMonsters)
            {
                if (monster.IsAlive)
                {
                    monster.TakeDamage(damage, ignoreDefense);
                }
            }
        }
        
        /// <summary>
        /// 생존한 몬스터 목록
        /// </summary>
        public List<Monster> GetAliveMonsters()
        {
            return activeMonsters.Where(m => m.IsAlive).ToList();
        }
    }
}
