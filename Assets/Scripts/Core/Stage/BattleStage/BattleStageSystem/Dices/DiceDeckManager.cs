using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 플레이어 소유 주사위 덱의 단일 출처 (ArtifactManager/PotionManager와 대칭).
    /// 기본 덱 = 캐릭터당 표준 주사위 2개(모집 시 +2). 교체/효과 부여는 Phase 2~3.
    /// 저장은 이번 범위 밖 — 이어하기 시 파티 인원 기준 기본 덱으로 복귀.
    /// </summary>
    public class DiceDeckManager : MonoBehaviour
    {
        public static DiceDeckManager Instance { get; private set; }

        [Header("덱 구성")]
        [Tooltip("시드용 표준 주사위 (면 1~6, 효과 없음)")]
        [SerializeField] private DieDefinitionSO standardDie;
        [Tooltip("보상 획득용 특수 주사위 풀 (Phase 2에서 사용)")]
        [SerializeField] private List<DieDefinitionSO> specialPool = new List<DieDefinitionSO>();
        [Tooltip("캐릭터 1명당 시드되는 표준 주사위 수")]
        [SerializeField] private int diePerCharacter = 2;

        private readonly List<DieInstance> deck = new List<DieInstance>();
        public IReadOnlyList<DieInstance> Deck => deck;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public static DiceDeckManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            Instance = FindAnyObjectByType<DiceDeckManager>(FindObjectsInactive.Include);
            if (Instance == null)
                Debug.LogWarning("[DiceDeckManager] 씬에 인스턴스가 없습니다. 씬에 배치하고 standardDie를 배선해주세요.");
            return Instance;
        }

        private void Start()
        {
            SyncDeckToParty();
            var pm = PartyManager.Instance;
            if (pm != null) pm.OnPartyChanged += HandlePartyChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var pm = PartyManager.Instance;
            if (pm != null) pm.OnPartyChanged -= HandlePartyChanged;
        }

        private void HandlePartyChanged(int partySize) => SyncDeckToParty();

        /// <summary>덱이 파티 인원×diePerCharacter 만큼 되도록 표준 주사위를 채운다(부족분만 추가 — 특수/부여분 보존).</summary>
        public void SyncDeckToParty()
        {
            int target = Mathf.Max(0, (PartyManager.Instance?.PartySize ?? 0) * Mathf.Max(1, diePerCharacter));
            while (deck.Count < target && standardDie != null)
                deck.Add(new DieInstance(standardDie));
            if (standardDie == null && deck.Count < target)
                Debug.LogWarning("[DiceDeckManager] standardDie 미배선 — 덱을 채울 수 없습니다.");
        }

        // ── 보상 드로우 ────────────────────────────────────────

        /// <summary>등급 가중 무작위 드로우 — 일반 6 : 고급 3 : 희귀 1 (2026-08-28).</summary>
        public DieDefinitionSO DrawRandomSpecial()
        {
            if (specialPool == null || specialPool.Count == 0) return null;

            int total = 0;
            foreach (var die in specialPool)
                if (die != null) total += RarityWeight(die.Rarity);
            if (total <= 0) return null;

            int pick = Random.Range(0, total);
            foreach (var die in specialPool)
            {
                if (die == null) continue;
                pick -= RarityWeight(die.Rarity);
                if (pick < 0) return die;
            }
            return null;
        }

        private static int RarityWeight(DieRarity rarity)
            => rarity switch { DieRarity.Advanced => 3, DieRarity.Rare => 1, _ => 6 };

        public void Replace(int index, DieDefinitionSO newBase)
        {
            if (index < 0 || index >= deck.Count || newBase == null) return;
            deck[index].BaseDie = newBase;
            deck[index].AttachedEffect = null;   // 교체 시 붙은 효과 초기화
            deck[index].FaceOverride = null;     // 교체 시 면 변형 초기화
        }

        public void AttachEffect(int index, DieEffect effect)
        {
            if (index < 0 || index >= deck.Count) return;
            deck[index].AttachedEffect = effect;
        }

        // ── 면 조작 (이벤트 노드 — 스펙 2026-07-30 §3.3) ─────────

        /// <summary>무작위 면 count개를 각각 1~6 무작위 값으로 (서로 다른 면 — 교정기 1).</summary>
        public void RandomizeFaces(int index, int count)
        {
            var faces = FacesFor(index);
            if (faces == null) return;
            foreach (int f in PickDistinctFaces(faces.Length, count))
                faces[f] = Random.Range(1, 7);
        }

        /// <summary>무작위 면 count개에 delta (하한 0 — 교정기 2).</summary>
        public void AddToRandomFaces(int index, int count, int delta)
        {
            var faces = FacesFor(index);
            if (faces == null) return;
            foreach (int f in PickDistinctFaces(faces.Length, count))
                faces[f] = Mathf.Max(0, faces[f] + delta);
        }

        /// <summary>무작위 면 1개를 0으로 (인챈트/회복기 대가).</summary>
        public void ZeroRandomFace(int index)
        {
            var faces = FacesFor(index);
            if (faces == null || faces.Length == 0) return;
            faces[Random.Range(0, faces.Length)] = 0;
        }

        private int[] FacesFor(int index)
            => (index >= 0 && index < deck.Count) ? deck[index].EnsureFaceOverride() : null;

        /// <summary>0..total-1에서 서로 다른 인덱스 count개 (count >= total이면 전부).</summary>
        private static List<int> PickDistinctFaces(int total, int count)
        {
            var all = new List<int>(total);
            for (int i = 0; i < total; i++) all.Add(i);
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            if (count < all.Count) all.RemoveRange(count, all.Count - count);
            return all;
        }
    }
}
