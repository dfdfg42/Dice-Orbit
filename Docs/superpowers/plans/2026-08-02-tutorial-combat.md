# 튜토리얼 (코어 전투) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 게임 시작 시 선택형 스크립트 튜토리얼 전투 1회를 제공해, 새 플레이어가 코어 전투(주사위 배정·이동1행동1·액티브/패시브·몬스터 의도/공격범위)와 첫 캐릭터 선택(2명)을 배우게 한다.

**Architecture:** 재사용 가능한 경량 튜토리얼 프레임워크(`TutorialStep` 데이터 + `TutorialDirector` 코루틴 + `TutorialOverlayUI` 스포트라이트/입력잠금). GameFlow에 `Tutorial` 상태를 추가하고, 고정 데모 셋업(전사+도적 인접, 약체 타일공격 몬스터, 통제 주사위)에서 Director가 단계를 재생한다. 전투(Tutorial)→실제 런(Recruit)은 BattleScene 내 상태 전환이라 Director가 지속하며 step 13(캐릭터 선택 안내)까지 이어간다. 실제 런 상태(RunManager/세이브/영구덱)는 절대 오염하지 않는다.

**Tech Stack:** Unity 6000.3.8f1, uGUI + TextMeshPro, 싱글톤 매니저 패턴. 기존 시스템 재사용: CombatManager, DiceManager, CharacterActionUI, MonsterAttackIntentManager, CharacterSpawner, EncounterSpawner, GameFlowManager.

## Global Constraints

- **격리**: 튜토리얼은 RunManager(맵/시드), RunSaveService(세이브), DiceDeckManager 영구 교체/부여분, GoldManager/ArtifactManager/PotionManager를 변경하지 않는다. 데모 파티/몬스터만 스폰하고 종료 시 정리한다.
- **완료 플래그**: `PlayerPrefs` 키 `"tutorial_done"`(int, 1=완료). 완료 전엔 StartGame마다 프롬프트, 완료 후엔 자동 프롬프트 없음.
- **데모 파티**: 전사 + 도적 2명, 인접 타일 배치(전사 패시브 +50% 시연). `charactersToSelect`(CharacterSelectionUI)는 이미 2 → step 13은 이를 전제.
- **검증 사이클(이 프로젝트 표준)**: 각 태스크는 pytest 대신 **① 컴파일 게이트**(Unity MCP RunCommand: `AssetDatabase.Refresh()` 후 `GetConsoleLogs(error)` = 0) → **② 플레이/에디터 검증**(RunCommand로 상태 구동 + 콘솔 로그/스크린샷 확인) → **③ 커밋**. 순수 로직은 RunCommand 내 단언(assert→LogError)으로 확인.
- **네임스페이스**: 신규 튜토리얼 코드는 `DiceOrbit.UI.Tutorial`(UI) / `DiceOrbit.Core.Tutorial`(플로우). 커밋 메시지 말미: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- **RunCommand 주의**: `UnityEngine.UI.Image`는 정규화(네임스페이스 충돌), `internal class CommandScript : IRunCommand` 형식.

## File Structure

**신규**
- `Assets/Scripts/UI/Tutorial/TutorialStep.cs` — 단계 데이터 + 진행조건 enum + 하이라이트 대상 참조.
- `Assets/Scripts/UI/Tutorial/TutorialOverlayUI.cs` — 딤 4분할 스포트라이트 + 말풍선 + 입력잠금 + 스킵 버튼. 런타임 생성(씬 배치 불필요).
- `Assets/Scripts/UI/Tutorial/TutorialDirector.cs` — 싱글톤 코루틴 엔진. 단계 재생·진행조건 폴링·오버레이 구동·상태전환 지속.
- `Assets/Scripts/Core/Tutorial/TutorialScenario.cs` — 고정 데모 셋업(파티/몬스터/통제 주사위) + step 리스트 정의.
- `Assets/Scripts/UI/Tutorial/TutorialPromptUI.cs` — "튜토리얼 하시겠어요? [예][아니오]" 런타임 모달.

**수정**
- `Assets/Scripts/Core/GameState.cs` — `Tutorial` 추가.
- `Assets/Scripts/Core/GameFlowManager.cs` — StartGame 프롬프트 분기, `EnterState(Tutorial)`, 완료 후 실제 런 개시.
- `Assets/Scripts/.../Dices/DiceManager.cs` — 스크립트 강제 굴림 훅.
- `Assets/Scripts/.../Combat/CombatManager.cs` — 이동/스킬 실행 알림 이벤트.
- `Assets/Scripts/UI/CharacterSelectionUI.cs` — 남은 선택 수 조회 프로퍼티(step 13 진행조건용).

---

### Task 1: GameState.Tutorial + StartGame 프롬프트 골격

**Files:**
- Modify: `Assets/Scripts/Core/GameState.cs`
- Create: `Assets/Scripts/UI/Tutorial/TutorialPromptUI.cs`
- Modify: `Assets/Scripts/Core/GameFlowManager.cs` (StartGame, EnterState)

**Interfaces:**
- Produces: `GameState.Tutorial`; `TutorialPromptUI.Show(System.Action onYes, System.Action onNo)`; `GameFlowManager`가 StartGame 시 프롬프트 표시 후 분기.

- [ ] **Step 1: GameState에 Tutorial 추가**

`GameState.cs`의 enum 마지막 항목(`Event`) 뒤에 콤마와 함께 추가:

```csharp
        Event,              // 이벤트 노드 — 주사위 도박
        Tutorial            // 튜토리얼 전투 (신규, 실제 런과 격리)
```

- [ ] **Step 2: TutorialPromptUI 작성 (런타임 모달)**

