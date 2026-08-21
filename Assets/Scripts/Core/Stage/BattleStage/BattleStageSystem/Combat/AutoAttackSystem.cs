using System.Collections;
using System.Collections.Generic;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 자동 기본공격. 캐릭터는 매 턴 반드시 한 번 자기 구역의 몬스터를 때린다 —
    /// 주사위 눈·액티브 사용 여부와 무관하며, 이것이 파티 피해량의 바닥을 보장한다(스펙 §3, 불변식 1).
    ///
    /// 발동 지점은 둘이지만 실제 처리는 ResolveRoutine 한 곳이다:
    ///  ① 이동 직후 (MoveThenAttackRoutine — 플레이어가 결과를 보고 다음 캐릭터를 정할 수 있도록)
    ///  ② 턴 종료 시 아직 안 때린 캐릭터 일괄 (ResolveRemainingRoutine — 주사위를 안 받은 캐릭터도 바닥은 보장)
    /// 중복 발동은 턴별 기록으로 막는다.
    /// </summary>
    public class AutoAttackSystem : MonoBehaviour
    {
        public static AutoAttackSystem Instance { get; private set; }

        [Header("연출")]
        [Tooltip("공격 사이 간격(초). 여러 캐릭터가 연달아 때릴 때 읽히도록.")]
        [SerializeField] private float attackInterval = 0.25f;
        [Tooltip("피해 팝업·로그에 표시할 이름")]
        [SerializeField] private string attackName = "기본 공격";

        private readonly HashSet<Character> _resolvedThisTurn = new HashSet<Character>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static AutoAttackSystem EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<AutoAttackSystem>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("[AutoAttackSystem]").AddComponent<AutoAttackSystem>();
        }

        /// <summary>플레이어 턴 시작 시 호출 — 이번 턴 발동 기록을 비운다.</summary>
        public void ResetTurn() => _resolvedThisTurn.Clear();

        /// <summary>이동을 끝까지 수행한 뒤 도착 구역의 몬스터를 자동 공격한다.</summary>
        public IEnumerator MoveThenAttackRoutine(Character character, int steps)
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit == null)
            {
                Debug.LogError("[AutoAttack] OrbitManager가 없어 이동을 처리할 수 없다.");
                yield break;
            }

            yield return orbit.MoveRoutine(character, steps);
            yield return ResolveRoutine(character);
        }

        /// <summary>아직 공격하지 않은 생존 캐릭터 전원을 순서대로 공격시킨다.</summary>
        public IEnumerator ResolveRemainingRoutine()
        {
            var party = PartyManager.Instance != null ? PartyManager.Instance.Party : null;
            if (party == null) yield break;

            // 순회 중 파티 목록이 바뀔 수 있으므로 복사본으로 돈다.
            var snapshot = new List<Character>(party);
            foreach (var character in snapshot)
                yield return ResolveRoutine(character);
        }

        /// <summary>한 캐릭터의 자동 공격 1회. 이번 턴에 이미 때렸으면 아무 일도 하지 않는다.</summary>
        public IEnumerator ResolveRoutine(Character character)
        {
            if (character == null || !character.IsAlive) yield break;
            if (_resolvedThisTurn.Contains(character)) yield break;
            _resolvedThisTurn.Add(character);

            var targets = CollectTargets(character);
            if (targets.Count == 0) yield break;   // 사거리 안에 생존 몬스터 없음 — 정상 상태

            character.OnSkillExecutionStarted();

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                var context = new AttackContext(character, target, attackName, character.Stats.Attack);
                CombatPipeline.Instance?.Process(context);
            }

            if (attackInterval > 0f) yield return new WaitForSeconds(attackInterval);
        }

        /// <summary>
        /// 이 캐릭터가 이번 공격으로 때릴 대상들.
        /// 지금은 자기가 선 구역의 소유 몬스터 하나뿐이다 —
        /// 사거리를 넓히는 패시브(마법사 원거리 등)는 이 메서드 한 곳만 확장하면 된다.
        /// </summary>
        private List<Unit> CollectTargets(Character character)
        {
            var result = new List<Unit>();

            var zones = CombatZoneManager.Instance;
            if (zones == null)
            {
                Debug.LogError("[AutoAttack] CombatZoneManager가 없어 공격 대상을 정할 수 없다.");
                return result;
            }

            int zone = zones.GetZoneOf(character);
            if (zone < 0) return result;   // 아직 타일에 배치되지 않음

            var owner = zones.GetOwner(zone);
            if (owner != null) result.Add(owner);   // 중립지대(주인 없음)면 이번 턴 공격 없음

            return result;
        }
    }
}
