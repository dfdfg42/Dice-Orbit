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
        
        [Header("AI Pattern")]
        [SerializeField] private Data.MonsterAI.MonsterPattern aiPattern;
        
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
            
            // 패턴 초기화
            if (aiPattern != null)
            {
                aiPattern.Initialize(this);
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
            if (aiPattern != null)
            {
                // AI 패턴에서 다음 스킬 가져오기
                var nextSkill = aiPattern.GetNextSkill(this);
                
                if (nextSkill != null)
                {
                    // 스킬을 AttackIntent로 변환
                    currentIntent = nextSkill.CreateIntent(stats);
                    Debug.Log($"{stats.MonsterName} next intent: {currentIntent}");
                    
                    // 공격 미리보기 표시
                    ShowAttackPreview();
                }
                else
                {
                    Debug.LogWarning($"{stats.MonsterName} has no skill to execute!");
                    currentIntent = null;
                }
            }
            else
            {
                Debug.LogWarning($"{stats.MonsterName}: No AI Pattern assigned!");
            }
        }
        
        /// <summary>
        /// 공격 미리보기 표시
        /// </summary>
        public void ShowAttackPreview()
        {
            if (currentIntent == null) return;
            
            var indicator = Object.FindAnyObjectByType<UI.AttackIndicator>();
            if (indicator == null)
            {
                Debug.LogWarning("AttackIndicator not found in scene!");
                return;
            }
            
            switch (currentIntent.Type)
            {
                case IntentType.Attack:
                    // 타겟팅 타입에 따른 처리
                    ShowAttackPreviewByTargetType(indicator);
                    break;
                    
                case IntentType.Multi:
                    // 레거시: 전부 공격
                    ShowAreaAttackPreview(indicator, true);
                    break;
            }
        }

        /// <summary>
        /// 타겟 타입별 공격 미리보기
        /// </summary>
        private void ShowAttackPreviewByTargetType(UI.AttackIndicator indicator)
        {
            if (currentIntent.TargetType == TargetType.Single)
            {
                ShowTargetedAttackPreview(indicator);
            }
            else if (currentIntent.TargetType == TargetType.All)
            {
                ShowAreaAttackPreview(indicator, true);
            }
            else if (currentIntent.TargetType == TargetType.Area)
            {
                // 특정 타겟을 중심으로 범위 공격
                ShowTargetedAreaAttackPreview(indicator);
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
        /// 타겟 중심 범위 공격 미리보기
        /// </summary>
        private void ShowTargetedAreaAttackPreview(UI.AttackIndicator indicator)
        {
            // 1. 타겟 선정
            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;
            
            var aliveCharacters = partyManager.GetAliveCharacters();
            if (aliveCharacters.Count == 0) return;
            
            targetedCharacter = aliveCharacters[Random.Range(0, aliveCharacters.Count)];
            
            // 2. 타일 범위 계산 (반경 N칸)
            if (targetedCharacter.CurrentTile == null) return;

            var tiles = new HashSet<TileData>();
            tiles.Add(targetedCharacter.CurrentTile); // 중심
            
            // 좌우 확장
            int radius = currentIntent.AreaRadius;
            TileData left = targetedCharacter.CurrentTile.PreviousTile;
            TileData right = targetedCharacter.CurrentTile.NextTile;
            
            for (int i = 0; i < radius; i++)
            {
                if(left != null) { tiles.Add(left); left = left.PreviousTile; }
                if(right != null) { tiles.Add(right); right = right.NextTile; }
            }
            
            // Array 변환
            targetedTiles = new TileData[tiles.Count];
            tiles.CopyTo(targetedTiles);

            // 3. 표시
            indicator.ShowAreaAttack(targetedTiles);
            Debug.Log($"[Monster] Targeting {targetedCharacter.Stats.CharacterName} + Area(R{radius})");
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
            
                // 공격 실행 (Pipeline 위임)
                int damage = currentIntent.Damage;
                var action = new Pipeline.CombatAction($"{stats.MonsterName} Attack", Pipeline.ActionType.Attack, damage);
                action.AddTag("MonsterAttack");
                var context = new Pipeline.CombatContext(this, target, action);
                Pipeline.CombatPipeline.Instance.Process(context);
        }
        
        /// <summary>
        /// 범위 공격 미리보기 (전체 혹은 지정된 타일)
        /// </summary>
        private void ShowAreaAttackPreview(UI.AttackIndicator indicator, bool selectAll = false)
        {
            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;
            
            if (selectAll)
            {
                // 모든 캐릭터의 타일 수집
                var aliveCharacters = partyManager.GetAliveCharacters();
                var tiles = new System.Collections.Generic.List<Data.TileData>();
                foreach (var character in aliveCharacters)
                {
                    if (character.CurrentTile != null)
                    {
                        tiles.Add(character.CurrentTile);
                    }
                }
                targetedTiles = tiles.ToArray();
            }
            
            if (targetedTiles != null)
            {
                indicator.ShowAreaAttack(targetedTiles);
                Debug.Log($"[Monster] Targeting {targetedTiles.Length} tiles for area attack");
            }
        }
        
        /// <summary>
        /// 공격 미리보기 숨기기
        /// </summary>
        public void HideAttackPreview()
        {
            var indicator = Object.FindAnyObjectByType<UI.AttackIndicator>();
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
                    AttackParty();
                    break;
                case IntentType.Multi:
                    // Legacy support
                    PerformAreaAttack();
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
        /// 파티 공격 분기
        /// </summary>
        private void AttackParty()
        {
            Debug.Log($"{stats.MonsterName} executing attack!");
            
            if (currentIntent.TargetType == TargetType.Single)
            {
                PerformSingleAttack();
            }
            else
            {
                // Area or All
                PerformAreaAttack();
            }
        }
        
        /// <summary>
        /// 범위 공격 실행 (저장된 타일 위의 모든 적)
        /// </summary>
        private void PerformAreaAttack()
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
                    var action = new Pipeline.CombatAction($"{stats.MonsterName} Area", Pipeline.ActionType.Attack, damage);
                    action.AddTag("MonsterAttack");
                    var context = new Pipeline.CombatContext(this, character, action);
                    Pipeline.CombatPipeline.Instance.Process(context);
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
                        var action = new Pipeline.CombatAction($"{stats.MonsterName} Area", Pipeline.ActionType.Attack, damage);
                        action.AddTag("MonsterAttack");
                        var context = new Pipeline.CombatContext(this, character, action);
                        Pipeline.CombatPipeline.Instance.Process(context);
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