`Assets/Scripts/UI/Tutorial/TutorialPromptUI.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>"튜토리얼 하시겠어요?" 런타임 모달. 씬 배치 불필요 — Show()가 캔버스째 생성.</summary>
    public class TutorialPromptUI : MonoBehaviour
    {
        public static void Show(Action onYes, Action onNo)
        {
            var go = new GameObject("TutorialPromptUI");
            var self = go.AddComponent<TutorialPromptUI>();
            self.Build(onYes, onNo);
        }

        private void Build(Action onYes, Action onNo)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            gameObject.AddComponent<GraphicRaycaster>();

            var dim = NewImage(transform, new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim.rectTransform);

            var panel = NewImage(transform, new Color(0.118f, 0.133f, 0.200f, 1f));
            var prt = panel.rectTransform;
            prt.sizeDelta = new Vector2(560, 260);
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;

            var label = NewText(panel.transform, "튜토리얼을 진행하시겠어요?", 34);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0.5f); lrt.anchorMax = new Vector2(1, 1f);
            lrt.offsetMin = new Vector2(20, 0); lrt.offsetMax = new Vector2(-20, -20);

            MakeButton(panel.transform, "예", new Vector2(-130, -70), new Color(0.878f, 0.702f, 0.341f),
                () => { Close(); onYes?.Invoke(); });
            MakeButton(panel.transform, "아니오", new Vector2(130, -70), new Color(0.200f, 0.255f, 0.368f),
                () => { Close(); onNo?.Invoke(); });
        }

        private void Close() => Destroy(gameObject);

        private void MakeButton(Transform parent, string text, Vector2 pos, Color color, Action onClick)
        {
            var img = NewImage(parent, color);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(200, 66);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var t = NewText(img.transform, text, 28);
            Stretch(t.rectTransform);
        }

        private static UnityEngine.UI.Image NewImage(Transform parent, Color c)
        {
            var go = new GameObject("Img", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = c;
            return img;
        }

        private static TextMeshProUGUI NewText(Transform parent, string s, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.910f, 0.894f, 0.847f); t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
    }
}
```

- [ ] **Step 3: GameFlowManager.StartGame에 프롬프트 분기**

`StartGame()` 본문 시작에서, `RunSaveService.Delete()` 호출 **전에** 튜토리얼 분기를 넣는다. 기존 본문을 `StartGameInternal()`로 추출하고 StartGame은 프롬프트만:

```csharp
        public void StartGame()
        {
            // 튜토리얼 미완료 시 물어본다. 예 → 튜토리얼, 아니오 → 기존 흐름.
            if (PlayerPrefs.GetInt("tutorial_done", 0) == 0)
            {
                UI.Tutorial.TutorialPromptUI.Show(
                    onYes: () => ChangeState(GameState.Tutorial),
                    onNo:  () => StartGameInternal());
                return;
            }
            StartGameInternal();
        }

        private void StartGameInternal()
        {
            Debug.Log("[GameFlow] StartGame called");
            RunSaveService.Delete();   // 새 게임 = 기존 이어하기 폐기
            // ... (기존 StartGame 본문 그대로 이동)
        }
```

기존 StartGame 본문(씬 로드/StartGameFlow 분기)을 그대로 `StartGameInternal`로 옮긴다.

- [ ] **Step 4: EnterState(Tutorial) 스텁**

`GameFlowManager`의 `EnterState` switch에 케이스 추가(당장은 로그 + 즉시 실제 런으로 — 다음 태스크에서 대체):

```csharp
                case GameState.Tutorial:
                    Debug.Log("[GameFlow] Enter Tutorial (스텁 — 데모/Director는 후속 태스크)");
                    // 임시: 아직 튜토리얼 미구현이므로 그냥 실제 런으로 넘어간다.
                    StartGameInternal();
                    break;
```

- [ ] **Step 5: 컴파일 게이트**

RunCommand: `AssetDatabase.Refresh()` → `GetConsoleLogs(error)` 결과 0 확인. 실패 시 수정.

- [ ] **Step 6: 플레이 검증**

플레이 진입 → 메인메뉴 `게임 시작` 클릭. 기대: "튜토리얼을 진행하시겠어요?" 모달. **아니오** → 기존 흐름(Recruit) 정상. **예** → 콘솔 "Enter Tutorial (스텁...)" 후 실제 런 진입. `PlayerPrefs.SetInt("tutorial_done",1)`로 세팅 후 재시작 시 프롬프트 미표시 확인.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Core/GameState.cs "Assets/Scripts/UI/Tutorial/TutorialPromptUI.cs" Assets/Scripts/Core/GameFlowManager.cs
git commit -m "feat(tutorial): GameState.Tutorial + StartGame 프롬프트 골격

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: TutorialOverlayUI (스포트라이트 + 말풍선 + 입력잠금)

**Files:**
- Create: `Assets/Scripts/UI/Tutorial/TutorialOverlayUI.cs`

**Interfaces:**
- Produces:
  - `TutorialOverlayUI.EnsureInstance() : TutorialOverlayUI`
  - `void ShowStep(string instruction, RectTransform screenTarget, bool gateInput, System.Action onNext, System.Action onSkip)` — `screenTarget`이 null이면 스포트라이트 없이 말풍선만. onNext는 "다음" 버튼(진행조건이 Confirm일 때만 노출; null이면 버튼 숨김).
  - `void HighlightScreenRect(Rect screenRect)` — 월드/동적 대상용(매 프레임 갱신).
  - `void Hide()`
- 스포트라이트 = 대상 rect 바깥을 4개 딤 패널(상/하/좌/우)로 가려 대상만 밝게. 입력잠금 시 대상 rect 구멍만 클릭 통과(딤 패널이 raycastTarget=true로 그 외 차단).

- [ ] **Step 1: TutorialOverlayUI 작성**

