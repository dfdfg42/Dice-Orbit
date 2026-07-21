# 전투 정보 패널 (Battle Info Panel) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 화면 오른쪽 1/3 상시 정보 패널(선택/호버 대상 1명의 HP/패시브/액티브/상태이상/밟은 타일 카드)을 추가하고, 기존 호버 툴팁을 경량 경로(`ShowPinned(string)`)만 남기고 철거한다.

**Architecture:** 신규 구조화 데이터 모델(`UnitInfoData` + `IBattleInfoProvider`)을 `UnitInfoBuilder` 한 곳에서 빌드하고, `BattleInfoPanelUI`가 런타임 프로그래매틱 UI로 렌더링한다. 기존 `HoverTooltipData` blob 경로·글로서리 컨테이너는 완전 삭제. 스펙: `Docs/superpowers/specs/2026-07-05-battle-info-panel-design.md`

**Tech Stack:** Unity 6 (URP), TextMeshPro, New Input System (`Mouse.current`), C# (Assembly-CSharp, asmdef 없음)

## Global Constraints

- **테스트 전략**: 프로젝트에 Unity Test Framework 인프라 없음(게임 코드 asmdef 부재). 각 태스크의 검증 = ①Unity 에디터 포커스 후 콘솔 **컴파일 에러 0** 확인 ②명시된 플레이 모드 관찰. 검증 통과 전 커밋 금지.
- **서식 제로 원칙**: Provider/Builder는 원시 숫자·원문만. 색/리치텍스트/"Lv." 접두어/아이콘 해석은 패널 렌더 계층 전용.
- **네임스페이스**: UI 코드 `DiceOrbit.UI`, 신규 파일은 `Assets/Scripts/UI/InfoPanel/` 폴더.
- **입력**: `UnityEngine.InputSystem.Mouse.current` 사용 (레거시 `Input` 폴백은 기존 패턴 유지 시에만).
- **에디터 수작업 단계**는 `[EDITOR]` 표시 — 코드로 불가능한 씬/에셋 작업. 실행자가 사람에게 요청하거나 지시대로 수행.
- 커밋 메시지 끝: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

---

### Task 1: 데이터 모델 + UnitInfoBuilder

**Files:**
- Create: `Assets/Scripts/UI/InfoPanel/BattleInfoData.cs`
- Create: `Assets/Scripts/UI/InfoPanel/UnitInfoBuilder.cs`

**Interfaces:**
- Consumes: `TooltipKeywordFormatter.StatusDisplayData`, `BuildStatusDisplayData(string,int,int)` (기존), `Character.Stats`(CharacterName/CurrentHP/MaxHP/TempArmor/SourcePreset/ActiveAbilities), `Monster.Stats`(MonsterName 등), `Unit.Passives.ActivePassives`(PassiveName/CurrentLevel/Description/GetDynamicDescription), `Unit.StatusEffects.GetActiveEffects()`, `Character.CurrentTile`, `TileData.GetAttributes()/TileIndex`
- Produces: `UnitInfoData`, `SkillInfoData`, `PassiveInfoData`, `TileInfoData`, `TileAttributeInfo`, `IBattleInfoProvider`, `UnitInfoBuilder.Build(Character)`, `UnitInfoBuilder.Build(Monster)`, `UnitInfoBuilder.BuildTileInfo(TileData)` — 이후 모든 태스크가 이 시그니처에 의존

- [ ] **Step 1: BattleInfoData.cs 작성**

```csharp
using System.Collections.Generic;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.UI
{
    /// <summary>정보 패널이 소비하는 유닛 1명의 구조화 데이터. 서식 없음(원시값만).</summary>
    public readonly struct UnitInfoData
    {
        public readonly string Name;
        public readonly int CurrentHp, MaxHp, Armor;
        public readonly string FlavorText;                                   // 프로필 원문 (서식 없음)
        public readonly IReadOnlyList<SkillInfoData> Actives;
        public readonly IReadOnlyList<PassiveInfoData> Passives;
        public readonly IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData> Statuses;
        public readonly TileInfoData? CurrentTile;                            // 몬스터는 null

        public UnitInfoData(string name, int currentHp, int maxHp, int armor, string flavorText,
            IReadOnlyList<SkillInfoData> actives, IReadOnlyList<PassiveInfoData> passives,
            IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData> statuses, TileInfoData? currentTile)
        {
            Name = name; CurrentHp = currentHp; MaxHp = maxHp; Armor = armor; FlavorText = flavorText;
            Actives = actives; Passives = passives; Statuses = statuses; CurrentTile = currentTile;
        }
    }

    public readonly struct SkillInfoData
    {
        public readonly string Name;
        public readonly string DiceCondition;       // 예: "주사위 4 이상" (requirement.GetDescription())
        public readonly string DynamicDescription;  // GetDynamicDescription() 결과 원문
        public readonly int Level;
        public SkillInfoData(string name, string diceCondition, string dynamicDescription, int level)
        { Name = name; DiceCondition = diceCondition; DynamicDescription = dynamicDescription; Level = level; }
    }

    public readonly struct PassiveInfoData
    {
        public readonly string Name;
        public readonly int Level;
        public readonly string DynamicEffect;       // GetDynamicDescription() — 현재 유효 수치
        public readonly string FlavorText;          // Description 원문
        public PassiveInfoData(string name, int level, string dynamicEffect, string flavorText)
        { Name = name; Level = level; DynamicEffect = dynamicEffect; FlavorText = flavorText; }
    }

    public readonly struct TileInfoData
    {
        public readonly int TileIndex;
        public readonly Core.TileType Type;
        public readonly IReadOnlyList<TileAttributeInfo> Attributes;
        public TileInfoData(int tileIndex, Core.TileType type, IReadOnlyList<TileAttributeInfo> attributes)
        { TileIndex = tileIndex; Type = type; Attributes = attributes; }
    }

    public readonly struct TileAttributeInfo
    {
        public readonly TileAttributeType Type;
        public readonly int Value;
        public readonly int Duration;               // -1 = 영구
        public TileAttributeInfo(TileAttributeType type, int value, int duration)
        { Type = type; Value = value; Duration = duration; }
    }

    /// <summary>정보 패널에 자신을 표시할 수 있는 유닛. Character/Monster가 구현.</summary>
    public interface IBattleInfoProvider
    {
        UnitInfoData GetBattleInfo();
    }
}
```

