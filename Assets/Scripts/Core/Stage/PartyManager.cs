using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 파티 관리자 (싱글톤)
    /// 최대 4명의 캐릭터 관리
    /// </summary>
    public class PartyManager : MonoBehaviour
    {
        public static PartyManager Instance { get; private set; }
        
        [Header("Party Settings")]
        [SerializeField] private int maxPartySize = 4;
        [SerializeField] private List<Character> party = new List<Character>();
        
        [Header("Selection")]
        [SerializeField] private Character selectedCharacter;

        [Header("Turn Flags")]
        private bool teamFirstActionUsed = false;
        
        // Properties
        public List<Character> Party => party;
        public Character SelectedCharacter => selectedCharacter;
        public int PartySize => party.Count;
        public int MaxPartySize => maxPartySize;
        public bool IsPartyFull => party.Count >= maxPartySize;
    public bool TeamFirstActionUsed => teamFirstActionUsed;

        // 파티 인원 변화(추가/제거) 시 외부 시스템이 동기화할 수 있도록 알립니다.
        public event System.Action<int> OnPartyChanged;
        
        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("Multiple PartyManagers detected! Destroying duplicate.");
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // Scene에 있는 모든 Character를 자동으로 파티에 추가
            AutoDetectCharacters();
        }
        
        /// <summary>
        /// Scene의 캐릭터 자동 감지
        /// </summary>
        private void AutoDetectCharacters()
        {
            var characters = Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            
            foreach (var character in characters)
            {
                if (!party.Contains(character))
                {
                    AddCharacter(character);
                }
            }
            
            Debug.Log($"PartyManager: Detected {party.Count} characters in scene");
        }
        
        /// <summary>
        /// 파티에 캐릭터 추가
        /// </summary>
        public bool AddCharacter(Character character)
        {
            if (character == null)
            {
                Debug.LogWarning("Cannot add null character!");
                return false;
            }
            
            if (party.Contains(character))
            {
                Debug.LogWarning($"{character.Stats.CharacterName} is already in the party!");
                return false;
            }
            
            if (IsPartyFull)
            {
                Debug.LogWarning($"Party is full! Max size: {maxPartySize}");
                return false;
            }
            
            party.Add(character);
            Debug.Log($"{character.Stats.CharacterName} joined the party! Party size: {party.Count}/{maxPartySize}");

            OnPartyChanged?.Invoke(party.Count);
            
            return true;
        }
        
        /// <summary>
        /// 파티에서 캐릭터 제거
        /// </summary>
        public bool RemoveCharacter(Character character)
        {
            if (party.Remove(character))
            {
                if (selectedCharacter == character)
                {
                    selectedCharacter = null;
                }
                
                Debug.Log($"{character.Stats.CharacterName} left the party. Party size: {party.Count}/{maxPartySize}");
                OnPartyChanged?.Invoke(party.Count);
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 캐릭터 선택
        /// </summary>
        public void SelectCharacter(Character character)
        {
            if (!party.Contains(character))
            {
                Debug.LogWarning($"{character.Stats.CharacterName} is not in the party!");
                return;
            }
            
            selectedCharacter = character;
            Debug.Log($"Selected character: {character.Stats.CharacterName}");
        }

        public void ResetTeamFirstAction()
        {
            teamFirstActionUsed = false;
        }

        public bool TryConsumeTeamFirstAction()
        {
            if (teamFirstActionUsed) return false;
            teamFirstActionUsed = true;
            return true;
        }
        
        /// <summary>
        /// 선택 해제
        /// </summary>
        public void DeselectCharacter()
        {
            selectedCharacter = null;
        }
        
        /// <summary>
        /// 생존한 캐릭터 목록
        /// </summary>
        public List<Character> GetAliveCharacters()
        {
            return party.Where(c => c.IsAlive).ToList();
        }
        
        /// <summary>
        /// 전멸 확인
        /// </summary>
        public bool IsPartyWiped()
        {
            return party.All(c => !c.IsAlive);
        }

        /// <summary>
        /// 전투에서 리타이어한 파티원 전원 부활 (점감 부활 — 스펙 §4).
        /// 전투 승리 확정 후 GameFlowManager가 호출.
        /// </summary>
        public void ReviveRetiredMembers()
        {
            foreach (var character in party)
            {
                if (character != null && !character.IsAlive)
                    character.Revive();
            }
        }
        
        /// <summary>
        /// 파티원 모두 회복
        /// </summary>
        public void HealAllParty(int amount)
        {
            foreach (var character in party)
            {
                if (character == null) continue;

                if (Core.Pipeline.CombatPipeline.Instance != null)
                {
                    var context = new Core.Pipeline.HealContext(null, character, "Party Heal", amount);
                    context.AddTag("Party");
                    Core.Pipeline.CombatPipeline.Instance.Process(context);
                }
                else
                {
                    character.Stats.Heal(amount);
                }
            }
            
            Debug.Log($"Party healed for {amount} HP");
        }
    }
}
