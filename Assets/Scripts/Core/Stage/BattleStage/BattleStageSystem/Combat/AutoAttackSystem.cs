using System.Collections;
using System.Collections.Generic;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Visuals;
using UnityEngine;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 자동 공격. 캐릭터는 매 턴 반드시 한 번 공격한다 — 이것이 파티 피해량의 바닥을 보장한다(불변식 1).
    /// 배정한 주사위가 액티브의 조건을 만족하면 그 한 번이 강화 공격으로 나간다(대체이지 추가가 아니다).
    /// 조건은 공격을 막지 못하고 더 좋게 만들기만 하므로 불변식 4(게이트는 보너스만 연다)와도 맞는다.
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
        [Tooltip("캐릭터의 공격이 끝난 뒤 다음 캐릭터로 넘어가기까지의 간격(초).")]
        [SerializeField] private float attackInterval = 0.25f;
        [Tooltip("한 대상을 때린 뒤 다음 대상까지의 간격(초). 발사체 비행 시간에 이 값이 더해진다 — 피해 팝업이 겹치지 않도록.")]
        [SerializeField] private float hitGap = 0.15f;
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
        public IEnumerator MoveThenAttackRoutine(Character character, int diceValue)
        {
            var orbit = GameManager.Instance != null ? GameManager.Instance.GetOrbitManager() : null;
            if (orbit == null)
            {
                Debug.LogError("[AutoAttack] OrbitManager가 없어 이동을 처리할 수 없다.");
                yield break;
            }

            // 지나갈 구역은 이동 전에 기록한다 — 경로형 강화 공격(전사 돌파)이 읽는다.
            var passedZones = CollectPassedZones(character, diceValue);

            yield return orbit.MoveRoutine(character, diceValue);
            yield return ResolveRoutine(character, diceValue, passedZones);
        }

        /// <summary>이번 이동에서 지나갈 구역 번호들(출발 구역 포함, 중복 없이 순서대로).</summary>
        private static List<int> CollectPassedZones(Character character, int diceValue)
        {
            var result = new List<int>();
            var zones = Zones.CombatZoneManager.Instance;
            if (zones == null || character == null || character.CurrentTile == null) return result;

            // MoveRoutine과 같은 유효 걸음 수를 쓴다 — 버프/디버프로 실제 이동이 달라지므로.
            int steps = Mathf.Max(diceValue + character.Stats.MoveBuff - character.Stats.MoveDebuff, 0);

            var tile = character.CurrentTile;
            int startZone = zones.GetZoneOfTile(tile);
            if (startZone >= 0) result.Add(startZone);

            for (int i = 0; i < steps; i++)
            {
                if (tile.NextTile == null) break;
                tile = tile.NextTile;
                int zone = zones.GetZoneOfTile(tile);
                if (zone >= 0 && !result.Contains(zone)) result.Add(zone);
            }
            return result;
        }

        /// <summary>아직 공격하지 않은 생존 캐릭터 전원을 순서대로 공격시킨다.</summary>
        public IEnumerator ResolveRemainingRoutine()
        {
            var party = PartyManager.Instance != null ? PartyManager.Instance.Party : null;
            if (party == null) yield break;

            // 순회 중 파티 목록이 바뀔 수 있으므로 복사본으로 돈다.
            // 주사위를 받지 못한 캐릭터라 조건을 만족할 눈이 없다 — 기본공격만 나간다.
            var snapshot = new List<Character>(party);
            foreach (var character in snapshot)
                yield return ResolveRoutine(character, 0, null);
        }

        /// <summary>
        /// 한 캐릭터의 공격 1회. 이번 턴에 이미 때렸으면 아무 일도 하지 않는다.
        /// diceValue가 액티브 조건을 만족하면 기본공격 대신 그 강화 공격이 나간다.
        /// </summary>
        public IEnumerator ResolveRoutine(Character character, int diceValue, IReadOnlyList<int> passedZones)
        {
            if (character == null || !character.IsAlive) yield break;
            if (_resolvedThisTurn.Contains(character)) yield break;
            _resolvedThisTurn.Add(character);

            var empowered = FindEmpoweredAttack(character, diceValue);
            var skill = empowered != null ? empowered.RuntimeInstance : null;

            var targets = skill != null
                ? skill.ResolveTargets(character, passedZones)
                : CollectTargets(character);
            if (targets == null || targets.Count == 0) yield break;

            character.OnSkillExecutionStarted();

            // 무기 연출은 강화 공격이든 기본공격이든 같은 발사체를 쓴다.
            var weapon = skill != null ? skill : FindProjectileSource(character);
            if (skill != null) skill.PlayCast(character);

            // 대상이 여럿이어도 한 번에 몰아 때리지 않는다 — 한 대상씩 때리고 기다려
            // 피해 팝업과 패시브 발동이 순서대로 하나씩 읽히게 한다.
            float perHit = ResolveHitDelay(weapon);
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive) continue;

                if (skill != null) skill.ApplyToTarget(character, empowered, target, diceValue);
                else               LaunchBasicHit(character, target, weapon);

                if (perHit > 0f) yield return new WaitForSeconds(perHit);
            }

            if (attackInterval > 0f) yield return new WaitForSeconds(attackInterval);
        }

        /// <summary>기본공격 1타. 발사체가 있으면 날아가 도착할 때 피해가 들어간다.</summary>
        private void LaunchBasicHit(Character character, Unit target, Data.Skills.CharacterActiveSkill weapon)
        {
            var context = new AttackContext(character, target, attackName, character.Stats.Attack);

            if (weapon != null && weapon.ProjectilePrefab != null)
            {
                Vector3 from = character.transform.position + Vector3.up * 0.5f;
                Vector3 to   = target.transform.position + Vector3.up * 0.5f;
                ProjectileService.Launch(weapon.ProjectilePrefab, from, to,
                    weapon.ProjectileDuration, weapon.ProjectileArcHeight,
                    () => CombatPipeline.Instance?.Process(context));
            }
            else
            {
                CombatPipeline.Instance?.Process(context);
            }
        }

        /// <summary>한 타 사이의 대기 시간. 발사체가 도착한 뒤 간격이 생기도록 비행 시간을 더한다.</summary>
        private float ResolveHitDelay(Data.Skills.CharacterActiveSkill weapon)
        {
            float flight = weapon != null && weapon.ProjectilePrefab != null ? weapon.ProjectileDuration : 0f;
            return flight + Mathf.Max(0f, hitGap);
        }

        /// <summary>이 캐릭터의 무기 연출(발사체)을 들고 있는 액티브. 없으면 null(즉시 피해).</summary>
        private static Data.Skills.CharacterActiveSkill FindProjectileSource(Character character)
        {
            var abilities = character.Stats != null ? character.Stats.ActiveAbilities : null;
            if (abilities == null) return null;

            foreach (var slot in abilities)
            {
                var skill = slot != null ? (slot.RuntimeInstance ?? slot.BaseSkill) : null;
                if (skill != null && skill.ProjectilePrefab != null) return skill;
            }
            return null;
        }

        /// <summary>배정한 주사위가 조건을 만족하는 액티브. 없으면 null(기본공격으로 나간다).</summary>
        private static Data.Skills.ActiveSkillSlot FindEmpoweredAttack(Character character, int diceValue)
        {
            if (diceValue <= 0) return null;
            var abilities = character.Stats != null ? character.Stats.ActiveAbilities : null;
            if (abilities == null) return null;

            foreach (var slot in abilities)
                if (slot != null && slot.RuntimeInstance != null && slot.CanUse(diceValue)) return slot;

            return null;
        }

        /// <summary>
        /// 이 캐릭터가 이번 공격으로 때릴 대상들.
        /// 기본은 자기가 선 구역의 주인 하나이며, 사거리 패시브(IZoneReachProvider)가 있으면
        /// 그만큼 옆 구역까지 탐색한다 — 탐색 범위만 넓어지고 표적 수는 여전히 하나다.
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

            var owner = zones.FindNearestOwner(zone, ResolveZoneReach(character));
            if (owner != null) result.Add(owner);   // 사거리 안에 주인이 없으면 이번 턴 공격 없음

            return result;
        }

        /// <summary>캐릭터의 패시브가 제공하는 구역 사거리 중 가장 큰 값. 없으면 0(자기 구역만).</summary>
        private static int ResolveZoneReach(Character character)
        {
            var passives = character?.Stats?.PassiveInstances;
            if (passives == null) return 0;

            int reach = 0;
            foreach (var passive in passives)
                if (passive is Data.Passives.IZoneReachProvider provider)
                    reach = Mathf.Max(reach, provider.ExtraZoneReach);

            return reach;
        }
    }
}
