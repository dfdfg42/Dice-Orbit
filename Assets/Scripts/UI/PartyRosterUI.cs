using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 우측 상단 파티 목록 UI (초상화/이름/레벨/체력)
    /// 인스펙터에 연결한 List Root / Entry Prefab 기반으로만 동작합니다.
    /// </summary>
    public class PartyRosterUI : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static PartyRosterUI Instance { get; private set; }

        [Header("Bindings")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private PartyRosterEntryUI entryPrefab;

        private readonly Dictionary<Core.Character, PartyRosterEntryUI> entryByCharacter = new Dictionary<Core.Character, PartyRosterEntryUI>();
        private readonly List<Core.Character> orderedCharacters = new List<Core.Character>();
        private bool missingBindingLogged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            TryAutoBind();
        }

        private void Update()
        {
            SyncEntries();
            RefreshEntries();
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;

            var existing = FindFirstObjectByType<PartyRosterUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            var go = new GameObject("PartyRosterUI");
            go.AddComponent<PartyRosterUI>();
        }

        private bool HasBindings()
        {
            return listRoot != null && entryPrefab != null;
        }

        private void TryAutoBind()
        {
            if (listRoot == null)
            {
                var list = transform.Find("ListRoot");
                if (list != null)
                {
                    listRoot = list;
                }
            }
        }

        private void SyncEntries()
        {
            if (!HasBindings())
            {
                if (!missingBindingLogged)
                {
                    Debug.LogWarning("[PartyRosterUI] List Root 또는 Entry Prefab 참조가 없습니다. 인스펙터에서 연결하세요.", this);
                    missingBindingLogged = true;
                }

                ClearAllEntries();
                return;
            }

            missingBindingLogged = false;

            var partyManager = Core.PartyManager.Instance;
            if (partyManager == null)
            {
                ClearAllEntries();
                return;
            }

            orderedCharacters.Clear();
            orderedCharacters.AddRange(partyManager.Party);

            // Remove stale
            var toRemove = new List<Core.Character>();
            foreach (var kv in entryByCharacter)
            {
                if (kv.Key == null || !orderedCharacters.Contains(kv.Key))
                {
                    toRemove.Add(kv.Key);
                }
            }

            foreach (var character in toRemove)
            {
                RemoveEntry(character);
            }

            // Ensure existing/new
            for (int i = 0; i < orderedCharacters.Count; i++)
            {
                var character = orderedCharacters[i];
                if (character == null) continue;

                if (!entryByCharacter.TryGetValue(character, out var entry))
                {
                    entry = CreateEntry(character);
                    if (entry == null)
                    {
                        continue;
                    }

                    entryByCharacter[character] = entry;
                }

                entry.Root.SetSiblingIndex(i);
            }
        }

        private void RefreshEntries()
        {
            foreach (var kv in entryByCharacter)
            {
                var character = kv.Key;
                var entry = kv.Value;
                if (character == null || entry == null) continue;

                var stats = character.Stats;
                if (stats == null) continue;

                string displayName = string.IsNullOrWhiteSpace(stats.CharacterName) ? character.name : stats.CharacterName;
                entry.NameLevelText.text = $"{displayName}  Lv.{stats.Level}";

                float maxHp = Mathf.Max(1, stats.MaxHP);
                float currentHp = Mathf.Clamp(stats.CurrentHP, 0, stats.MaxHP);
                if (entry.HpSlider != null)
                {
                    entry.HpSlider.maxValue = maxHp;
                    entry.HpSlider.value = currentHp;
                }

                entry.HpText.text = $"{stats.CurrentHP}/{stats.MaxHP}";

                // Animator 기반 스프라이트를 사용하는 캐릭터는 현재 SpriteRenderer 프레임을 그대로 미러링합니다.
                Sprite portrait = null;
                if (entry.LivePortraitSource != null)
                {
                    portrait = entry.LivePortraitSource.sprite;
                }

                if (portrait == null)
                {
                    portrait = stats.SourcePreset != null && stats.SourcePreset.Portrait != null
                        ? stats.SourcePreset.Portrait
                        : stats.CharacterSprite;
                }

                if (entry.PortraitImage != null)
                {
                    entry.PortraitImage.sprite = portrait;
                }

                float alpha = character.IsAlive ? 1f : 0.45f;
                entry.SetAlpha(alpha);
            }
        }

        private PartyRosterEntryUI CreateEntry(Core.Character character)
        {
            var entry = Instantiate(entryPrefab, listRoot);
            entry.name = $"Entry_{character.name}";
            entry.Bind(character);

            var hoverProxy = entry.GetComponent<PartyRosterEntryHoverProxy>();
            if (hoverProxy == null)
            {
                hoverProxy = entry.gameObject.AddComponent<PartyRosterEntryHoverProxy>();
            }

            hoverProxy.Bind(character);

            // 캐릭터 애니메이터가 구동 중인 실제 SpriteRenderer를 찾아 초상화에 반영합니다.
            var livePortraitSource = character.GetComponentInChildren<SpriteRenderer>();

            entry.LivePortraitSource = livePortraitSource;
            return entry;
        }

        private void RemoveEntry(Core.Character character)
        {
            if (!entryByCharacter.TryGetValue(character, out var entry)) return;

            if (entry.Root != null)
            {
                Destroy(entry.Root.gameObject);
            }

            entryByCharacter.Remove(character);
        }

        private void ClearAllEntries()
        {
            foreach (var kv in entryByCharacter)
            {
                if (kv.Value?.Root != null)
                {
                    Destroy(kv.Value.Root.gameObject);
                }
            }
            entryByCharacter.Clear();
        }

    }

    public class PartyRosterEntryHoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Core.Character character;

        public void Bind(Core.Character target)
        {
            character = target;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (character == null) return;

            HoverTooltipUI.EnsureInstance();
            HoverTooltipUI.Instance?.ShowPinned(character.GetHoverTooltipData());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverTooltipUI.Instance?.HidePinned();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (character == null) return;
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            // 월드 클릭 선택과 동일 흐름(재클릭 토글 포함)
            character.OnSelected();
        }

        private void OnDisable()
        {
            HoverTooltipUI.Instance?.HidePinned();
        }
    }
}