주의: `TileType`의 실제 네임스페이스는 `TileData.cs` 상단에서 확인 후 using 조정 (파일 위치 `BattleStageSystem/Tile/TileData.cs`, 예상 `DiceOrbit.Core`).

- [ ] **Step 2: UnitInfoBuilder.cs 작성**

```csharp
using System.Collections.Generic;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    /// <summary>
    /// Character/Monster/TileData → 패널용 구조화 데이터 빌더 (단일 출처).
    /// 기존 Character/Monster에 복붙돼 있던 상태이상·패시브 빌드 로직을 통합한다.
    /// 서식 제로: 여기서 색/태그/접두어를 붙이지 않는다.
    /// </summary>
    public static class UnitInfoBuilder
    {
        public static UnitInfoData Build(Character ch)
        {
            var s = ch.Stats;
            return new UnitInfoData(
                name: !string.IsNullOrWhiteSpace(s?.CharacterName) ? s.CharacterName : ch.name,
                currentHp: s?.CurrentHP ?? 0, maxHp: s?.MaxHP ?? 0, armor: s?.TempArmor ?? 0,
                flavorText: s?.SourcePreset != null ? (s.SourcePreset.Description ?? "").Trim() : "",
                actives: BuildActives(s),
                passives: BuildPassives(ch.Passives),
                statuses: BuildStatuses(ch.StatusEffects),
                currentTile: ch.CurrentTile != null ? BuildTileInfo(ch.CurrentTile) : (TileInfoData?)null);
        }

        public static UnitInfoData Build(Monster m)
        {
            var s = m.Stats;
            return new UnitInfoData(
                name: !string.IsNullOrWhiteSpace(s?.MonsterName) ? s.MonsterName : m.name,
                currentHp: s?.CurrentHP ?? 0, maxHp: s?.MaxHP ?? 0, armor: s?.TempArmor ?? 0,
                flavorText: "",
                actives: System.Array.Empty<SkillInfoData>(),
                passives: BuildPassives(m.Passives),
                statuses: BuildStatuses(m.StatusEffects),
                currentTile: null);   // 몬스터는 타일 추적 없음 (Character.CurrentTile만 존재)
        }

        public static TileInfoData BuildTileInfo(TileData tile)
        {
            var list = new List<TileAttributeInfo>();
            foreach (var attr in tile.GetAttributes())
                if (attr != null) list.Add(new TileAttributeInfo(attr.Type, attr.Value, attr.Duration));
            return new TileInfoData(tile.TileIndex, tile.Type, list);
        }

        private static IReadOnlyList<SkillInfoData> BuildActives(Data.CharacterStats stats)
        {
            var result = new List<SkillInfoData>();
            var slots = stats?.ActiveAbilities;
            if (slots == null) return result;
            foreach (var slot in slots)
            {
                var skill = slot?.RuntimeInstance ?? slot?.BaseSkill;
                if (skill == null) continue;
                result.Add(new SkillInfoData(
                    skill.SkillName,
                    skill.requirement?.GetDescription() ?? "",
                    skill.GetDynamicDescription() ?? "",
                    slot.CurrentLevel));
            }
            return result;
        }

        private static IReadOnlyList<PassiveInfoData> BuildPassives(PassiveManager passives)
        {
            var result = new List<PassiveInfoData>();
            if (passives?.ActivePassives == null) return result;
            foreach (var p in passives.ActivePassives)
            {
                if (p == null) continue;
                result.Add(new PassiveInfoData(
                    string.IsNullOrWhiteSpace(p.PassiveName) ? "Unknown Passive" : p.PassiveName,
                    p.CurrentLevel,
                    p.GetDynamicDescription() ?? "",
                    (p.Description ?? "").Trim()));
            }
            return result;
        }

        private static IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData> BuildStatuses(
            Systems.Effects.StatusEffectManager statusEffects)
        {
            var result = new List<TooltipKeywordFormatter.StatusDisplayData>();
            var effects = statusEffects?.GetActiveEffects();
            if (effects == null) return result;
            foreach (var e in effects)
            {
                if (e == null) continue;
                result.Add(TooltipKeywordFormatter.BuildStatusDisplayData(e.Type.ToString(), e.Value, e.Duration));
            }
            return result;
        }
    }
}
```