`Assets/Scripts/UI/Tutorial/TutorialOverlayUI.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>
    /// 튜토리얼 오버레이 — 대상 rect만 밝게 남기는 4분할 딤 스포트라이트 + 말풍선 + 스킵.
    /// 런타임 생성(씬 배치 불필요). 최상위 sortingOrder로 항상 위.
    /// </summary>
    public class TutorialOverlayUI : MonoBehaviour
    {
        public static TutorialOverlayUI Instance { get; private set; }

        private Canvas canvas;
        private RectTransform canvasRect;
        private UnityEngine.UI.Image[] dim = new UnityEngine.UI.Image[4]; // top/bottom/left/right
        private RectTransform bubble;
        private TextMeshProUGUI bubbleText;
        private Button nextButton;
        private Button skipButton;
        private Action onSkip;

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color Card = new Color(0.118f, 0.133f, 0.200f, 0.98f);
        private static readonly Color Ink = new Color(0.910f, 0.894f, 0.847f);
        private static readonly Color Gold = new Color(0.878f, 0.702f, 0.341f);

        public static TutorialOverlayUI EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("TutorialOverlayUI");
            Instance = go.AddComponent<TutorialOverlayUI>();
            Instance.Build();
            return Instance;
        }

        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4000;
            gameObject.AddComponent<GraphicRaycaster>();
            canvasRect = (RectTransform)transform;

            for (int i = 0; i < 4; i++)
            {
                var img = NewImage(transform, Dim, "Dim" + i);
                img.raycastTarget = true;   // 대상 외 클릭 차단
                dim[i] = img;
            }

            bubble = NewImage(transform, Card, "Bubble").rectTransform;
            bubble.sizeDelta = new Vector2(560, 150);
            bubbleText = NewText(bubble, "", 26);
            var brt = bubbleText.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(24, 54); brt.offsetMax = new Vector2(-24, -18);

            nextButton = MakeButton(bubble, "다음 ▸", new Vector2(-24, 16), new Vector2(1, 0), new Vector2(140, 40));
            skipButton = MakeButton(transform, "튜토리얼 스킵", new Vector2(-16, -16), new Vector2(1, 1), new Vector2(160, 40));
            skipButton.onClick.AddListener(() => onSkip?.Invoke());

            gameObject.SetActive(false);
        }

        public void ShowStep(string instruction, RectTransform screenTarget, bool gateInput, Action onNext, Action onSkipAction)
        {
            gameObject.SetActive(true);
            bubbleText.text = instruction;
            onSkip = onSkipAction;

            nextButton.gameObject.SetActive(onNext != null);
            nextButton.onClick.RemoveAllListeners();
            if (onNext != null) nextButton.onClick.AddListener(() => onNext());

            foreach (var d in dim) d.raycastTarget = gateInput; // 잠금 아니면 클릭 통과(정보용 단계)

            if (screenTarget != null) HighlightScreenRect(GetScreenRect(screenTarget));
            else HighlightScreenRect(new Rect(-9999, -9999, 0, 0)); // 대상 없음 → 전체 딤
        }

        /// <summary>대상 rect(스크린 좌표)만 남기고 4방향 딤 배치 + 말풍선 위치 조정.</summary>
        public void HighlightScreenRect(Rect r)
        {
            float w = Screen.width, h = Screen.height;
            // top
            SetRect(dim[0], 0, r.yMax, w, h - r.yMax);
            // bottom
            SetRect(dim[1], 0, 0, w, Mathf.Max(0, r.yMin));
            // left
            SetRect(dim[2], 0, Mathf.Max(0, r.yMin), Mathf.Max(0, r.xMin), Mathf.Min(r.yMax, h) - Mathf.Max(0, r.yMin));
            // right
            SetRect(dim[3], r.xMax, Mathf.Max(0, r.yMin), Mathf.Max(0, w - r.xMax), Mathf.Min(r.yMax, h) - Mathf.Max(0, r.yMin));

            // 말풍선: 대상 아래(공간 없으면 위)
            float bubbleH = bubble.sizeDelta.y;
            float bx = Mathf.Clamp(r.center.x, 300, w - 300);
            float by = (r.yMin - 20 - bubbleH * 0.5f);
            if (by - bubbleH * 0.5f < 0) by = r.yMax + 20 + bubbleH * 0.5f;
            bubble.anchorMin = bubble.anchorMax = bubble.pivot = new Vector2(0, 0);
            bubble.anchoredPosition = new Vector2(bx - bubble.sizeDelta.x * 0.5f + bubble.sizeDelta.x * 0.5f, by); // 중심 배치
            bubble.anchoredPosition = new Vector2(bx, by);
        }

        public void Hide() { onSkip = null; gameObject.SetActive(false); }

        public static Rect GetScreenRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners); // Overlay 캔버스면 월드=스크린 픽셀
            float xMin = Mathf.Min(corners[0].x, corners[2].x);
            float yMin = Mathf.Min(corners[0].y, corners[2].y);
            float xMax = Mathf.Max(corners[0].x, corners[2].x);
            float yMax = Mathf.Max(corners[0].y, corners[2].y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void SetRect(UnityEngine.UI.Image img, float x, float y, float w, float h)
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(Mathf.Max(0, w), Mathf.Max(0, h));
        }

        private Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 anchor, Vector2 size)
        {
            var img = NewImage(parent, Gold, "Btn");
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var t = NewText(img.rectTransform, text, 20); Stretch(t.rectTransform);
            t.color = new Color(0.14f, 0.11f, 0.055f);
            return btn;
        }

        private static UnityEngine.UI.Image NewImage(Transform parent, Color c, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<UnityEngine.UI.Image>(); img.color = c;
            return img;
        }
        private static TextMeshProUGUI NewText(Transform parent, string s, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.alignment = TextAlignmentOptions.Left;
            t.color = Ink; t.raycastTarget = false; t.enableWordWrapping = true;
            return t;
        }
        private static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
    }
}
```

- [ ] **Step 2: 컴파일 게이트** — Refresh + 에러 0.

- [ ] **Step 3: 에디터 검증 (스포트라이트 스크린샷)**

RunCommand로 오버레이를 띄우고 임의 스크린 rect를 하이라이트한 뒤, 게임뷰/렌더로 확인:

```csharp
// RunCommand 요지: TutorialOverlayUI.EnsureInstance().ShowStep("테스트 안내", null, true, ()=>{}, ()=>{});
//   그리고 HighlightScreenRect(new Rect(Screen.width*0.4f, Screen.height*0.4f, 200, 120)); 후 스크린샷.
```

기대: 지정 rect만 밝고 나머지 딤, 말풍선/스킵 버튼 표시.

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Scripts/UI/Tutorial/TutorialOverlayUI.cs"
git commit -m "feat(tutorial): 스포트라이트 오버레이 UI (딤 4분할 + 말풍선 + 스킵)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: TutorialStep + TutorialDirector 엔진 (Confirm 진행)

**Files:**
- Create: `Assets/Scripts/UI/Tutorial/TutorialStep.cs`
- Create: `Assets/Scripts/UI/Tutorial/TutorialDirector.cs`

**Interfaces:**
- Consumes: `TutorialOverlayUI`(Task 2).
- Produces:
  - `enum TutorialAdvance { Confirm, DiceRolled, CharacterSelected, MoveExecuted, SkillExecuted, EndTurn, MonsterActed, CombatWon, RecruitDone }`
  - `class TutorialStep { string Instruction; Func<RectTransform> Target; TutorialAdvance Advance; Func<bool> Done; bool GateInput; Action OnEnter; }`
  - `TutorialDirector.EnsureInstance()`; `void Play(List<TutorialStep> steps, Action onComplete)`; `void Abort()`(스킵/폴백).

- [ ] **Step 1: TutorialStep 작성**

`Assets/Scripts/UI/Tutorial/TutorialStep.cs`:

```csharp
using System;
using UnityEngine;

namespace DiceOrbit.UI.Tutorial
{
    public enum TutorialAdvance
    {
        Confirm,          // "다음" 버튼 클릭으로 진행 (정보 단계)
        Custom            // Step.Done() 델리게이트가 true를 반환하면 진행 (조작 단계)
    }

    /// <summary>튜토리얼 한 단계. Target/Done은 런타임 상태를 참조하므로 델리게이트로 지연 평가.</summary>
    public class TutorialStep
    {
        public string Instruction;
        public Func<RectTransform> Target;   // 하이라이트 대상(널 가능). 매 프레임 재평가.
        public TutorialAdvance Advance = TutorialAdvance.Confirm;
        public Func<bool> Done;              // Advance=Custom일 때 진행조건
        public bool GateInput;               // 대상 외 입력 차단
        public Action OnEnter;               // 단계 진입 시 1회 (통제 주사위 세팅 등)

        public TutorialStep(string instruction) { Instruction = instruction; }
    }
}
```

> 설계 스펙의 진행조건(DiceRolled/MoveExecuted 등)은 `Advance=Custom` + `Done` 델리게이트로 표현한다(Task 6에서 각 단계에 실제 폴링 람다를 주입). 이로써 Director는 조건 종류를 몰라도 되고 결합이 낮다.

- [ ] **Step 2: TutorialDirector 작성**

`Assets/Scripts/UI/Tutorial/TutorialDirector.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>튜토리얼 단계 재생 엔진. 오버레이를 구동하고 각 단계 진행조건까지 대기.
    /// 상태 전환(Tutorial→Recruit)은 BattleScene 내라 이 오브젝트가 지속되어 이어서 재생.</summary>
    public class TutorialDirector : MonoBehaviour
    {
        public static TutorialDirector Instance { get; private set; }

        private bool _confirmPressed;
        private Action _onComplete;
        private bool _aborted;

        public static TutorialDirector EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("TutorialDirector");
            Instance = go.AddComponent<TutorialDirector>();
            return Instance;
        }

        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Play(List<TutorialStep> steps, Action onComplete)
        {
            _onComplete = onComplete;
            _aborted = false;
            StartCoroutine(Run(steps));
        }

        /// <summary>스킵/폴백 — 오버레이 제거, 완료 콜백은 호출하지 않음(호출자가 스킵 흐름 처리).</summary>
        public void Abort()
        {
            _aborted = true;
            TutorialOverlayUI.Instance?.Hide();
            StopAllCoroutines();
        }

        private IEnumerator Run(List<TutorialStep> steps)
        {
            var overlay = TutorialOverlayUI.EnsureInstance();

            foreach (var step in steps)
            {
                if (_aborted) yield break;

                // 단계별 예외 격리 — 어떤 단계가 죽어도 전체가 멈추지 않게.
                try { step.OnEnter?.Invoke(); }
                catch (Exception e) { Debug.LogWarning($"[Tutorial] OnEnter 예외: {e}"); }

                _confirmPressed = false;
                bool isConfirm = step.Advance == TutorialAdvance.Confirm;

                overlay.ShowStep(
                    step.Instruction,
                    SafeTarget(step),
                    step.GateInput,
                    onNext: isConfirm ? (Action)(() => _confirmPressed = true) : null,
                    onSkipAction: OnSkipRequested);

                // 진행조건 대기: 매 프레임 대상 rect 갱신 + 조건 검사.
                while (!_aborted)
                {
                    var target = SafeTarget(step);
                    if (target != null)
                        overlay.HighlightScreenRect(TutorialOverlayUI.GetScreenRect(target));

                    bool done = isConfirm ? _confirmPressed : SafeDone(step);
                    if (done) break;
                    yield return null;
                }

                if (_aborted) yield break;
            }

            overlay.Hide();
            var cb = _onComplete; _onComplete = null;
            cb?.Invoke();
        }

        private RectTransform SafeTarget(TutorialStep s)
        {
            try { return s.Target?.Invoke(); } catch { return null; }
        }
        private bool SafeDone(TutorialStep s)
        {
            try { return s.Done != null && s.Done(); } catch { return false; }
        }

        private void OnSkipRequested()
        {
            // 스킵 = 튜토리얼 포기. 완료 플래그 세팅 후 즉시 실제 런으로(호출자 정의).
            _aborted = true;
            TutorialOverlayUI.Instance?.Hide();
            StopAllCoroutines();
            TutorialSkipHandler?.Invoke();
        }

        /// <summary>스킵 시 실행할 콜백(시나리오/플로우가 주입). 실제 런으로 폴백.</summary>
        public Action TutorialSkipHandler;
    }
}
```

- [ ] **Step 3: 컴파일 게이트** — Refresh + 에러 0.

- [ ] **Step 4: 에디터 검증 (더미 2단계)**

RunCommand로 Confirm 단계 2개를 Play하고, `_confirmPressed`를 코드로 눌러(또는 nextButton.onClick.Invoke) 다음으로 넘어가는지 + 마지막에 onComplete 호출되는지 콘솔 로그로 확인:

```csharp
// 요지: var steps = new List<TutorialStep>{ new("A"), new("B") };
//   TutorialDirector.EnsureInstance().Play(steps, () => Debug.Log("[TEST] tutorial complete"));
//   그리고 TutorialOverlayUI의 nextButton을 두 번 Invoke → "[TEST] tutorial complete" 확인.
```

- [ ] **Step 5: 커밋**

```bash
git add "Assets/Scripts/UI/Tutorial/TutorialStep.cs" "Assets/Scripts/UI/Tutorial/TutorialDirector.cs"
git commit -m "feat(tutorial): TutorialStep 모델 + Director 재생 엔진 (Confirm/Custom 진행)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: 진행조건 훅 (Combat 알림 + Dice 강제 굴림)

**Files:**
- Modify: `Assets/Scripts/.../Combat/CombatManager.cs`
- Modify: `Assets/Scripts/.../Dices/DiceManager.cs`

**Interfaces:**
- Produces:
  - `CombatManager`: `public event Action<Character> OnPlayerMoved;`(TrySpendMove 성공 시) `public event Action<Character> OnPlayerSkillUsed;`(TrySpendAction 성공 시). 이미 있는 `OnCombatEnd`(승/패 후) 재사용, `CombatStatus combatStatus`/`InCombat` 폴링 가능.
  - `DiceManager`: `public void SetScriptedRoll(int[] faces)` — 다음 `RollDice()`가 덱 대신 이 값들로 굴린 것처럼 세팅(길이 부족 시 나머지는 정상 굴림). 1회 소모.

- [ ] **Step 1: CombatManager 알림 이벤트 추가**

`CombatManager` 이벤트 선언부(`OnMonsterDeath` 근처)에 추가:

```csharp
        public System.Action<Character> OnPlayerMoved;      // TrySpendMove 성공 시
        public System.Action<Character> OnPlayerSkillUsed;  // TrySpendAction 성공 시
