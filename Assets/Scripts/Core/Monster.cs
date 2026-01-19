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
        [SerializeField] private MonsterStats stats;
        
        [Header("Attack Patterns")]
        [SerializeField] private List<AttackIntent> attackPatterns = new List<AttackIntent>();
        // 현재 의도
        private AttackIntent currentIntent;
        
        // 타겟팅 공격 시 선택된 타겟 (미리보기와 실제 공격에서 동일하게 사용)
        private Character targetedCharacter;
        
        // 범위 공격 시 선택된 타일들 (미리보기와 실제 공격에서 동일하게 사용)
        private Data.TileData[] targetedTiles;
        
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
                
                // 공격 미리보기 표시
                ShowAttackPreview();
            }
        }
        
        /// <summary>
        /// 공격 미리보기 표시
        /// </summary>
        public void ShowAttackPreview()
        {
            if (currentIntent == null) return;
            
            var indicator = FindObjectOfType<UI.AttackIndicator>();
            if (indicator == null)
            {
                Debug.LogWarning("AttackIndicator not found in scene!");
                return;
            }
            
            switch (currentIntent.Type)
            {
                case IntentType.Attack:
                    // 타겟팅 공격 - 랜덤 캐릭터에게 빨간 줄
                    ShowTargetedAttackPreview(indicator);
                    break;
                    
                case IntentType.Multi:
                    // 범위 공격 - 모든 캐릭터 타일 빨간색
                    ShowAreaAttackPreview(indicator);
                    break;
            }
        }
        
        /// <summary>
        /// 타겟팅 공격 미리보기
        /// </summary>
        private void ShowTargetedAttackPreview(UI.AttackIndicator indicator)
        {
            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;
            
            var aliveCharacters = partyManager.GetAliveCharacters();
            if (aliveCharacters.Count == 0) return;
            
            // 타겟 선택 및 저장 (실제 공격에서도 이 타겟 사용)
            targetedCharacter = aliveCharacters[Random.Range(0, aliveCharacters.Count)];
            
            // Transform 전달 (실시간 업데이트용)
            indicator.ShowTargetedAttack(transform, targetedCharacter.transform);
            Debug.Log($"[Monster] Targeting {targetedCharacter.Stats.CharacterName} for attack");
        }
        
        /// <summary>
        /// 단일 공격 실행
        /// </summary>
        private void PerformSingleAttack()
        {
            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;
            
            // 미리보기에서 저장된 타겟 사용
            Character target = targetedCharacter;
            
            // 타겟이 없거나 죽었으면 새로 선택
            if (target == null || !target.IsAlive)
            {
                var aliveCharacters = partyManager.GetAliveCharacters();
                if (aliveCharacters.Count == 0)
                {
                    Debug.Log("No alive characters to attack!");
                    return;
                }
                target = aliveCharacters[Random.Range(0, aliveCharacters.Count)];
                Debug.LogWarning($"[Monster] Original target unavailable, selecting new target: {target.Stats.CharacterName}");
            }
            
            // 공격 실행
            int damage = currentIntent.Damage;
            target.Stats.TakeDamage(damage);
            
            Debug.Log($"{stats.MonsterName} attacks {target.Stats.CharacterName} for {damage} damage!");
            
            // 타겟 초기화
            targetedCharacter = null;
        }
        
        /// <summary>
        /// 범위 공격 미리보기
        /// </summary>
        private void ShowAreaAttackPreview(UI.AttackIndicator indicator)
        {
            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;
            
            var aliveCharacters = partyManager.GetAliveCharacters();
            if (aliveCharacters.Count == 0) return;
            
            // 모든 캐릭터의 타일 수집 및 저장
            var tiles = new System.Collections.Generic.List<Data.TileData>();
            foreach (var character in aliveCharacters)
            {
                if (character.CurrentTile != null)
                {
                    tiles.Add(character.CurrentTile);
                }
            }
            
            targetedTiles = tiles.ToArray();
            indicator.ShowAreaAttack(targetedTiles);
            
            Debug.Log($"[Monster] Targeting {targetedTiles.Length} tiles for area attack");
        }
        
        /// <summary>
        /// 공격 미리보기 숨기기
        /// </summary>
        public void HideAttackPreview()
        {
            var indicator = FindObjectOfType<UI.AttackIndicator>();
            if (indicator != null)
            {
                indicator.Hide();
            }
            
            // 공격 후 타겟 초기화는 PerformSingleAttack에서 수행
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
            
            if (currentIntent.Type == IntentType.Attack)
            {
                // 단일 타겟팅 공격 - 저장된 타겟 사용
                PerformSingleAttack();
            }
            else if (currentIntent.Type == IntentType.Multi)
            {
                // 범위 공격 - 모든 캐릭터
                PerformMultiAttack();
            }
        }
        
        /// <summary>
        /// 범위 공격 실행 (모든 캐릭터)
        /// </summary>
        private void PerformMultiAttack()
        {
            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;
            
            // 저장된 타일이 없으면 현재 모든 캐릭터 공격 (fallback)
            if (targetedTiles == null || targetedTiles.Length == 0)
            {
                Debug.LogWarning("[Monster] No targeted tiles saved, attacking all characters");
                var allCharacters = partyManager.GetAliveCharacters();
                foreach (var character in allCharacters)
                {
                    int damage = currentIntent.Damage;
                    character.Stats.TakeDamage(damage);
                    Debug.Log($"{stats.MonsterName} hits {character.Stats.CharacterName} for {damage} damage!");
                }
                return;
            }
            
            // 저장된 타일에 있는 캐릭터만 공격
            var aliveCharacters = partyManager.GetAliveCharacters();
            int hitCount = 0;
            
            foreach (var tile in targetedTiles)
            {
                // 이 타일에 있는 캐릭터 찾기
                foreach (var character in aliveCharacters)
                {
                    if (character.CurrentTile == tile)
                    {
                        int damage = currentIntent.Damage;
                        character.Stats.TakeDamage(damage);
                        Debug.Log($"{stats.MonsterName} hits {character.Stats.CharacterName} on tile {tile.TileIndex} for {damage} damage!");
                        hitCount++;
                    }
                }
            }
            
            if (hitCount == 0)
            {
                Debug.Log($"{stats.MonsterName}'s area attack missed! All targets moved away.");
            }
            
            // 타일 초기화
            targetedTiles = null;
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