주의(실행자 확인 사항 — 컴파일 에러 나면 실제 선언부를 열어 맞출 것):
- `PassiveManager`/`ActivePassives`의 정확한 네임스페이스·요소 타입은 `Character.cs:519-548`의 기존 `BuildPassiveTooltipData` 참고.
- `StatusEffectManager.GetActiveEffects()` 요소의 `Type/Value/Duration` 필드명은 `Monster.cs:414-427` 참고.
- `slot.CurrentLevel`, `skill.requirement.GetDescription()`은 `SkillCardUI` 삭제 전 코드와 `RuntimeAbility.cs`에서 검증된 API.
- `TileData.Type` 프로퍼티는 아직 없음 — **Task 2 Step 3에서 추가**되므로 이 시점 컴파일 에러가 나면 Task 2를 먼저 완료 후 함께 검증해도 됨 (같은 커밋 금지, 순서만 조정).

- [ ] **Step 3: 컴파일 확인** — Unity 에디터 포커스 → 콘솔 에러 0 (`TileData.Type` 관련 에러 1건은 Task 2에서 해소 예정이면 Step 4 커밋을 Task 2와 순서 조정)

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/UI/InfoPanel/
git commit -m "feat: 정보 패널용 구조화 데이터 모델 + UnitInfoBuilder 추가"
```

---

### Task 2: Provider 구현 (Character / Monster / TileData)

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Character.cs` (클래스 선언부 + `GetHoverTooltipData` 근처, ~490행)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/Monster.cs` (클래스 선언부 15행 + ~382행)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileData.cs` (~45행, ~172행)

**Interfaces:**
- Consumes: Task 1의 `IBattleInfoProvider`, `UnitInfoBuilder.Build(...)`, `UnitInfoBuilder.BuildTileInfo(...)`
- Produces: `Character : IBattleInfoProvider`, `Monster : IBattleInfoProvider` (각 `GetBattleInfo()`), `TileData.GetTileInfo()` → `TileInfoData`, `TileData.Type` → `TileType` — Task 4/5가 의존

- [ ] **Step 1: Character에 구현 추가** — 클래스 선언에 `, UI.IBattleInfoProvider` 추가, 본문에:

```csharp
public UI.UnitInfoData GetBattleInfo() => UI.UnitInfoBuilder.Build(this);
```

기존 `IHoverTooltipProvider` 구현은 **아직 지우지 않는다** (Task 5에서 철거 — 병행 기간 동안 게임 동작 유지).

- [ ] **Step 2: Monster에 동일 추가**

```csharp
public UI.UnitInfoData GetBattleInfo() => UI.UnitInfoBuilder.Build(this);
```

- [ ] **Step 3: TileData에 Type 프로퍼티 + GetTileInfo 추가** — 기존 `ResolveTileTypeName()`(TileData.cs:177-181, index 0 = LevelUp)과 동일 규칙:

```csharp
/// <summary>타일 타입. 현재 규칙: 0번 타일 = LevelUp, 나머지 = Normal (ResolveTileTypeName과 동일).</summary>
public TileType Type => tileIndex == 0 ? TileType.LevelUp : TileType.Normal;

public UI.TileInfoData GetTileInfo() => UI.UnitInfoBuilder.BuildTileInfo(this);
```

- [ ] **Step 4: 컴파일 확인** — 콘솔 에러 0 (Task 1의 보류 에러도 함께 해소 확인)

- [ ] **Step 5: 플레이 모드 스모크** — BattleScene 플레이 진입만 확인 (동작 변화 없어야 정상)

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Core/
git commit -m "feat: Character/Monster/TileData에 IBattleInfoProvider 경로 구현"
```

---

### Task 3: TileAttributeVisualDatabase에 description 확장

**Files:**
- Modify: `Assets/Scripts/UI/TileAttributeVisualDatabase.cs:12-19` (Entry 클래스)

**Interfaces:**
- Produces: `TileAttributeVisualDatabase.Entry.description` (string) — Task 4의 TileCardSection이 조회

- [ ] **Step 1: Entry에 필드 추가**

```csharp
[Serializable]
public class Entry
{
    public TileAttributeType type;
    public Sprite icon;
    public Color iconTint = Color.white;
    public string shortLabel;
    [TextArea(2, 4)] public string description;   // 패널 타일 속성 행에 표시할 설명
}
```

- [ ] **Step 2: 컴파일 확인** — 에러 0. SerializedObject 하위호환: 필드 추가는 기존 에셋 데이터 보존됨.

- [ ] **Step 3: [EDITOR] 에셋에 설명 채우기** — 프로젝트 창에서 `TileAttributeVisualDatabase` 에셋(검색: `t:TileAttributeVisualDatabase`) 선택 → 각 entry의 description에 속성 설명 입력 (Honey/Reagent/Bone/SnowPrison/RandMine/Cloud/ScoutHeal/LevelUp). 비워두면 패널에서 설명 줄 생략되므로 커밋은 코드만으로도 가능.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/UI/TileAttributeVisualDatabase.cs
git commit -m "feat: 타일 속성 시각 DB에 description 필드 추가"
```

