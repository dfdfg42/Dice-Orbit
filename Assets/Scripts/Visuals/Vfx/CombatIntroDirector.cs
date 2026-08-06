using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 전투 시작 연출 (스펙 2026-07-31). 타일 시계방향 낙하 → 캐릭터 팝인 → 몬스터 소환.
    /// CombatManager.StartEncounter가 yield return Play(...)로 소비. 연출 동안 playerTurnActive=false라 입력 차단됨.
    /// 타일/캐릭터/몬스터의 원래 위치·스케일을 캐시했다 정확히 복원 (캐릭터 배치가 tile.Position 참조).
    /// </summary>
    public class CombatIntroDirector : MonoBehaviour
    {
        public static CombatIntroDirector Instance { get; private set; }

        [Header("타일 낙하")]
        [SerializeField] private float tileDropHeight = 14f;
        [SerializeField] private float tileDropDuration = 0.5f;
        [SerializeField] private float tileStagger = 0.08f;
        [Tooltip("화면상 시계방향이 되도록 순회 방향 (플레이로 맞춤)")]
        [SerializeField] private bool clockwise = true;

        [Header("페이즈 간 딜레이")]
        [SerializeField] private float afterTilesDelay = 0.35f;   // 타일 낙하 → 캐릭터
        [SerializeField] private float afterCharsDelay = 0.35f;   // 캐릭터 → 몬스터 소환

        [Header("캐릭터 팝인")]
        [SerializeField] private float charPopDuration = 0.3f;
        [SerializeField] private float charStagger = 0.2f;

        [Header("몬스터 소환")]
        [SerializeField] private float monsterPopDuration = 0.3f;
        [SerializeField] private float monsterStagger = 0.25f;

        // 연출 시작 시 숨긴 캐릭터 (트랜스폼, 원래 스케일) — 자기 차례에 복원
        private readonly List<KeyValuePair<Transform, Vector3>> _hiddenChars = new List<KeyValuePair<Transform, Vector3>>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static CombatIntroDirector EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<CombatIntroDirector>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("CombatIntroDirector").AddComponent<CombatIntroDirector>();
        }

        /// <summary>3단계 순차 연출. 완료 후 반환 → 호출자가 BroadcastCombatStart/StartCombat 진행.
        /// 각 페이즈는 예외 격리(RunSafe) — 어느 하나가 죽어도 연출은 끝까지 가고, 마지막에
        /// 타일/캐릭터/몬스터를 최종 표시 상태로 강제 복원한다. 인트로가 전투를 절대 막지 않게.</summary>
        public IEnumerator Play(IReadOnlyList<Monster> spawnedMonsters)
        {
            HideCharacters();   // 연출 시작 즉시 캐릭터를 숨겨 타일 낙하 동안 안 보이게
            DiceOrbit.UI.TileAttributeBubbleManager.Instance?.SetIconsHidden(true);   // 타일 낙하 전 속성 아이콘 숨김
            yield return RunSafe(TileDropPhase(), "타일 낙하");
            DiceOrbit.UI.TileAttributeBubbleManager.Instance?.SetIconsHidden(false);  // 타일 착지 후 아이콘 표시
            if (afterTilesDelay > 0f) yield return new WaitForSeconds(afterTilesDelay);
            yield return RunSafe(CharacterPopPhase(), "캐릭터 팝인");
            if (afterCharsDelay > 0f) yield return new WaitForSeconds(afterCharsDelay);
            yield return RunSafe(MonsterSummonPhase(spawnedMonsters), "몬스터 소환");

            RevealAll(spawnedMonsters);   // 안전망: 무슨 일이 있어도 전부 표시 상태로 확정
        }

        /// <summary>코루틴 예외 격리 — inner가 던져도 잡아서 로그만 남기고 조용히 종료(전투는 계속).</summary>
        private IEnumerator RunSafe(IEnumerator inner, string label)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!inner.MoveNext()) yield break;
                    current = inner.Current;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[CombatIntro] {label} 중단(무시하고 전투 진행): {ex}");
                    yield break;
                }
                yield return current;
            }
        }

        /// <summary>캐릭터·몬스터를 최종 스케일로 강제 복원 (페이즈가 중단됐어도 숨긴 채 남지 않게).</summary>
        private void RevealAll(IReadOnlyList<Monster> monsters)
        {
            DiceOrbit.UI.TileAttributeBubbleManager.Instance?.SetIconsHidden(false);   // 안전망: 아이콘 표시 확정

            foreach (var pair in _hiddenChars)
                if (pair.Key != null) pair.Key.localScale = pair.Value;
            _hiddenChars.Clear();

            if (monsters == null) return;
            foreach (var m in monsters)
                if (m != null) m.transform.localScale = m.IntroBaseScale;
        }

        /// <summary>파티 캐릭터의 원래 스케일을 캐시하고 즉시 숨김 (스케일 0).</summary>
        private void HideCharacters()
        {
            _hiddenChars.Clear();
            var party = PartyManager.Instance != null ? PartyManager.Instance.Party : null;
            if (party == null) return;
            foreach (var c in party)
            {
                if (c == null || !c.IsAlive) continue;
                _hiddenChars.Add(new KeyValuePair<Transform, Vector3>(c.transform, c.transform.localScale));
                c.transform.localScale = Vector3.zero;
            }
        }

        // ── Phase 1: 타일 시계방향 낙하 ──────────────────────
        private IEnumerator TileDropPhase()
        {
            var orbit = FindAnyObjectByType<OrbitManager>();
            if (orbit == null || orbit.Tiles == null || orbit.Tiles.Count == 0) yield break;

            var tiles = new List<DiceOrbit.Data.TileData>(orbit.Tiles);
            if (clockwise) tiles.Reverse();

            // 원래 값 캐시 + 시작 위치(위)로 올리고 전부 숨김(스케일 0) — 자기 차례에 나타나며 떨어짐
            int n = tiles.Count;
            var origPos = new Vector3[n];
            var origScale = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var t = tiles[i];
                if (t == null) continue;
                origPos[i] = t.transform.position;
                origScale[i] = t.transform.localScale;
                t.transform.position = origPos[i] + Vector3.up * tileDropHeight;
                t.transform.localScale = Vector3.zero;   // 차례 오기 전엔 숨김
            }

            // 순차 낙하 — try/finally로 타일 원위치 복원을 보장 (중단돼도 타일이 떠 있으면 보드가 깨짐)
            try
            {
                for (int i = 0; i < n; i++)
                {
                    if (tiles[i] == null) continue;
                    tiles[i].transform.localScale = origScale[i];   // 이 타일만 나타남
                    StartCoroutine(DropOne(tiles[i].transform, origPos[i], origScale[i]));
                    if (tileStagger > 0f) yield return new WaitForSeconds(tileStagger);
                }
                yield return new WaitForSeconds(tileDropDuration);   // 마지막 낙하까지 대기
            }
            finally
            {
                // 안전: 전부 정확히 원위치·원스케일로 확정 (tile.Position을 캐릭터 배치가 참조)
                for (int i = 0; i < n; i++)
                {
                    if (tiles[i] == null) continue;
                    tiles[i].transform.position = origPos[i];
                    tiles[i].transform.localScale = origScale[i];
                }
            }
        }

        private IEnumerator DropOne(Transform t, Vector3 targetPos, Vector3 targetScale)
        {
            Vector3 startPos = t.position;
            float elapsed = 0f;
            while (elapsed < tileDropDuration)
            {
                if (t == null) yield break;
                float k = EaseInQuad(elapsed / tileDropDuration);   // 중력 낙하 = 가속(t^2)
                t.position = Vector3.LerpUnclamped(startPos, targetPos, k);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (t != null) { t.position = targetPos; t.localScale = targetScale; }
        }

        // ── Phase 2: 캐릭터 순차 팝인 ────────────────────────
        private IEnumerator CharacterPopPhase()
        {
            if (_hiddenChars.Count == 0) yield break;

            foreach (var pair in _hiddenChars)
            {
                if (pair.Key == null) continue;
                StartCoroutine(PopIn(pair.Key, pair.Value, charPopDuration));   // 캐시한 원래 스케일로 복원
                if (charStagger > 0f) yield return new WaitForSeconds(charStagger);
            }
            yield return new WaitForSeconds(charPopDuration);
            _hiddenChars.Clear();
        }

        // ── Phase 3: 몬스터 순차 소환 ────────────────────────
        private IEnumerator MonsterSummonPhase(IReadOnlyList<Monster> monsters)
        {
            if (monsters == null || monsters.Count == 0) yield break;

            foreach (var m in monsters)
            {
                if (m == null) continue;
                VfxService.PlayOn(VfxTags.Summon, m);
                StartCoroutine(PopIn(m.transform, m.IntroBaseScale, monsterPopDuration));   // 숨길 때 캡처한 원래 스케일로 복원
                if (monsterStagger > 0f) yield return new WaitForSeconds(monsterStagger);
            }
            yield return new WaitForSeconds(monsterPopDuration);
        }

        /// <summary>스케일 0 → target 팝인 (EaseOutBack).</summary>
        private IEnumerator PopIn(Transform t, Vector3 target, float duration)
        {
            if (t == null) yield break;
            t.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                float k = EaseOutBack(elapsed / duration);
                t.localScale = Vector3.LerpUnclamped(Vector3.zero, target, k);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (t != null) t.localScale = target;
        }

        // ── 이징 ──────────────────────────────────────────────
        private static float EaseInQuad(float x) => x * x;   // 가속 (중력 낙하)
        private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = 1.70158f + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
