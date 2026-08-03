using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DiceOrbit.Core.Run;
using DiceOrbit.Data.Monsters;
using DiceOrbit.UI;
using DiceOrbit.UI.Tutorial;

namespace DiceOrbit.Core.Tutorial
{
    /// <summary>
    /// 튜토리얼 고정 데모 셋업. 실제 런 상태(RunManager/세이브/영구덱)를 건드리지 않고
    /// 데모 파티(전사·도적 인접)·약체 몬스터만 스폰하고, 끝나면 ClearAll로 정리한다.
    /// </summary>
    public class TutorialScenario : MonoBehaviour
    {
        public static TutorialScenario Instance { get; private set; }

        [Header("데모 데이터 (Resources/TutorialScenario 프리팹에 배선)")]
        [SerializeField] private CharacterPreset warriorPreset;
        [SerializeField] private CharacterPreset roguePreset;
        [SerializeField] private MonsterPreset demoMonster; // 약체 타일공격 몬스터(GreenSlime)

        public Character Warrior { get; private set; }
        public Character Rogue { get; private set; }
        public Monster DemoMonster { get; private set; }

        public static TutorialScenario EnsureInstance()
        {
            if (Instance != null) return Instance;
            Instance = FindAnyObjectByType<TutorialScenario>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                var prefab = Resources.Load<TutorialScenario>("TutorialScenario");
                Instance = prefab != null ? Instantiate(prefab)
                                          : new GameObject("TutorialScenario").AddComponent<TutorialScenario>();
            }
            return Instance;
        }

        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>데모 파티(전사·도적 인접) 스폰 후 약체 몬스터로 전투 개시.</summary>
        public void SpawnDemo()
        {
            var spawner = UnityEngine.Object.FindAnyObjectByType<CharacterSpawner>();
            int tileCount = UnityEngine.Object.FindAnyObjectByType<OrbitManager>()?.TileCount ?? 12;

            // slotCount = 타일수 → gap 1칸 → slot 0/1 = 인접 타일. 전사 옆에 도적 → 전사 패시브(+50%) 조건.
            Warrior = spawner?.Spawn(warriorPreset, 0, tileCount);
            Rogue   = spawner?.Spawn(roguePreset,   1, tileCount);

            if (demoMonster != null && CombatManager.Instance != null)
            {
                var enc = new EncounterDefinition { MonsterPresets = new List<MonsterPreset> { demoMonster } };
                CombatManager.Instance.StartEncounter(enc, 1); // 몬스터 스폰 + 인트로 + 전투 개시
                DemoMonster = CombatManager.Instance.ActiveMonsters.FirstOrDefault(m => m != null);
                // 턴1 통제 주사위 (전사 스킬 조건 + 도적 원거리 이동6). 인트로 후 StartPlayerTurn의 자동 굴림이 소모.
                DiceManager.Instance?.SetScriptedRoll(new[] { 4, 5, 6, 6 });
            }
            else
            {
                Debug.LogError("[TutorialScenario] demoMonster/CombatManager 없음 — 데모 전투 개시 불가.");
            }
        }

        // 진행조건 이벤트 핸들러 (Cleanup에서 해제)
        private Action<Character> _onMoved, _onSkill;