---

### Task 4: BattleInfoPanelUI — 패널 본체 + 선택 컨트롤러 + 섹션 렌더링

**Files:**
- Create: `Assets/Scripts/UI/InfoPanel/BattleInfoPanelUI.cs` (오케스트레이터 + 프로그래매틱 레이아웃)
- Create: `Assets/Scripts/UI/InfoPanel/InfoPanelSelectionController.cs` (대상 결정 규칙)
- Create: `Assets/Scripts/UI/InfoPanel/InfoPanelRows.cs` (행/카드 생성 헬퍼)

**Interfaces:**
- Consumes: Task 1/2의 `IBattleInfoProvider.GetBattleInfo()`, `TileData.GetTileInfo()`, `TileAttributeVisualDatabase.TryGet(type, out Entry)`(+`entry.description`), `TooltipKeywordFormatter.ExtractMatches(string)` → `List<KeywordDisplayData>`, `SkillTargetSelector.Instance.IsSelectingTarget`
- Produces: `BattleInfoPanelUI.Instance` (싱글턴), `BattleInfoPanelUI.EnsureInstance()` — 씬 부트스트랩이 호출

**설계 결정**: 씬/프리팹 수작업 의존을 없애기 위해 UI 계층을 **코드로 생성**한다 (프로젝트에 이미 런타임 `AddComponent` 패턴 다수 — SkillTargetSelector의 LineRenderer, CharacterActionUI의 SkillPreviewHoverUI). 스킨(색/폰트/스프라이트)은 `[SerializeField]`로 나중에 얹을 수 있게 남긴다.