```

`TrySpendMove`가 true를 반환하기 직전에 발화:

```csharp
        public bool TrySpendMove(Character character)
        {
            if (!CanSpendMove(character)) return false;
            playerTurnBudgets[character].RemainingMove--;
            OnPlayerMoved?.Invoke(character);
            return true;
        }
```

`TrySpendAction` 동일:

```csharp
        public bool TrySpendAction(Character character)
        {
            if (!CanSpendAction(character)) return false;
            playerTurnBudgets[character].RemainingAction--;
            OnPlayerSkillUsed?.Invoke(character);
            return true;
        }
```

- [ ] **Step 2: DiceManager 강제 굴림 훅 추가**

필드 추가(클래스 상단):

```csharp
        private int[] _scriptedRoll;   // 세팅되면 다음 RollDice가 이 값 사용 후 소모
        public void SetScriptedRoll(int[] faces) => _scriptedRoll = faces;
```

`RollDice()`의 덱 순회 루프를 스크립트 값 우선으로 교체:

```csharp
                int i = 0;
                foreach (var inst in deck)
                {
                    int face = (_scriptedRoll != null && i < _scriptedRoll.Length)
                        ? _scriptedRoll[i]
                        : inst.RollFace();
                    currentDice.Add(new DiceData(diceIdCounter++, face, inst));
                    i++;
                }
                _scriptedRoll = null; // 1회 소모
```

(기존 `foreach (var inst in deck) currentDice.Add(...RollFace()...)` 를 위 블록으로 대체.)

- [ ] **Step 3: 컴파일 게이트** — Refresh + 에러 0.

- [ ] **Step 4: 에디터/플레이 검증**

- `SetScriptedRoll(new[]{3,3})` 후 `RollDice()` → `CurrentDice`의 앞 두 값이 3,3인지 로그 확인. 그다음 `RollDice()`는 정상(랜덤)인지 확인(1회 소모).
- 실제 전투에서 이동 1회 → `OnPlayerMoved` 로그, 스킬 1회 → `OnPlayerSkillUsed` 로그(임시 구독 후 확인).

- [ ] **Step 5: 커밋**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/CombatManager.cs" "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DiceManager.cs"
git commit -m "feat(tutorial): 이동/스킬 실행 알림 + 주사위 강제 굴림 훅

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: TutorialScenario — 고정 데모 셋업

**Files:**
- Create: `Assets/Scripts/Core/Tutorial/TutorialScenario.cs`
- (에셋) 튜토리얼 전용 고정 `EncounterDefinition` 및 캐릭터 프리셋 참조 — 기존 프리셋 재사용(전사/도적, 약체 몬스터).

**Interfaces:**
- Consumes: `CharacterSpawner`, `EncounterSpawner`, `CombatManager`, `DiceManager`, `PartyManager`, 전사/도적 `CharacterPreset`, 약체 몬스터 `EncounterDefinition`.
- Produces:
  - `TutorialScenario.EnsureInstance()`
  - `void SpawnDemo()` — 전사·도적을 **인접 타일**에 스폰(격리: PartyManager 데모 표시만, RunManager 미사용), 약체 타일공격 몬스터 스폰, 전투 개시.
  - `void Cleanup()` — 데모 파티/몬스터 제거, PartyManager 초기화(실제 런 오염 방지).
  - `Character Warrior { get; }`, `Character Rogue { get; }`, `Monster DemoMonster { get; }`(단계 타깃/조건용).

- [ ] **Step 1: 데모 에셋 확인/생성**

- 전사/도적 프리셋: `Assets/Scripts/Data/Character Preset/Warrior/Warrior.asset`, `.../Rogue/Rogue.asset` (기존).
- 약체 몬스터: **타일 기반 공격**(AttackIntent TargetType.Tiles)을 가진 저HP 프리셋 필요. 기존 중 조건 맞는 것 조사(RunCommand로 각 몬스터 프리셋의 스킬 TargetType 스캔). 없으면 튜토리얼 전용 `EncounterDefinition`/약체 몬스터 프리셋 신규 생성(HP 낮게, 타일 공격 스킬 1개, AreaRadius로 색 범위). 경로: `Assets/Scripts/Data/MonsterPresets/Tutorial/TutorialDummy.asset` + Encounter.
- 이 스텝의 산출물: 확정된 전사/도적 프리셋 참조 + 약체 타일공격 몬스터 Encounter 1개(경로 기록).

- [ ] **Step 2: TutorialScenario 작성**

`Assets/Scripts/Core/Tutorial/TutorialScenario.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core.Run;

namespace DiceOrbit.Core.Tutorial
{
    /// <summary>튜토리얼 고정 데모 셋업. 실제 런 상태를 건드리지 않고 데모 파티/몬스터만 스폰·정리.</summary>
    public class TutorialScenario : MonoBehaviour
    {
        public static TutorialScenario Instance { get; private set; }

        [Header("데모 데이터 (인스펙터 배선 또는 Resources)")]
        [SerializeField] private CharacterPreset warriorPreset;
        [SerializeField] private CharacterPreset roguePreset;
        [SerializeField] private EncounterDefinition demoEncounter; // 약체 타일공격 몬스터 1

        public Character Warrior { get; private set; }
        public Character Rogue { get; private set; }
        public Monster DemoMonster { get; private set; }

        public static TutorialScenario EnsureInstance()
        {
            if (Instance != null) return Instance;
            // 데모 데이터가 배선된 씬 오브젝트를 우선 사용, 없으면 Resources 프리팹.
            Instance = FindAnyObjectByType<TutorialScenario>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                var prefab = Resources.Load<TutorialScenario>("TutorialScenario");
                if (prefab != null) Instance = Instantiate(prefab);
                else Instance = new GameObject("TutorialScenario").AddComponent<TutorialScenario>();
            }
            return Instance;
        }

        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>데모 파티(전사·도적 인접) + 몬스터 스폰 후 전투 개시.</summary>
        public void SpawnDemo()
        {
            var spawner = Object.FindAnyObjectByType<CharacterSpawner>();
            var party = PartyManager.Instance;
            // 인접 슬롯: 전체 궤도를 4분할한다고 할 때 인접이 되도록 slotCount를 타일 수로 크게 잡아 연속 인덱스로 배치.
            // (정확한 인접 보장을 위해 CharacterSpawner.Spawn(preset, slotIndex, slotCount) 사용, slotCount=타일수)
            int tileCount = Object.FindAnyObjectByType<OrbitManager>()?.TileCount ?? 12;
            Warrior = spawner?.Spawn(warriorPreset, 0, tileCount);
            Rogue   = spawner?.Spawn(roguePreset,   1, tileCount); // 전사 옆(인덱스 1) → 인접

            var spawned = EncounterSpawner.EnsureInstance().Spawn(demoEncounter, startHidden: false);
            foreach (var m in spawned) CombatManager.Instance.RegisterMonster(m);
            DemoMonster = spawned.Count > 0 ? spawned[0] : null;

            CombatManager.Instance.StartCombat();
        }