        /// <summary>전투 12단계 시퀀스. 대상/조건은 런타임 상태를 참조하므로 델리게이트로 지연 평가.
        /// 정보 단계(Confirm)만 입력잠금, 조작 단계는 하이라이트+조건 대기(비잠금 — 다중 클릭 허용).</summary>
        public List<TutorialStep> BuildCombatSteps()
        {
            var cm = CombatManager.Instance;

            // 진행 플래그 (이벤트 구독으로 세팅 — 클로저 공유)
            bool warriorSkill = false, rogueMoved = false, rogueSkill = false;
            _onMoved = c => { if (c == Rogue) rogueMoved = true; };
            _onSkill = c => { if (c == Warrior) warriorSkill = true; if (c == Rogue) rogueSkill = true; };
            if (cm != null) { cm.OnPlayerMoved += _onMoved; cm.OnPlayerSkillUsed += _onSkill; }

            Func<RectTransform> Action = () => CharacterActionUI.Instance != null ? CharacterActionUI.Instance.PanelRoot : null;
            Func<RectTransform> Dice   = () => DiceUI.Instance != null ? DiceUI.Instance.PanelRect : null;
            Func<RectTransform> EndTurn = () => cm != null ? cm.EndTurnRect : null;
            Func<RectTransform> W = () => ScreenBoxProvider.ForWorld(Warrior != null ? Warrior.transform : null);
            Func<RectTransform> R = () => ScreenBoxProvider.ForWorld(Rogue != null ? Rogue.transform : null);
            Func<RectTransform> M = () => ScreenBoxProvider.ForWorld(DemoMonster != null ? DemoMonster.transform : null);

            return new List<TutorialStep>
            {
                new TutorialStep("원형 궤도 위 캐릭터로 몬스터를 처치하는 게 목표예요. 시작해볼까요?")
                    { GateInput = true },
                new TutorialStep("몬스터 머리 위 아이콘 = 다음 행동, 바닥의 색칠된 타일 = 그 공격이 닿는 범위예요. 그 위에 있는 캐릭터가 맞습니다.")
                    { Target = M, GateInput = true },
                new TutorialStep("매 턴 주사위가 자동으로 굴려집니다. 이번 턴에 쓸 자원이에요.")
                    { Target = Dice, GateInput = true },
                new TutorialStep("전사를 클릭하세요.")
                    { Target = W, Advance = TutorialAdvance.Custom,
                      Done = () => CharacterActionUI.Instance != null && CharacterActionUI.Instance.IsShowingCharacter(Warrior) },
                new TutorialStep("이동은 턴당 1번, 행동(스킬)도 턴당 1번만 가능해요.")
                    { Target = Action, GateInput = true },
                new TutorialStep("전사 좌우에 아군이 있으면 공격 +50%! 지금 도적이 옆에 있죠. 주사위를 골라 스킬로 몬스터를 공격하세요.")
                    { Target = Action, Advance = TutorialAdvance.Custom, Done = () => warriorSkill },
                new TutorialStep("이제 도적! 멀리 이동할수록 다음 공격이 강해져요(1칸당 +25%). 도적을 골라 주사위로 멀리 이동해보세요.")
                    { Target = R, Advance = TutorialAdvance.Custom, Done = () => rogueMoved },
                new TutorialStep("이동한 만큼 강해진 공격으로 몬스터를 타격하세요!")
                    { Target = Action, Advance = TutorialAdvance.Custom, Done = () => rogueSkill },
                new TutorialStep("방금 전사·도적 효과는 모두 '패시브' — 버튼 없이 조건이 맞으면 자동 발동해요. 스킬 버튼은 '액티브'!")
                    { GateInput = true },
                new TutorialStep("행동을 마쳤으면 [턴 종료]로 몬스터 턴을 넘기세요.")
                    { Target = EndTurn, Advance = TutorialAdvance.Custom,
                      Done = () => cm == null || !cm.InCombat || !cm.PlayerTurnActive },
                new TutorialStep("예고한 색 타일 범위로 몬스터가 공격합니다! (그 위 캐릭터가 피격)")
                    { Target = M, Advance = TutorialAdvance.Custom,
                      Done = () => cm == null || !cm.InCombat || cm.PlayerTurnActive },
                new TutorialStep("이제 마무리! 남은 몬스터를 처치하세요.")
                    { Advance = TutorialAdvance.Custom, Done = () => cm == null || !cm.InCombat },
            };
        }

        /// <summary>데모 정리 — 실제 런 시작 전에 데모 파티/몬스터 제거(파티 0으로 → Recruit가 2명 선택으로 시작).</summary>
        public void Cleanup()
        {
            var cm = CombatManager.Instance;
            if (cm != null)
            {
                if (_onMoved != null) cm.OnPlayerMoved -= _onMoved;
                if (_onSkill != null) cm.OnPlayerSkillUsed -= _onSkill;
            }
            _onMoved = null; _onSkill = null;
            ScreenBoxProvider.ClearAll();

            PartyManager.Instance?.ClearAll();   // 데모 파티 제거 + 오브젝트 파괴
            foreach (var m in UnityEngine.Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
                if (m != null) Destroy(m.gameObject);
            Warrior = null; Rogue = null; DemoMonster = null;
        }
    }
}