- [ ] **Step 1: InfoPanelRows.cs 작성** — TMP 행 생성 유틸 (모든 섹션이 공유)

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>정보 패널 내부 행(제목/본문/카드) 생성 헬퍼. 스타일 상수의 단일 출처.</summary>
    internal static class InfoPanelRows
    {
        public static readonly Color SectionTitleColor = new Color(1f, 0.85f, 0.55f);
        public static readonly Color PassiveColor      = new Color(1f, 0.6f, 0.4f);   // 기존 툴팁 관례 계승
        public static readonly Color MutedColor        = new Color(0.7f, 0.7f, 0.7f);

        public static TextMeshProUGUI AddText(Transform parent, string text, float size,
            Color color, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.color = color; tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        public static void AddSectionTitle(Transform parent, string title)
            => AddText(parent, title, 20f, SectionTitleColor, FontStyles.Bold);

        public static void AddDivider(Transform parent)
        {
            var go = new GameObject("Divider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.15f); img.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 2f; le.preferredHeight = 2f; le.flexibleWidth = 1f;
        }

        public static void Clear(Transform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.Destroy(content.GetChild(i).gameObject);
        }
    }
}
```

(주의: TMP 버전에 따라 `textWrappingMode`가 없으면 `enableWordWrapping = true`로 대체.)

- [ ] **Step 2: InfoPanelSelectionController.cs 작성** — 스펙 §6 규칙 그대로

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 패널에 표시할 대상 결정 (스펙 §6):
    /// 평시: 호버 즉시 갱신 + 클릭 고정(핀), 핀 중 타 대상 호버는 임시 표시.
    /// 타게팅 모드: 호버 추적만, 클릭 고정 비활성 (클릭 = 스킬 대상 지정).
    /// 빈 타일 직접 호버: 타일 단독 뷰.
    /// 이 컴포넌트는 클릭을 소비하지 않는다(읽기 전용) — 기존 클릭 동작과 충돌 없음.
    /// </summary>
    public class InfoPanelSelectionController : MonoBehaviour
    {
        private Camera _cachedCamera;

        public IBattleInfoProvider PinnedUnit   { get; private set; }
        public IBattleInfoProvider HoveredUnit  { get; private set; }
        public TileData            HoveredTile  { get; private set; }   // 유닛 없이 타일만 호버

        /// <summary>패널이 렌더링해야 할 현재 유닛 (호버 우선, 없으면 핀).</summary>
        public IBattleInfoProvider CurrentUnit => HoveredUnit ?? PinnedUnit;

        private void Update()
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            UpdateHover(overUI);
            UpdatePin(overUI);
        }

        private void UpdateHover(bool overUI)
        {
            HoveredUnit = null; HoveredTile = null;
            if (overUI) return;

            var cam = GetCamera();
            if (cam == null || Mouse.current == null) return;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

            HoveredUnit = hit.collider.GetComponentInParent<IBattleInfoProvider>();
            if (HoveredUnit == null)
                HoveredTile = hit.collider.GetComponentInParent<TileData>();
        }

        private void UpdatePin(bool overUI)
        {
            if (Mouse.current == null) return;

            // 타게팅 모드: 클릭은 SkillTargetSelector 소유 — 핀 조작 금지 (스펙 §6)
            bool targeting = SkillTargetSelector.Instance != null && SkillTargetSelector.Instance.IsSelectingTarget;
            if (targeting) return;

            if (Mouse.current.leftButton.wasPressedThisFrame && !overUI)
            {
                if (HoveredUnit != null)
                    PinnedUnit = ReferenceEquals(PinnedUnit, HoveredUnit) ? null : HoveredUnit; // 재클릭 = 해제
            }
            if (Mouse.current.rightButton.wasPressedThisFrame)
                PinnedUnit = null;

            // 핀 대상이 죽어 파괴된 경우 정리 (UnityEngine.Object 널 체크)
            if (PinnedUnit is Component c && c == null) PinnedUnit = null;
        }

        private Camera GetCamera()
        {
            if (_cachedCamera != null && _cachedCamera.isActiveAndEnabled) return _cachedCamera;
            _cachedCamera = Camera.main;
            if (_cachedCamera == null) _cachedCamera = FindFirstObjectByType<Camera>();
            return _cachedCamera;
        }
    }
}
```

- [ ] **Step 3: BattleInfoPanelUI.cs 작성** — 캔버스/스크롤 프로그래매틱 생성 + 섹션 렌더링 + 0.5s 리프레시

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 화면 오른쪽 1/3 상시 정보 패널 (스펙 Docs/superpowers/specs/2026-07-05-battle-info-panel-design.md).
    /// UI 계층을 코드로 생성한다. 표시 대상은 InfoPanelSelectionController가 결정.
    /// </summary>
    public class BattleInfoPanelUI : MonoBehaviour
    {
        public static BattleInfoPanelUI Instance { get; private set; }

        [Header("스킨 (선택)")]
        [SerializeField] private Sprite normalTileCardSprite;
        [SerializeField] private Sprite levelUpTileCardSprite;
        [SerializeField] private TileAttributeVisualDatabase attributeVisuals;
        [SerializeField, Range(0f, 1f)] private float panelWidthRatio = 0.30f;
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.11f, 0.96f);
        [SerializeField] private float refreshInterval = 0.5f;

        private InfoPanelSelectionController _selection;
        private RectTransform _content;         // 스크롤 내용 (섹션들이 붙는 곳)
        private float _nextRefresh;
        private object _lastTargetKey;          // 대상 변경 감지용

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            Instance = FindFirstObjectByType<BattleInfoPanelUI>();
            if (Instance == null)
            {
                var go = new GameObject("BattleInfoPanelUI");
                Instance = go.AddComponent<BattleInfoPanelUI>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _selection = gameObject.AddComponent<InfoPanelSelectionController>();
            BuildLayout();
            if (attributeVisuals == null)
                attributeVisuals = Resources.Load<TileAttributeVisualDatabase>("UI/TileAttributeVisualDatabase");
        }

        private void Update()
        {
            object key = (object)_selection.CurrentUnit ?? _selection.HoveredTile;
            bool targetChanged = !ReferenceEquals(key, _lastTargetKey);
            if (!targetChanged && Time.unscaledTime < _nextRefresh) return;

            _lastTargetKey = key;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Render();
        }

        // ── 레이아웃 생성 ─────────────────────────────────────────
        private void BuildLayout()
        {
            var canvasGo = new GameObject("InfoPanelCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;                       // 툴팁(30000)보다 아래, 일반 UI보다 위
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // 오른쪽 도킹 패널 (앵커로 폭 비율 고정)
            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(1f - panelWidthRatio, 0f);
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
            var bg = panelGo.AddComponent<Image>();
            bg.color = backgroundColor;                      // raycastTarget=true 유지 → 패널 위 3D 호버 차단

            // 스크롤 영역
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(panelGo.transform, false);
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.anchorMin = Vector2.zero; scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(16f, 16f); scrollRect.offsetMax = new Vector2(-16f, -16f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scrollGo.AddComponent<RectMask2D>();
            scroll.horizontal = false;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            _content = (RectTransform)contentGo.transform;
            _content.anchorMin = new Vector2(0f, 1f); _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero; _content.offsetMax = Vector2.zero;
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f; layout.childForceExpandHeight = false; layout.childControlHeight = true;
            layout.childControlWidth = true;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _content;
        }

        // ── 렌더링 ────────────────────────────────────────────────
        private void Render()
        {
            InfoPanelRows.Clear(_content);

            var unit = _selection.CurrentUnit;
            if (unit is Component c && c == null) unit = null;   // 파괴된 유닛 방어

            if (unit != null)         { RenderUnit(unit.GetBattleInfo()); return; }
            if (_selection.HoveredTile != null) { RenderTileSection(_selection.HoveredTile.GetTileInfo(), standalone: true); return; }

            InfoPanelRows.AddText(_content, "캐릭터나 몬스터에 마우스를 올리거나\n클릭해 고정하세요.", 16f, InfoPanelRows.MutedColor);
        }

        private void RenderUnit(UnitInfoData d)
        {
            // Header
            InfoPanelRows.AddText(_content, d.Name, 26f, Color.white, FontStyles.Bold);
            string hpLine = $"HP {d.CurrentHp}/{d.MaxHp}" + (d.Armor > 0 ? $"   방어도 {d.Armor}" : "");
            InfoPanelRows.AddText(_content, hpLine, 18f, new Color(0.95f, 0.5f, 0.5f));
            if (!string.IsNullOrWhiteSpace(d.FlavorText))
                InfoPanelRows.AddText(_content, d.FlavorText, 14f, InfoPanelRows.MutedColor, FontStyles.Italic);
            InfoPanelRows.AddDivider(_content);

            // Actives
            if (d.Actives != null && d.Actives.Count > 0)
            {
                InfoPanelRows.AddSectionTitle(_content, "액티브");
                foreach (var a in d.Actives)
                {
                    string title = a.Level > 1 ? $"{a.Name}  Lv.{a.Level}" : a.Name;   // "Lv." 접두어는 렌더 계층 담당
                    InfoPanelRows.AddText(_content, title, 17f, Color.white, FontStyles.Bold);
                    if (!string.IsNullOrWhiteSpace(a.DiceCondition))
                        InfoPanelRows.AddText(_content, a.DiceCondition, 14f, new Color(0.62f, 0.9f, 1f));
                    if (!string.IsNullOrWhiteSpace(a.DynamicDescription))
                        InfoPanelRows.AddText(_content, a.DynamicDescription, 14f, InfoPanelRows.MutedColor);
                }
                InfoPanelRows.AddDivider(_content);
            }

            // Passives
            if (d.Passives != null && d.Passives.Count > 0)
            {
                InfoPanelRows.AddSectionTitle(_content, "패시브");
                foreach (var p in d.Passives)
                {
                    string title = p.Level > 0 ? $"{p.Name}  Lv.{p.Level}" : p.Name;
                    InfoPanelRows.AddText(_content, title, 17f, InfoPanelRows.PassiveColor, FontStyles.Bold);
                    if (!string.IsNullOrWhiteSpace(p.DynamicEffect))
                        InfoPanelRows.AddText(_content, p.DynamicEffect, 14f, Color.white);
                    if (!string.IsNullOrWhiteSpace(p.FlavorText))
                        InfoPanelRows.AddText(_content, p.FlavorText, 13f, InfoPanelRows.MutedColor);
                }
                InfoPanelRows.AddDivider(_content);
            }

            // Statuses
            if (d.Statuses != null && d.Statuses.Count > 0)
            {
                InfoPanelRows.AddSectionTitle(_content, "상태이상");
                foreach (var s in d.Statuses)
                {
                    string meta = $"{s.StackText} {s.DurationText}".Trim();
                    InfoPanelRows.AddText(_content, meta.Length > 0 ? $"{s.Name}  {meta}" : s.Name, 16f, s.Color, FontStyles.Bold);
                    if (!string.IsNullOrWhiteSpace(s.Description))
                        InfoPanelRows.AddText(_content, s.Description, 13f, InfoPanelRows.MutedColor);
                }
                InfoPanelRows.AddDivider(_content);
            }

            // Tile
            if (d.CurrentTile.HasValue)
                RenderTileSection(d.CurrentTile.Value, standalone: false);

            // Keywords (본문들에서 추출)
            RenderKeywords(d);
        }

        private void RenderTileSection(TileInfoData t, bool standalone)
        {
            InfoPanelRows.AddSectionTitle(_content, standalone ? $"타일 #{t.TileIndex}" : "밟고 있는 타일");

            // 타일 카드 이미지 (스프라이트 미지정 시 생략)
            var sprite = t.Type == TileType.LevelUp ? levelUpTileCardSprite : normalTileCardSprite;
            if (sprite != null)
            {
                var cardGo = new GameObject("TileCard", typeof(RectTransform));
                cardGo.transform.SetParent(_content, false);
                var img = cardGo.AddComponent<Image>();
                img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
                cardGo.AddComponent<LayoutElement>().preferredHeight = 120f;
            }
            InfoPanelRows.AddText(_content, $"#{t.TileIndex}  {t.Type}", 14f, InfoPanelRows.MutedColor);

            // 속성별 행: 아이콘 라벨 + 이름 + x스택 (nT) + 설명 (항상 표시 — 스펙 §5.1)
            foreach (var a in t.Attributes)
            {
                string label = a.Type.ToString(); Color tint = Color.white; string desc = "";
                if (attributeVisuals != null && attributeVisuals.TryGet(a.Type, out var entry))
                {
                    if (!string.IsNullOrWhiteSpace(entry.shortLabel)) label = entry.shortLabel;
                    tint = entry.iconTint;
                    desc = entry.description ?? "";
                }
                string dur = a.Duration < 0 ? "(∞T)" : $"({a.Duration}T)";
                string stack = a.Value > 0 ? $"x{a.Value} " : "";
                InfoPanelRows.AddText(_content, $"{label}  {stack}{dur}", 15f, tint, FontStyles.Bold);
                if (!string.IsNullOrWhiteSpace(desc))
                    InfoPanelRows.AddText(_content, desc, 13f, InfoPanelRows.MutedColor);
            }
        }

        private void RenderKeywords(UnitInfoData d)
        {
            // 액티브/패시브/상태 설명을 합쳐 키워드 추출 (기존 ExtractMatches 재사용)
            var sb = new System.Text.StringBuilder();
            if (d.Actives  != null) foreach (var a in d.Actives)  sb.AppendLine(a.DynamicDescription);
            if (d.Passives != null) foreach (var p in d.Passives) { sb.AppendLine(p.DynamicEffect); sb.AppendLine(p.FlavorText); }
            var matches = TooltipKeywordFormatter.ExtractMatches(sb.ToString());
            if (matches == null || matches.Count == 0) return;

            InfoPanelRows.AddSectionTitle(_content, "키워드");
            foreach (var k in matches)
            {
                InfoPanelRows.AddText(_content, k.Key, 15f, k.Color, FontStyles.Bold);
                if (!string.IsNullOrWhiteSpace(k.Description))
                    InfoPanelRows.AddText(_content, k.Description, 13f, InfoPanelRows.MutedColor);
            }
        }
    }
}
```

주의: `ExtractMatches`는 원래 TMP 링크 태그가 삽입된 텍스트 기준일 수 있음 — 시그니처가 평문을 받지 않으면 `TooltipKeywordFormatter.cs`를 열어 평문 매칭 메서드 확인/추가. `TileType` 네임스페이스는 Task 1과 동일 규칙.

- [ ] **Step 4: 부트스트랩** — `SkillManager` 또는 `CombatManager`의 `Awake`/`Start`에 한 줄 추가 (기존 `EnsureInstance` 관례):

```csharp
UI.BattleInfoPanelUI.EnsureInstance();
```

- [ ] **Step 5: 컴파일 확인** — 콘솔 에러 0

- [ ] **Step 6: 플레이 모드 검증**
  1. BattleScene 플레이 → 오른쪽 30% 어두운 패널 + 빈 상태 안내 문구
  2. 캐릭터 호버 → 이름/HP/액티브/패시브/타일 섹션 표시. 몬스터 호버 → 타일 섹션 없음
  3. 캐릭터 클릭 → 핀. 다른 유닛 호버 → 임시 표시, 벗어나면 핀 복귀. 우클릭 → 해제
  4. 빈 타일 호버 → 타일 단독 뷰
  5. 스킬 타게팅 진입 → 적 호버 시 패널 갱신, 클릭해도 핀 안 됨, 예상 피해 커서 툴팁 병행
  6. 패널 위에 마우스 → 3D 호버 반응 없음 (기존 툴팁도 동일)

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/UI/InfoPanel/ Assets/Scripts/Core/
git commit -m "feat: 전투 정보 패널 본체 (선택 컨트롤러 + 섹션 렌더링 + 타일 카드)"
```

---

### Task 5: 구 툴팁 경로 철거 🔥

**Files:**
- Modify: `Assets/Scripts/UI/HoverTooltipUI.cs` (대수술 — 아래 명세)
- Delete: `Assets/Scripts/UI/GlossaryContainerUI.cs` (+.meta)
- Delete: `Assets/Scripts/UI/IHoverTooltipProvider.cs` (+.meta)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Character.cs` (~490-560행 블록 삭제)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/Monster.cs` (15행 선언 + ~310-430행 블록 삭제)
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileData.cs` (`GetHoverTooltipData`+`BuildTooltipText` 삭제)

**Interfaces:**
- Consumes: 없음 (삭제 태스크)
- Produces: 축소된 `HoverTooltipUI` 공개 API — **`ShowPinned(string)` / `HidePinned()` / `EnsureInstance()`만 잔존.** 기존 호출자 `SkillPreviewHoverUI`(19,24행), `SkillTargetSelector`(297,365행 등)는 이 3개만 사용하므로 무변경으로 호환.

- [ ] **Step 1: 삭제 전 참조 전수 조사** — 다음 grep이 **소스 코드에서 0건**이어야 철거 안전 (자기 정의 제외):

```bash
grep -rn "GetHoverTooltipData\|HoverTooltipData\|IHoverTooltipProvider\|GlossaryContainerUI" --include="*.cs" Assets/Scripts/
```

남은 호출자가 있으면 (예: `CombatNotifier`, `MonsterUI`) 먼저 해당 호출을 제거/이관하고 이 스텝을 재실행.

- [ ] **Step 2: Character/Monster/TileData 철거**
  - `Character.cs`: 클래스 선언에서 `UI.IHoverTooltipProvider` 제거, `GetHoverTooltipData`/`BuildCharacterTooltipText`/`BuildPassiveTooltipData`/`BuildStatusTooltipData` 메서드 삭제 (Task 2에서 넣은 `GetBattleInfo`는 유지)
  - `Monster.cs`: 동일 (선언 15행 `UI.IHoverTooltipProvider` 제거 + blob/중복 빌더 삭제)
  - `TileData.cs`: `GetHoverTooltipData`/`BuildTooltipText`/`ResolveTileTypeName` 삭제 (`Type` 프로퍼티가 대체)

- [ ] **Step 3: HoverTooltipUI 대수술** — 남기는 것과 지우는 것:

| 지움 | 남김 |
|---|---|
| `UpdateHoveredTarget`/`GetProviderUnderMouse`/`ClearProvider` (레이캐스트 호버) | `ShowPinned(string)`, `HidePinned()`, `EnsureInstance()` |
| `Show(HoverTooltipData)` 오버로드, `ShowPinned(HoverTooltipData)` | `Show(string)` → private으로 강등 |
| `glossaryContainer` 필드 + 모든 호출 | `panelRect`/`tooltipText`/`FollowMouse`/`UpdateSize`/레이아웃 헬퍼 |
| `keywordDetailRect`/`keywordDetailText` + `UpdateKeywordDetailInteraction`/`ShowKeywordDetail`/`HideKeywordDetail`/`RepositionKeywordDetailPanel` + 관련 필드(`_keywordDetailPinned`,`_pinnedKeywordKey`) | 싱글턴/캔버스 정렬(`EnsureTopMostOrder`) |
| `_currentProvider` 필드 | `_pinnedByUI`, `_visible` |

축소 후 `Update()`는 다음만 남는다:

```csharp
private void Update()
{
    if (!_visible) return;
    FollowMouse();   // 핀 툴팁도 커서를 따라다님 (기존 동작 유지)
}
```

클래스 상단 주석도 "경량 핀 툴팁 (예상 피해/선택 카운터/스킬버튼 호버 전용)"으로 갱신.

- [ ] **Step 4: 컴파일 확인** — 콘솔 에러 0

- [ ] **Step 5: [EDITOR] 씬/프리팹 잔재 제거** — Hierarchy에서 HoverTooltipUI 오브젝트 하위의 GlossaryContainer/KeywordDetail 관련 자식 오브젝트 삭제 (missing script 방지). 프리팹이면 프리팹 모드에서 제거 후 저장. 콘솔에 "Missing script" 경고 0 확인.

- [ ] **Step 6: 플레이 모드 검증**
  1. 캐릭터/몬스터/타일 호버 → **구 툴팁 안 뜸**, 패널만 갱신
  2. 스킬 버튼 호버 → 커서 옆 경량 툴팁 정상 (SkillPreviewHoverUI 경로)
  3. 타게팅 중 예상 피해/선택 카운터 툴팁 정상

- [ ] **Step 7: 커밋**

```bash
git add -A Assets/Scripts/ Assets/Scenes/ Assets/Prefabs/
git commit -m "refactor: 구 호버 툴팁 경로 철거 - HoverTooltipUI 경량화, 글로서리/blob 빌더 삭제"
```

---

### Task 6: 카메라/레이아웃 조정 + 통합 검증

**Files:**
- Modify: `Assets/Scenes/BattleScene.unity` ([EDITOR] 카메라 트랜스폼)

**Interfaces:**
- Consumes: Task 4의 패널 (폭 30%)
- Produces: 최종 레이아웃 — 스펙 §9 검증 체크리스트 전체 통과

- [ ] **Step 1: [EDITOR] 카메라 조정** — BattleScene의 Main Camera를 선택하고, 게임 뷰(16:9)에서 궤도 필드 중심이 **왼쪽 70% 영역의 중앙**에 오도록 X 위치(또는 Y축 회전)를 조정. 패널에 가려지는 타일이 없어야 함. 조정값은 트랜스폼만 변경 (스크립트 없음).

- [ ] **Step 2: 통합 검증 (스펙 §9 전체)**
  1. 호버/클릭/핀/해제 — Task 4 Step 6 재확인
  2. 타게팅: 카메라 이동 후에도 대상 클릭/미리보기 라인/타일 프리뷰 정상 (레이캐스트는 카메라 기준이라 자동 적응 — 어긋나면 카메라가 아니라 다른 원인)
  3. 상태이상 부여 후 0.5s 내 패널 반영 (몬스터에게 중독 걸고 관찰)
  4. 속성 타일(시약/꿀) 위 캐릭터 → 타일 카드 섹션에 속성 행 표시
  5. 콘솔: 컴파일 에러 0, missing script 0, 신규 경고 0
  6. 웨이브 클리어 → 보상 → 다음 웨이브 전환 시 패널 생존 확인 (씬 전환 없으면 자동 통과)

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scenes/BattleScene.unity
git commit -m "feat: 궤도 필드 좌측 배치 카메라 조정 + 정보 패널 통합 검증"
```

---

## Self-Review 결과 (작성 시 수행)

- **스펙 커버리지**: §3 레이아웃/카메라→Task 4·6, §4 데이터 모델→Task 1·2, §5 패널/타일카드/키워드→Task 4, §5.1 DB 확장→Task 3, §6 상호작용→Task 4 Step 2, §7 철거→Task 5, §8 존치→각 태스크에서 무변경, §9 검증→Task 6. 갭 없음.
- **미검증 API 주의 플래그**: `PassiveManager.ActivePassives` 요소 타입, `StatusEffectManager.GetActiveEffects()` 요소 필드, `ExtractMatches` 평문 지원 여부, TMP `textWrappingMode` — 각 사용처에 실제 선언부 확인 지침 명기함.
- **타입 일관성**: `GetBattleInfo()`/`GetTileInfo()`/`UnitInfoBuilder.Build`/`TileData.Type` 명칭이 Task 1~5에서 동일함 확인.