        /// <summary>데모 정리 — 실제 런 시작 전에 데모 파티/몬스터 제거.</summary>
        public void Cleanup()
        {
            var party = PartyManager.Instance;
            if (party != null) party.ClearParty();   // 데모 파티 제거(실제 런 Recruit가 새로 채움)
            // 남은 몬스터/오브젝트 정리
            foreach (var m in Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
                if (m != null) Destroy(m.gameObject);
        }
    }
}
```

> 주의: `CharacterSpawner.Spawn` 시그니처(`Spawn(preset, slotIndex, slotCount)`)와 반환형(Character), `PartyManager.ClearParty()` 존재를 구현 시 확인(없으면 인접 배치/정리 로직을 실제 API에 맞춰 조정). 인접 보장이 어려우면 스폰 후 두 캐릭터의 `CurrentTile`을 직접 인접 타일로 지정.

- [ ] **Step 3: 컴파일 게이트** — Refresh + 에러 0.

- [ ] **Step 4: 플레이 검증 (임시 트리거)**

RunCommand로 `TutorialScenario.EnsureInstance().SpawnDemo()` 호출(플레이 중) → 전사·도적이 **인접 타일**에 서고 약체 몬스터 1이 뜨며 전투가 시작되는지 확인(콘솔 "Combat started! 1 monster(s)"). 몬스터 의도에 **색 타일 범위**가 표시되는지 확인. `Cleanup()` 호출 후 데모 파티/몬스터가 사라지는지 확인.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/Scripts/Core/Tutorial/TutorialScenario.cs" Assets/Scripts/Data/MonsterPresets/Tutorial
git commit -m "feat(tutorial): 고정 데모 셋업(전사·도적 인접 + 약체 타일공격 몬스터)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 6: 전투 단계(1~12) 정의 + Tutorial 상태 연결

**Files:**
- Modify: `Assets/Scripts/Core/Tutorial/TutorialScenario.cs` (BuildCombatSteps 추가)
- Modify: `Assets/Scripts/Core/GameFlowManager.cs` (EnterState(Tutorial) 실제 구현)

**Interfaces:**
- Consumes: Task 2~5 전부.
- Produces: `List<TutorialStep> TutorialScenario.BuildCombatSteps()`; `GameFlowManager.EnterState(Tutorial)`가 데모 스폰 + Director.Play(단계, onComplete=전투완료→다음).

- [ ] **Step 1: BuildCombatSteps 작성 (12단계)**

`TutorialScenario`에 추가. 각 단계는 스펙 시퀀스 표를 그대로 구현. 대상/조건 델리게이트는 런타임 참조:

```csharp
using DiceOrbit.UI.Tutorial;
// ...
public System.Collections.Generic.List<TutorialStep> BuildCombatSteps()
{
    var steps = new System.Collections.Generic.List<TutorialStep>();
    var cm = CombatManager.Instance;
    var dm = DiceManager.Instance;
    var actionUI = DiceOrbit.UI.CharacterActionUI.Instance;

    // 진행 플래그(이벤트 구독으로 세팅)
    bool moved = false, warriorSkill = false, rogueMoved = false, rogueSkill = false, ended = false, monsterActed = false;
    cm.OnPlayerMoved += c => { moved = true; if (c == Rogue) rogueMoved = true; };
    cm.OnPlayerSkillUsed += c => { if (c == Warrior) warriorSkill = true; if (c == Rogue) rogueSkill = true; };
    // 턴/몬스터행동: combatStatus 폴링으로 감지(아래 Done 람다에서).

    // 1. 인트로
    steps.Add(new TutorialStep("원형 궤도 위 캐릭터로 몬스터를 처치하세요. 시작해볼까요?"));
    // 2. 몬스터 의도 + 색 타일
    steps.Add(new TutorialStep("몬스터 머리 위 아이콘 = 다음 행동, 바닥의 색칠된 타일 = 그 공격이 닿는 범위예요. 그 위 캐릭터가 맞습니다.")
        { Target = () => MonsterIntentTarget() });
    // 3. 주사위 자동 굴림
    steps.Add(new TutorialStep("매 턴 주사위가 자동으로 굴려집니다. 이번 턴 자원이에요.")
        { Target = () => DiceHandTarget() });
    // 4. 전사 선택
    steps.Add(new TutorialStep("전사를 클릭하세요.")
        { Target = () => WorldTarget(Warrior), GateInput = true, Advance = TutorialAdvance.Custom,
          Done = () => actionUI != null && actionUI.IsShowingCharacter(Warrior) });
    // 5. 예산
    steps.Add(new TutorialStep("이동은 턴당 1번, 행동(스킬)도 턴당 1번만 가능해요.")
        { Target = () => ActionPanelTarget() });
    // 6. 전사 패시브 + 스킬
    steps.Add(new TutorialStep("전사 좌우에 아군이 있으면 공격 +50%! 지금 도적이 옆에 있죠. 주사위로 스킬을 써서 몬스터를 공격하세요.")
        { Target = () => ActionPanelTarget(), GateInput = true, Advance = TutorialAdvance.Custom,
          Done = () => warriorSkill });
    // 7. 도적 선택 + 멀리 이동
    steps.Add(new TutorialStep("이제 도적! 멀리 이동할수록 다음 공격이 강해져요(1칸당 +25%). 주사위로 멀리 이동해보세요.")
        { OnEnter = () => dm?.SetScriptedRoll(new[]{ 6, 6 }), // 큰 이동값 보장(격리: 데모 덱)
          Target = () => WorldTarget(Rogue), GateInput = true, Advance = TutorialAdvance.Custom,
          Done = () => rogueMoved });
    // 8. 도적 공격
    steps.Add(new TutorialStep("이동한 만큼 강해진 공격으로 몬스터를 타격하세요!")
        { Target = () => ActionPanelTarget(), GateInput = true, Advance = TutorialAdvance.Custom,
          Done = () => rogueSkill });
    // 9. 액티브 vs 패시브 정리
    steps.Add(new TutorialStep("방금 전사·도적 효과는 모두 '패시브' — 버튼 없이 조건 충족 시 자동 발동이에요. 스킬 버튼은 '액티브'!"));
    // 10. 턴 종료
    steps.Add(new TutorialStep("행동을 마쳤으면 [턴 종료]로 몬스터 턴으로 넘기세요.")
        { Target = () => EndTurnTarget(), GateInput = true, Advance = TutorialAdvance.Custom,
          Done = () => cm.combatStatus == CombatStatus.StartMonsterTurn || cm.combatStatus == CombatStatus.ExecuteMonsterTurn || cm.combatStatus == CombatStatus.EndMonsterTurn });
    // 11. 몬스터 턴 관전
    steps.Add(new TutorialStep("예고한 색 타일 범위로 몬스터가 공격합니다! (그 위 캐릭터가 피격)")
        { Target = () => MonsterIntentTarget(), Advance = TutorialAdvance.Custom,
          Done = () => cm.combatStatus == CombatStatus.StartPlayerTurn && cm.InCombat });
    // 12. 마무리·승리
    steps.Add(new TutorialStep("이제 마무리! 몬스터를 처치하세요.")
        { GateInput = false, Advance = TutorialAdvance.Custom,
          Done = () => !cm.InCombat }); // 전투 종료(승리)로 진행
    return steps;
}
```

타깃 헬퍼(같은 파일):

```csharp
private RectTransform WorldTarget(Character c) => ScreenBoxProvider.ForWorld(c?.transform);
private RectTransform MonsterIntentTarget() => ScreenBoxProvider.ForWorld(DemoMonster?.transform);
private RectTransform DiceHandTarget() => DiceOrbit.UI.DiceUI.Instance?.PanelRect;   // DiceUI에 PanelRect 노출 필요(없으면 캔버스 하단 고정 박스)
private RectTransform ActionPanelTarget() => DiceOrbit.UI.CharacterActionUI.Instance?.PanelRoot; // 노출 필요
private RectTransform EndTurnTarget() => DiceOrbit.UI.CombatUIRefs.EndTurnRect;      // 없으면 GameObject.Find로 대체
```

> **월드 오브젝트 하이라이트**: 캐릭터/몬스터는 RectTransform이 없으므로, 화면 박스를 RectTransform으로 변환하는 소형 헬퍼 `ScreenBoxProvider.ForWorld(Transform)`가 필요하다 — 오버레이 캔버스 아래 임시 RectTransform 하나를 만들어 매 프레임 대상 월드좌표→스크린좌표로 위치/크기를 맞춘다. Task 6 Step 2에서 이 헬퍼를 작성한다.
> **UI 대상 노출**: `DiceUI.PanelRect`, `CharacterActionUI.PanelRoot`, 턴종료 버튼 RectTransform은 `public` 게터가 없으면 최소한으로 추가(또는 `GameObject.Find`/이름 탐색). 각 노출은 해당 단계에서 필요.

- [ ] **Step 2: ScreenBoxProvider 헬퍼 작성**

`Assets/Scripts/UI/Tutorial/ScreenBoxProvider.cs`:

```csharp
using UnityEngine;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>월드 Transform을 오버레이용 RectTransform(스크린 박스)로 매 프레임 미러링.</summary>
    public class ScreenBoxProvider : MonoBehaviour
    {
        public Transform world;
        public Vector2 size = new Vector2(160, 160);
        private RectTransform _rt;

        public static RectTransform ForWorld(Transform world)
        {
            if (world == null) return null;
            var go = new GameObject("ScreenBox", typeof(RectTransform));
            var self = go.AddComponent<ScreenBoxProvider>();
            self.world = world;
            self._rt = (RectTransform)go.transform;
            var canvas = TutorialOverlayUI.EnsureInstance();
            go.transform.SetParent(canvas.transform, false);
            self.Update();
            return self._rt;
        }

        private void Update()
        {
            if (world == null || Camera.main == null) return;
            var sp = Camera.main.WorldToScreenPoint(world.position);
            _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0, 0);
            _rt.sizeDelta = size;
            _rt.anchoredPosition = new Vector2(sp.x - size.x * 0.5f, sp.y - size.y * 0.5f);
        }
    }
}
```

- [ ] **Step 3: EnterState(Tutorial) 실제 구현**

`GameFlowManager.EnterState`의 Task 1 스텁을 교체:

```csharp
                case GameState.Tutorial:
                    if (combatUI != null) combatUI.SetActive(true);
                    var scenario = Core.Tutorial.TutorialScenario.EnsureInstance();
                    var director = UI.Tutorial.TutorialDirector.EnsureInstance();
                    director.TutorialSkipHandler = () => FinishTutorialToRun();
                    scenario.SpawnDemo();
                    director.Play(scenario.BuildCombatSteps(), onComplete: () => OnTutorialCombatDone());
                    break;
