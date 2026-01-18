using UnityEngine;
using DiceOrbit.Data;
using System.Collections.Generic;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 몬스터 (중앙 구역)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Monster : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private MonsterStats stats = new MonsterStats();
        
        [Header("AI")]
        [SerializeField] private List<AttackIntent> attackPatterns = new List<AttackIntent>();
        private AttackIntent currentIntent;
        
        [Header("Visual")]
        private SpriteRenderer spriteRenderer;
        private Camera mainCamera;
        
        // Properties
        public MonsterStats Stats => stats;
        public AttackIntent CurrentIntent => currentIntent;
        public bool IsAlive => stats.IsAlive;
        
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            if (spriteRenderer != null && stats.MonsterSprite != null)
            {
                spriteRenderer.sprite = stats.MonsterSprite;
                spriteRenderer.color = stats.SpriteColor;
            }
            
            mainCamera = Camera.main;
            
            // 기본 공격 패턴 설정 (Inspector에서 설정 안 했으면)
            if (attackPatterns.Count == 0)
            {
                attackPatterns.Add(new AttackIntent(IntentType.Attack, stats.Attack, "Basic Attack"));
                attackPatterns.Add(new AttackIntent(IntentType.Attack, stats.Attack + 3, "Heavy Attack"));
            }
        }
        
        private void LateUpdate()
        {
            // Billboard: 항상 카메라를 향하도록
            if (mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }
        
        private void Start()
        {
            // 첫 턴 의도 선택
            SelectNextIntent();
        }
        
        /// <summary>
        /// 다음 턴 의도 선택
        /// </summary>
        public void SelectNextIntent()
        {
            if (attackPatterns.Count > 0)
            {
                // 랜덤으로 패턴 선택
                currentIntent = attackPatterns[Random.Range(0, attackPatterns.Count)];
                Debug.Log($"{stats.MonsterName} next intent: {currentIntent}");
            }
        }
        
        /// <summary>
        /// 현재 의도 실행
        /// </summary>
        public void ExecuteIntent()
        {
            if (currentIntent == null || !IsAlive) return;
            
            switch (currentIntent.Type)
            {
                case IntentType.Attack:
                case IntentType.Multi:
                    AttackParty();
                    break;
                    
                case IntentType.Defend:
                    stats.Defense += 3;
                    Debug.Log($"{stats.MonsterName} defends! Defense +3");
                    break;
                    
                case IntentType.Buff:
                    stats.Attack += 2;
                    Debug.Log($"{stats.MonsterName} powers up! Attack +2");
                    break;
            }
            
            // 다음 턴 의도 선택
            SelectNextIntent();
        }
        
        /// <summary>
        /// 파티 공격
        /// </summary>
        private void AttackParty()
        {
            Debug.Log($"{stats.MonsterName} executing attack!");
            
            var partyManager = PartyManager.Instance;
            if (partyManager == null)
            {
                Debug.LogError("PartyManager not found! Cannot attack party.");
                return;
            }
            
            Debug.Log($"PartyManager found with {partyManager.PartySize} characters");
            
            var aliveCharacters = partyManager.GetAliveCharacters();
            if (aliveCharacters.Count == 0)
            {
                Debug.LogWarning("No alive characters to attack!");
                return;
            }
            
            Debug.Log($"Found {aliveCharacters.Count} alive characters");
            
            // 랜덤 타겟
            var target = aliveCharacters[Random.Range(0, aliveCharacters.Count)];
            
            int hitCount = currentIntent.HitCount;
            for (int i = 0; i < hitCount; i++)
            {
                int damage = currentIntent.Damage;
                target.Stats.TakeDamage(damage);
                Debug.Log($">>> {stats.MonsterName} hits {target.Stats.CharacterName} for {damage} damage! (HP: {target.Stats.CurrentHP}/{target.Stats.MaxHP})");
            }
        }
        
        /// <summary>
        /// 데미지 받기
        /// </summary>
        public void TakeDamage(int damage, bool ignoreDefense = false)
        {
            if (!IsAlive) return;
            
            if (ignoreDefense)
            {
                stats.CurrentHP = Mathf.Max(0, stats.CurrentHP - damage);
                Debug.Log($"{stats.MonsterName} took {damage} damage (defense ignored)! HP: {stats.CurrentHP}/{stats.MaxHP}");
            }
            else
            {
                stats.TakeDamage(damage);
            }
            
            // 사망 체크
            if (!IsAlive)
            {
                OnDeath();
            }
        }
        
        /// <summary>
        /// 사망 처리
        /// </summary>
        private void OnDeath()
        {
            Debug.Log($"{stats.MonsterName} defeated!");
            
            // 승리 처리는 CombatManager에서
            var combatManager = CombatManager.Instance;
            if (combatManager != null)
            {
                combatManager.OnMonsterDefeated(this);
            }
        }
    }
}
