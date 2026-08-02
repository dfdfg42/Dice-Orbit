using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core.Run.Save;
using DiceOrbit.Data.Modifiers;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 파티 관리자 (싱글톤)
    /// 최대 4명의 캐릭터 관리
    /// </summary>
    public class PartyManager : MonoBehaviour, IRunSaveParticipant
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
        /// 파티 전원 제거 + 오브젝트 파괴 — 세이브 복원이 이전 런의 잔여 파티 위에 스폰하지 않게 한다.
        /// </summary>
        public void ClearAll()
        {
            for (int i = party.Count - 1; i >= 0; i--)
            {
                var character = party[i];
                if (character != null) Destroy(character.gameObject);
            }
            party.Clear();
            selectedCharacter = null;
            OnPartyChanged?.Invoke(party.Count);
        }

        // ── 세이브 참가자 ──────────────────────────────────────

        public void Capture(RunSaveData data)
        {
            data.Party.Clear();
            foreach (var character in party)
            {
                if (character == null || character.Stats == null) continue;

                var save = new CharacterSaveData
                {
                    PresetId = character.Stats.SourcePreset != null
                        ? character.Stats.SourcePreset.SaveId
                        : null,
                    CurrentHp = character.Stats.CurrentHP,
                    MaxHp = character.Stats.MaxHP,
                    RevivalStock = character.Stats.RevivalStock,
                };

                var mods = character.Stats.Modifiers != null ? character.Stats.Modifiers.Modifiers : null;
                if (mods != null)
                    foreach (var mod in mods)
                        if (mod != null)
                            save.Modifiers.Add(new ModifierSaveData { Id = mod.GetType().Name });

                data.Party.Add(save);
            }
        }

        public void Validate(RunSaveData data, RunRestoreContext ctx)
        {
            if (ctx.Spawner == null)
                ctx.Report.Fail("CharacterSpawner를 찾지 못해 파티를 복원할 수 없습니다.");

            if (data.Party.Count > maxPartySize)
                ctx.Report.Fail($"세이브의 파티 인원이 {data.Party.Count}명으로 최대 {maxPartySize}명을 넘습니다 — 손상된 세이브입니다.");

            foreach (var save in data.Party)
            {
                if (string.IsNullOrEmpty(save.PresetId))
                {
                    ctx.Report.Fail("파티 구성원의 PresetId가 비어 있습니다.");
                }
                else if (ctx.FindPreset(save.PresetId) == null)
                {
                    ctx.Report.Fail($"프리셋 '{save.PresetId}'를 카탈로그에서 찾지 못했습니다.");
                }

                // Apply가 CurrentHP를 Clamp(_, 1, MaxHp)로 넣으므로 MaxHp가 1 미만이면 범위가 뒤집힌다.
                if (save.MaxHp < 1)
                    ctx.Report.Fail($"'{save.PresetId}'의 MaxHp가 {save.MaxHp}입니다 — 손상된 세이브입니다.");

                foreach (var mod in save.Modifiers)
                    if (!ModifierRegistry.Exists(mod.Id))
                        ctx.Report.Fail($"모디파이어 '{mod.Id}'가 레지스트리에 없습니다.");
            }
        }

        public void Apply(RunSaveData data, RunRestoreContext ctx)
        {
            ClearAll();

            for (int i = 0; i < data.Party.Count; i++)
            {
                var save = data.Party[i];

                // CharacterSpawner.Spawn이 PartyManager.AddCharacter를 자동 호출하므로 별도 등록은 없다.
                var character = ctx.Spawner.Spawn(ctx.FindPreset(save.PresetId), i, data.Party.Count);
                if (character == null || character.Stats == null)
                {
                    // 조용히 건너뛰면 파티 0명짜리 반쪽 복원이 성공으로 보고되고, 그대로 세이브를 덮어쓴다.
                    // Apply 단계 실패는 롤백되지 않지만 ContinueGameFlow의 !report.Success 분기가
                    // 새 게임 폴백 + 세이브 삭제로 처리한다 — 조용한 반쪽 복원보다 정직한 실패가 낫다.
                    ctx.Report.Fail($"파티 복원 중 '{save.PresetId}' 스폰에 실패했습니다.");
                    continue;
                }

                character.Stats.MaxHP = save.MaxHp;
                character.Stats.CurrentHP = Mathf.Clamp(save.CurrentHp, 1, save.MaxHp);
                character.Stats.RevivalStock = save.RevivalStock;

                foreach (var mod in save.Modifiers)
                {
                    // 캐릭터마다 독립 인스턴스가 필요하므로 매번 새로 만든다.
                    var instance = ModifierRegistry.Create(mod.Id);
                    if (instance != null && character.Stats.Modifiers != null)
                        character.Stats.Modifiers.Add(instance);
                }
            }
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