```

보조 메서드(같은 클래스):

```csharp
        private void OnTutorialCombatDone()
        {
            // 전투 12단계 완료 → 데모 정리 → 실제 런 개시(Recruit). step 13은 Recruit 상태에서 이어짐(Task 7).
            Core.Tutorial.TutorialScenario.Instance?.Cleanup();
            _tutorialAwaitingRecruit = true;   // Task 7에서 사용
            StartGameInternal();               // 실제 런 시작 → Recruit
        }

        private void FinishTutorialToRun()
        {
            PlayerPrefs.SetInt("tutorial_done", 1); PlayerPrefs.Save();
            Core.Tutorial.TutorialScenario.Instance?.Cleanup();
            StartGameInternal();
        }
```

`private bool _tutorialAwaitingRecruit;` 필드 추가.

- [ ] **Step 4: 컴파일 게이트** — Refresh + 에러 0.

- [ ] **Step 5: 플레이 검증 (전투 튜토리얼 전체)**

플레이 → 게임 시작 → 예 → 데모 전투에서 단계 1~12가 순서대로 진행되는지 수동 확인:
- 하이라이트가 올바른 대상(몬스터/주사위/전사/액션패널/도적/턴종료)에 뜨는지.
- 전사 스킬 시 +50%, 도적 멀리 이동 후 +이동보너스 적용되는지(피해 로그).
- 몬스터 턴에서 색 타일 범위 공격.
- 승리 후 데모 정리 + Recruit 진입(step 13 없이도 여기까지). 스크린샷 몇 장 기록.

- [ ] **Step 6: 커밋**

```bash
git add "Assets/Scripts/Core/Tutorial/TutorialScenario.cs" "Assets/Scripts/UI/Tutorial/ScreenBoxProvider.cs" Assets/Scripts/Core/GameFlowManager.cs
git commit -m "feat(tutorial): 전투 12단계 시퀀스 + Tutorial 상태 연결

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 7: Step 13 (첫 캐릭터 선택 안내) + 완료 처리

