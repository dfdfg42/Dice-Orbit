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
        [SerializeField] private Sprite tutorialBackground; // 튜토리얼 전투 배경(배경 수정 1차)

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

            // 캐릭터 지연 배치(Character.InitializeAfterDelay)가 끝난 뒤 전투 개시.
            // 인트로의 타일 이동과 레이스가 나면 캐릭터가 엉뚱한(공중) 위치에 고정된다.
            StartCoroutine(StartCombatAfterSettle());
        }

        private System.Collections.IEnumerator StartCombatAfterSettle()
        {
            float t = 0f;
            while ((Warrior == null || Warrior.CurrentTile == null) && t < 1f) { t += Time.deltaTime; yield return null; }
            yield return null; // 여유 한 프레임

            // 튜토리얼: 데모 캐릭터는 죽지 않게(HP 1 미만 방지) — 몬스터 공격에 튜토리얼이 실패하지 않도록
            if (Warrior != null && Warrior.Stats != null) Warrior.Stats.Invulnerable = true;
            if (Rogue != null && Rogue.Stats != null) Rogue.Stats.Invulnerable = true;

            if (demoMonster != null && CombatManager.Instance != null)
            {
                var enc = new EncounterDefinition { MonsterPresets = new List<MonsterPreset> { demoMonster } };
                if (tutorialBackground != null) enc.BackgroundSprite = tutorialBackground; // 튜토리얼 전용 배경
                CombatManager.Instance.StartEncounter(enc, 1); // 몬스터 스폰 + 인트로 + 전투 개시
                DemoMonster = CombatManager.Instance.ActiveMonsters.FirstOrDefault(m => m != null);
                // 턴1 고정 주사위: 전사 돌파(4↑)=4 → 타일4(구역0 유지), 도적 급습(3↓)=2 → 타일3(전사와 같은 구역=협공), 여분 5·3.
                // 인트로 후 StartPlayerTurn의 자동 굴림이 이 값을 소모(1회).
                DiceManager.Instance?.SetScriptedRoll(new[] { 4, 2, 5, 3 });
            }
            else
            {
                Debug.LogError("[TutorialScenario] demoMonster/CombatManager 없음 — 데모 전투 개시 불가.");
            }
        }

        // 진행조건 이벤트 핸들러 (Cleanup에서 해제)
        private Action<Character> _onMoved;

        /// <summary>전투 12단계 시퀀스. 대상/조건은 런타임 상태를 참조하므로 델리게이트로 지연 평가.
        /// 정보 단계(Confirm)만 입력잠금, 조작 단계는 하이라이트+조건 대기(비잠금 — 다중 클릭 허용).</summary>
        public List<TutorialStep> BuildCombatSteps()
        {
            var cm = CombatManager.Instance;

            // 진행 플래그 (이벤트 구독으로 세팅 — 클로저 공유)
            // 공격이 자동이 된 뒤로는 이동이 유일한 조작이므로 이동 이벤트만 본다.
            bool warriorMoved = false, rogueMoved = false;
            _onMoved = c => { if (c == Warrior) warriorMoved = true; if (c == Rogue) rogueMoved = true; };
            if (cm != null) { cm.OnPlayerMoved += _onMoved; }

            Func<RectTransform> Action = () => CharacterActionUI.Instance != null ? CharacterActionUI.Instance.PanelRoot : null;
            Func<RectTransform> Dice   = () => DiceUI.Instance != null ? DiceUI.Instance.PanelRect : null;
            Func<RectTransform> EndTurn = () => cm != null ? cm.EndTurnRect : null;
            Func<RectTransform> W = () => ScreenBoxProvider.ForWorld(Warrior != null ? Warrior.transform : null);
            Func<RectTransform> R = () => ScreenBoxProvider.ForWorld(Rogue != null ? Rogue.transform : null);
            Func<RectTransform> M = () => ScreenBoxProvider.ForWorld(DemoMonster != null ? DemoMonster.transform : null);
            Func<RectTransform> Field = () =>
            {
                var om = UnityEngine.Object.FindAnyObjectByType<OrbitManager>();
                return om != null ? ScreenBoxProvider.ForWorld(om.transform, new Vector2(1050, 900)) : null;   // 위아래로 더 넓게
            };
            Func<RectTransform> Info = () => BattleInfoPanelUI.Instance != null ? BattleInfoPanelUI.Instance.PanelRect : null;

            return new List<TutorialStep>
            {
                new TutorialStep("캐릭터를 움직여 궤도 안의 몬스터를 모두 처치하세요. 먼저 전투 화면부터 살펴볼게요.")
                    { GateInput = true },
                // 몬스터 → 필드(공격범위) 순서로 각각 보게
                new TutorialStep("몬스터 머리 위의 아이콘은 다음 턴에 사용할 행동을 보여줍니다.")
                    { Target = M, GateInput = true, HighlightOffset = new Vector2(0, 60) },   // 머리 위 아이콘까지 보이게 조금 위로
                new TutorialStep("오른쪽 정보 패널에서 몬스터의 체력과 다음 행동을 자세히 확인할 수 있습니다.")
                    { Target = Info, GateInput = true, CenterBubble = true, OnEnter = () => BattleInfoPanelUI.Instance?.ShowUnitExternal(DemoMonster) },
                new TutorialStep("전장은 <b>4개 구역</b>으로 나뉩니다. 구역 가장자리의 색은 그 구역을 맡은 몬스터를 나타냅니다. 캐릭터는 자신이 서 있는 구역의 몬스터를 공격합니다.")
                    { Target = Field, GateInput = true, OnEnter = () => BattleInfoPanelUI.Instance?.ClearUnitExternal(DemoMonster) },
                new TutorialStep("색이 칠해진 타일은 몬스터가 다음 턴에 공격할 범위입니다. 공격 전에 안전한 곳으로 이동하세요.")
                    { Target = Field, GateInput = true },
                new TutorialStep("플레이어 턴이 시작되면 주사위가 자동으로 굴러갑니다. 나온 주사위를 캐릭터에게 하나씩 배정할 수 있습니다.")
                    { Target = Dice, GateInput = true },
                new TutorialStep("이동을 마친 캐릭터는 도착한 구역의 몬스터를 자동으로 공격합니다. 따로 공격 버튼을 누를 필요는 없습니다.")
                    { GateInput = true },
                new TutorialStep("전사를 선택하세요.")
                    { Target = W, Advance = TutorialAdvance.Custom,
                      Done = () => CharacterActionUI.Instance != null && CharacterActionUI.Instance.IsShowingCharacter(Warrior) },
                new TutorialStep("캐릭터마다 한 턴에 한 번 이동할 수 있습니다. 주사위 눈은 이동 거리와 강화 공격의 발동 여부를 함께 결정합니다.")
                    { Target = Action, GateInput = true, HighlightPad = new Vector4(0, 0, 0, 120) },   // 아래로 더 길게
                // 이동(전체 화면) + 눈 4만 — 4 이상이라 돌파 강화 발동
                new TutorialStep("<b>눈 4</b> 주사위를 전사에게 배정하고 이동하세요. 전사는 눈이 <b>4 이상</b>이면 [돌파]를 사용해 이동 중 지나온 모든 구역의 몬스터를 공격합니다.")
                    { NoSpotlight = true, OnlyDieValue = 4, ActionLock = TutorialActionLock.MoveOnly, Advance = TutorialAdvance.Custom, Done = () => warriorMoved },
                // 도적 선택(스포트라이트)
                new TutorialStep("이번에는 도적을 선택하세요. 강화 조건에 맞지 않는 주사위에는 <b>빗금 표시</b>가 붙으며, 그 주사위를 쓰면 기본 공격만 발동합니다.")
                    { Target = R, Advance = TutorialAdvance.Custom,
                      Done = () => CharacterActionUI.Instance != null && CharacterActionUI.Instance.IsShowingCharacter(Rogue) },
                // 이동(전체 화면) + 눈 2만 — 3 이하라 급습 강화 발동
                new TutorialStep("<b>눈 2</b> 주사위를 도적에게 배정하고 이동하세요. 도적은 눈이 <b>3 이하</b>이면 [급습]을 사용합니다. 전사와 <b>같은 구역</b>에 도착하면 [협공]도 발동해 피해가 두 배로 증가합니다.")
                    { NoSpotlight = true, OnlyDieValue = 2, ActionLock = TutorialActionLock.MoveOnly, Advance = TutorialAdvance.Custom, Done = () => rogueMoved },
                new TutorialStep("정리해 볼까요? 공격은 이동 후 자동으로 발동합니다. <b>주사위 눈</b>은 강화 공격을, <b>캐릭터의 위치</b>는 패시브 발동을 결정합니다.")
                    { GateInput = true },
                new TutorialStep("행동을 모두 마쳤다면 [턴 종료]를 눌러 몬스터 턴으로 넘어가세요.")
                    { Target = EndTurn, Advance = TutorialAdvance.Custom,
                      Done = () => cm == null || !cm.InCombat || !cm.PlayerTurnActive },
                new TutorialStep("몬스터가 예고했던 타일을 공격합니다. 공격 범위 안에 남아 있는 캐릭터는 피해를 받습니다.")
                    { NoSpotlight = true, Advance = TutorialAdvance.Custom,
                      Done = () => cm == null || !cm.InCombat || cm.PlayerTurnActive },
                new TutorialStep("이제 배운 내용을 활용해 남은 몬스터를 처치하세요.")
                    { NoSpotlight = true, Advance = TutorialAdvance.Custom, AllowDismiss = true, Done = () => cm == null || !cm.InCombat },
            };
        }

        /// <summary>데모 정리 — 실제 런 시작 전에 데모 파티/몬스터 제거(파티 0으로 → Recruit가 2명 선택으로 시작).</summary>
        public void Cleanup()
        {
            var cm = CombatManager.Instance;
            if (cm != null)
            {
                if (_onMoved != null) cm.OnPlayerMoved -= _onMoved;
            }
            _onMoved = null;
            ScreenBoxProvider.ClearAll();
            HoverTooltipUI.Instance?.HidePinned();   // 데모 몬스터 위에 떠 있던 커서 툴팁이 얼어붙어 남지 않게
            DiceManager.Instance?.SetScriptedRoll(null);   // 잔여 통제 주사위가 실제 런으로 새지 않게
            DiceUI.Instance?.SetTutorialDiceLock(null);    // 주사위 잠금 해제(실제 런 무영향)
            CharacterActionUI.Instance?.SetTutorialActionLock(TutorialActionLock.None);

            if (Warrior?.Stats != null) Warrior.Stats.Invulnerable = false;   // 방어적 해제(스탯이 공유일 경우 대비)
            if (Rogue?.Stats != null) Rogue.Stats.Invulnerable = false;
            PartyManager.Instance?.ClearAll();   // 데모 파티 제거 + 오브젝트 파괴
            foreach (var m in UnityEngine.Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
                if (m != null) Destroy(m.gameObject);
            Warrior = null; Rogue = null; DemoMonster = null;
        }
    }
}