**Files:**
- Modify: `Assets/Scripts/UI/CharacterSelectionUI.cs` (남은 선택 수 노출)
- Modify: `Assets/Scripts/Core/GameFlowManager.cs` (Recruit 진입 시 안내 재생)

**Interfaces:**
- Consumes: Task 3(Director), Task 6(`_tutorialAwaitingRecruit`).
- Produces: `CharacterSelectionUI`에 `public int RemainingToSelect { get; }`, `public RectTransform CardContainerRect { get; }`; GameFlow가 Recruit 진입 시 step 13을 Director로 재생 후 `tutorial_done=1`.

- [ ] **Step 1: CharacterSelectionUI 노출 프로퍼티 추가**

```csharp
        public int RemainingToSelect => Mathf.Max(0, sessionTargetCount - selectedCount);
        public RectTransform CardContainerRect => cardContainer as RectTransform;
```

- [ ] **Step 2: Recruit 진입 시 step 13 재생**

`GameFlowManager.EnterState`의 `case GameState.Recruit:` 끝에서(기존 characterSelectionUI.Show() 뒤) 튜토리얼 대기 중이면 안내 단계 재생:

```csharp
                case GameState.Recruit:
                    Debug.Log("Enter Recruit State");
                    if (characterSelectionUI != null) characterSelectionUI.Show();
                    if (_tutorialAwaitingRecruit)
                    {
                        _tutorialAwaitingRecruit = false;
                        var director = UI.Tutorial.TutorialDirector.EnsureInstance();
                        var csu = characterSelectionUI;
                        var step13 = new System.Collections.Generic.List<UI.Tutorial.TutorialStep>
                        {
                            new UI.Tutorial.TutorialStep("여기서 파티에 넣을 캐릭터 2명을 고르세요. 각 캐릭터는 액티브 스킬과 패시브를 가져요 (방금 배운 것처럼).")
                            {
                                Target = () => csu != null ? csu.CardContainerRect : null,
                                Advance = UI.Tutorial.TutorialAdvance.Custom,
                                Done = () => csu == null || csu.RemainingToSelect == 0,
                                GateInput = false   // 선택 UI는 자유 조작 허용
                            }
                        };
                        director.Play(step13, onComplete: () =>
                        {
                            PlayerPrefs.SetInt("tutorial_done", 1); PlayerPrefs.Save();
                            Debug.Log("[Tutorial] 완료 — 이후 정상 진행");
                        });
                    }
                    break;
```

- [ ] **Step 3: 컴파일 게이트** — Refresh + 에러 0.

- [ ] **Step 4: 플레이 검증 (2부 연결)**

전투 튜토리얼 승리 → Recruit 진입 시 "캐릭터 2명 고르기" 안내가 카드 컨테이너를 하이라이트하며 뜨는지, 2명 선택 완료 시 안내가 사라지고 `tutorial_done=1` 로그가 뜨는지, 이후 Map 정상 진입하는지 확인.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/UI/CharacterSelectionUI.cs Assets/Scripts/Core/GameFlowManager.cs
git commit -m "feat(tutorial): 첫 캐릭터 선택(2명) 안내(step 13) + 완료 플래그

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 8: 격리 검증 + 스킵 + 마감

**Files:**
- Modify: (필요 시) `Assets/Scripts/Core/GameFlowManager.cs`, `TutorialScenario.cs`

**Interfaces:**
- Consumes: 전체.

- [ ] **Step 1: 스킵 경로 확인**

오버레이 "튜토리얼 스킵" → `TutorialDirector.OnSkipRequested` → `TutorialSkipHandler`(=`FinishTutorialToRun`) → 데모 정리 + `tutorial_done=1` + 실제 런. 어느 단계에서 스킵해도 데모 파티/몬스터가 남지 않는지 확인.

- [ ] **Step 2: 격리 검증 (RunCommand)**

튜토리얼 완주(또는 스킵) 직후 다음을 로그로 확인:
- `RunManager.Instance.RunActive`가 튜토리얼 동안 false였는지(튜토리얼은 RunManager를 시작하지 않음), 실제 런은 정상 시작.
- `RunSaveService.HasSave()`가 튜토리얼만으로는 세이브를 만들지 않는지(실제 런 Map 진입 시부터 저장).
- `DiceDeckManager`의 덱이 튜토리얼로 인해 영구 변경되지 않았는지(교체/부여 없음).
- 데모 파티/몬스터 오브젝트가 실제 런 시작 시 남아있지 않은지.

```csharp
// RunCommand 요지: 각 항목을 검사해 위반 시 result.LogError, 통과 시 result.Log("[격리 OK] ...").
```

- [ ] **Step 3: 전체 플레이스루 검증**

메인메뉴 → 게임 시작 → 예 → 전투 튜토리얼(1~12) → Recruit 안내(13) → 2명 선택 → Map. 그리고 재시작 시 프롬프트 미표시(완료 플래그). "아니오" 경로도 정상. 스크린샷 기록.

- [ ] **Step 4: 커밋**

```bash
git add -A
git commit -m "feat(tutorial): 격리 검증 + 스킵 경로 마감

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review (계획 작성자 체크)

- **스펙 커버리지**: 진입 프롬프트(T1) · 격리(T5 Cleanup·T8) · 데모 셋업(T5) · 오버레이/입력잠금(T2) · 프레임워크(T3) · 진행조건 훅(T4) · 12단계(T6) · step13 캐릭터선택(T7) · 스킵(T2/T3/T8) — 스펙 항목 전부 태스크 매핑됨.
- **미해결 확인 필요(구현 시)**: ① 약체 **타일공격** 몬스터 프리셋 존재 여부(없으면 T5에서 신규) ② `CharacterSpawner.Spawn` 시그니처/반환형과 **인접 배치** 보장 ③ `PartyManager.ClearParty`/`OrbitManager.TileCount` 존재 ④ `DiceUI.PanelRect`/`CharacterActionUI.PanelRoot`/턴종료 버튼 RectTransform **public 노출**(없으면 최소 추가). 각 태스크 검증 단계에서 확정.
- **타입 일관성**: `TutorialAdvance`/`TutorialStep.Done`/`OnPlayerMoved`/`OnPlayerSkillUsed`/`SetScriptedRoll`/`EnsureInstance` 명칭 태스크 간 일치.
- **YAGNI**: 단계 데이터는 코드 정의(SO 미도입), 오버레이는 4분할 딤(셰이더 마스크 미사용) — 최소 구현.
