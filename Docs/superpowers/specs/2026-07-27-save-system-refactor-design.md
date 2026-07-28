# 세이브 시스템 리팩토링 설계

> 작성: 2026-07-27. 브랜치 `refactor/save-system-20260727`.
> 결정: 저장/복원이 두 파일로 갈라진 현 구조를 **매니저 자가 저장(참가자) 방식**으로 통합하고,
> 이름 문자열 대신 **불변 식별자 `saveId`** 로 에셋을 참조한다.
> 세이브 포맷은 v2로 올리며, **구 세이브는 마이그레이션하지 않고 버린다**(개발 중이라 배포 빌드 없음).

## 1. 동기 — 현 구조의 문제

현 세이브는 [`RunSaveService`](../../Assets/Scripts/Core/Run/RunSaveService.cs)와
[`GameFlowManager.ContinueGameFlow`](../../Assets/Scripts/Core/GameFlowManager.cs) 두 곳에 걸쳐 있다.

1. **저장과 복원이 다른 파일에 있다.** 직렬화는 `RunSaveService.SaveCurrent()`, 역직렬화와 적용은
   `GameFlowManager.ContinueGameFlow()`(70줄). 필드 하나를 추가하려면 DTO·저장·복원 **세 곳**을
   동시에 고쳐야 하고, 한 곳만 빠뜨리면 조용히 저장되지 않는다.
2. **시스템별 저장 계약이 없다.** `RunSaveService`가 `ArtifactManager.Instance.Artifacts`,
   `PartyManager.Instance.Party`처럼 남의 내부를 직접 훑는다. 저장 대상 매니저가 늘 때마다
   이 파일이 비대해진다.
3. **버전 필드가 없다.** 그래서 유물 개편 때 마이그레이션이 DTO 안에 임시방편으로 박혔다
   (`RelicNames` + `EffectiveArtifactNames`). 다음 개편 때도 같은 방식으로 필드가 는다.
4. **전부 이름 문자열 매칭이다.** `artifactName`, `PotionName`, `CharacterName`, `ModifierName`을
   저장하므로 표시 이름을 고치면 세이브가 경고 로그만 남기고 조용히 깨진다.
5. **원자적 쓰기가 아니다.** `File.WriteAllText` 직격이라 저장 중 크래시하면 세이브가 손상된다.
6. **부분 실패를 무시한다.** 프리셋을 못 찾으면 `continue` — 파티 4명이 3명으로 줄어든 채
   게임이 그대로 진행된다.
7. **버그**: `ContinueGameFlow`가 `allModifiers`를 만들어 쓰지 않고, 모디파이어 **하나마다**
   `ModifierRegistry.CreateAll()`을 새로 호출한다.
8. **복원이 획득 후보 목록에서 에셋을 찾는다.** `FindInPool`은 `artifactPool`/`potionPool`을 뒤지는데,
   이 리스트는 원래 **상점 진열·드랍 후보**를 담는 게임 디자인용 목록이다. 풀 밖 경로(이벤트 보상 등)로
   얻은 포션은 저장은 되지만 복원 때 찾지 못한다. 성격이 다른 두 역할을 한 리스트가 겸하고 있다.
9. **캐릭터 프리셋 조회가 UI에 매달려 있다.** 복원이 `FindFirstObjectByType<CharacterSelectionUI>()`로
   프리셋 목록을 얻으므로, 그 UI가 로드된 씬에 없으면 파티 복원이 통째로 실패한다.

## 2. 목표와 범위

**목표** — 구조 정리·확장성, 데이터 안정성. 이 둘만이 이번 범위다.

**성공 기준**

- 새 저장 항목을 추가할 때 **매니저 한 파일만** 고치면 된다.
- 표시 이름을 바꿔도 세이브가 깨지지 않는다.
- 저장 도중 크래시해도 직전 세이브가 살아남는다.
- 복원이 반쪽으로 성공하는 일이 없다 — 전부 복원되거나, 실패로 처리되거나 둘 중 하나.

## 3. 아키텍처 — 참가자 방식

각 매니저가 자기 상태의 저장·복원을 직접 소유한다. `RunSaveService`는 오케스트레이션만 한다.

```csharp
public interface IRunSaveParticipant
{
    void Capture(RunSaveData data);                          // 현재 상태 → DTO
    void Validate(RunSaveData data, RunRestoreContext ctx);  // 부작용 없음. 실패는 ctx.Report에 기록
    void Apply(RunSaveData data, RunRestoreContext ctx);     // 검증 전원 통과 후에만 호출
}
```

`Validate`/`Apply` 2단계 분리가 이 설계의 핵심이다. 검증은 아무것도 바꾸지 않고 "이 세이브의 모든
ID를 풀·레지스트리에서 해결할 수 있는가"만 확인한다. 전원이 통과했을 때만 `Apply`로 넘어가므로
**"골드는 넣었는데 파티 스폰 중 실패" 같은 중간 상태가 원천적으로 생기지 않는다.** 롤백 코드가 필요 없다.

### 참가자 수집 — 고정 목록

```csharp
private static List<IRunSaveParticipant> CollectParticipants(out string missing)
{
    missing = null;

    // 순서 = 복원 의존성 순서.
    // FindObjectsByType 순회는 쓰지 않는다 — 순서가 비결정적이고,
    // 인스턴스가 없으면 그 섹션이 '조용히' 누락된다.
    var run   = RunManager.Instance;      // 씬 배치 (EnsureInstance 없음)
    var party = PartyManager.Instance;    // 씬 배치 (EnsureInstance 없음)
    if (run == null)   { missing = nameof(RunManager);   return null; }
    if (party == null) { missing = nameof(PartyManager); return null; }

    return new List<IRunSaveParticipant>
    {
        run,                                // 맵·진행 — 가장 먼저
        GoldManager.EnsureInstance(),
        ArtifactManager.EnsureInstance(),
        PotionManager.EnsureInstance(),
        party,                              // 파티 스폰 — 맨 마지막
    };
}
```

`ArtifactManager`·`PotionManager`·`GoldManager`는 `EnsureInstance()`로 지연 생성되는 싱글톤이다.
현 코드의 `if (ArtifactManager.Instance != null)` 패턴은 저장 시점에 인스턴스가 없으면 **그 섹션을
통째로 유실**시킨다. 고정 목록 + `EnsureInstance`가 이를 막는다. 씬 배치 매니저가 없으면 누락이
아니라 **명시적 실패**가 된다.

### 복원 컨텍스트

파티 복원에 필요한 협력자는 서비스가 찾아 주입한다. `PartyManager`가 UI를 직접 뒤지지 않게 하려는 것이다.

```csharp
public class RunRestoreContext
{
    public CharacterSpawner Spawner;
    public SaveIdCatalog Catalog;                       // 에셋 조회 출처 (§5)
    public RestoreReport Report;

    public CharacterPreset FindPreset(string saveId);   // 못 찾으면 null
}
```

프리셋 조회는 `CharacterSelectionUI.AllCharacters`가 아니라 **카탈로그**가 답한다(§1-9).
복원이 UI 컴포넌트의 존재 여부에 의존하지 않게 하려는 것이다.

### 서비스

```csharp
public static class RunSaveService
{
    public static bool HasSave() => RunSaveFile.HasValidSave();
    public static void Delete()  => RunSaveFile.Delete();

    public static void SaveCurrent()
    {
        var run = RunManager.Instance;
        if (run == null || !run.RunActive) return;

        var participants = CollectParticipants(out string missing);
        if (participants == null)
        {
            // 기존 세이브를 덮어쓰지 않는다 — 반쪽 저장보다 직전 세이브 유지가 낫다
            Debug.LogWarning($"[RunSave] 저장 중단 — {missing} 없음");
            return;
        }

        var data = new RunSaveData();
        foreach (var p in participants) p.Capture(data);
        RunSaveFile.Write(data);
    }

    public static RestoreReport RestoreCurrent()
    {
        var report = new RestoreReport();
        var data = RunSaveFile.Read(report);
        if (data == null) return report;

        var participants = CollectParticipants(out string missing);
        if (participants == null) { report.Failures.Add($"{missing} 없음"); return report; }

        var ctx = BuildContext(report);

        // 1단계 — 검증만. 첫 실패에서 멈추지 않고 전부 수집한다 (디버깅 편의).
        foreach (var p in participants) p.Validate(data, ctx);
        if (!report.Success) return report;   // 아무것도 적용되지 않은 상태

        // 2단계 — 적용
        foreach (var p in participants) p.Apply(data, ctx);
        return report;
    }
}
```

### GameFlowManager 축소

`ContinueGameFlow`의 70줄이 이렇게 줄어든다.

```csharp
private void ContinueGameFlow()
{
    var report = RunSaveService.RestoreCurrent();
    if (!report.Success)
    {
        Debug.LogWarning($"[GameFlow] 세이브 복원 실패 — 새 게임으로 시작합니다.\n{report}");
        RunSaveService.Delete();
        StartGameFlow();
        return;
    }
    ChangeState(GameState.Map);
}
```

## 4. 데이터 모델 v2

저장이 필요한 대상마다 전용 클래스를 둔다. 지금은 `Id` 하나뿐이어도 마찬가지다 —
`RuntimeArtifact`가 상태(발동 횟수, 충전 등)를 갖게 되는 순간 `List<string>` → `List<...>` 전환은
저장·복원 양쪽을 다시 쓰는 일이 되지만, 래퍼가 있으면 필드 한 줄 추가로 끝난다.
`JsonUtility`는 `List<중첩 [Serializable] 클래스>`를 문제없이 다룬다.

```csharp
[System.Serializable]
public class RunSaveData
{
    public int Version = 2;
    public RunProgressSave Progress = new RunProgressSave();                        // 주인: RunManager
    public int Gold;                                                                // 주인: GoldManager
    public List<ArtifactSaveData>  Artifacts = new List<ArtifactSaveData>();        // 주인: ArtifactManager
    public List<PotionSaveData>    Potions   = new List<PotionSaveData>();          // 주인: PotionManager
    public List<CharacterSaveData> Party     = new List<CharacterSaveData>();       // 주인: PartyManager
}

[System.Serializable]
public class RunProgressSave
{
    public int Seed;                    // 같은 시드 → MapGenerator가 같은 맵 재생성
    public int CurrentNodeId = -1;
    public List<int> VisitedNodeIds = new List<int>();
    public int BattlesCleared;
    public List<string> BanishedPresetIds = new List<string>();
}

[System.Serializable] public class ArtifactSaveData { public string Id; }   // ArtifactData.SaveId
[System.Serializable] public class PotionSaveData   { public string Id; }   // Potion.SaveId
[System.Serializable] public class ModifierSaveData { public string Id; }   // 모디파이어 클래스 타입명

[System.Serializable]
public class CharacterSaveData
{
    public string PresetId;
    public int CurrentHp;
    public int MaxHp;
    public int RevivalStock;
    public List<ModifierSaveData> Modifiers = new List<ModifierSaveData>();
}
```

각 필드의 **주인 참가자는 하나로 고정**한다. 두 참가자가 같은 필드를 건드리지 않는다.

`BanishedPresetIds`만 `List<string>`으로 남긴다. 이건 엔티티 인스턴스가 아니라 **집합 소속 여부**라
부가 상태가 붙을 자리가 없다.

`RelicNames`와 `EffectiveArtifactNames` 폴백은 삭제한다.

## 5. 식별자 `saveId`

`saveId`는 **세이브 파일이 그 에셋을 다시 찾아오기 위한 키**다. 게임에 보이는 이름과 완전히 분리된,
한 번 정해지면 바뀌지 않는 문자열이다.

```csharp
public class ArtifactData : ScriptableObject
{
    [Tooltip("세이브 식별자 — 자동으로 채워집니다. 직접 수정하지 마세요.")]
    [SerializeField] private string saveId;

    /// <summary>세이브가 이 에셋을 다시 찾는 키. 한 번 정해지면 바뀌지 않는다.</summary>
    public string SaveId => saveId;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(saveId))                     // 비어 있을 때만 = 최초 1회
            saveId = name;                                    // 에셋 파일명
    }
#endif
}
```

**저장 위치는 `.asset` 파일 자체**다. 에셋 안에 필드로 박혀 git으로 공유되므로 이 저장소와
사용자의 Windows Unity가 같은 값을 본다. 런타임 생성 값이 아니다.

`Potion`처럼 **서브클래스를 갖는 SO는 `protected virtual void OnValidate()`로 선언**한다.
Unity는 가장 파생된 클래스의 `OnValidate` 하나만 호출하므로, 베이스에 `private`으로 두면
나중에 `HealPotion`이 자기 `OnValidate`를 추가하는 순간 `saveId` 채우기가 조용히 멈춘다.

**값이 정해지는 시점은 최초 한 번**뿐이다. Unity가 에셋을 임포트하거나 인스펙터에서 열 때
`OnValidate`가 돌면서, 비어 있을 때만 채운다.

| 대상 | 최초 값의 출처 | 예 |
|---|---|---|
| `ArtifactData` | 에셋 파일명 | `"PowerfullPunch"` |
| `Potion` | 에셋 파일명 | `"NewHealPotion"` |
| `CharacterPreset` | 에셋 파일명 | `"Warrior"` |
| 모디파이어 | 클래스 타입명 (에셋 아님 — 코드 레지스트리) | `"SharpBladeModifier"` |

한 번 채워진 뒤에는 표시 이름·에셋 파일명·클래스명·파라미터 값을 전부 바꿔도 `saveId`는 그대로다.

에셋 파일명은 폴더가 다르면 겹칠 수 있다. 전역 유일성은 파일명이 아니라 **중복 검출기**가 보장한다.

### 왜 타입명이 아니라 에셋 파일명인가

세 종류 모두 **"에셋 1개 = 게임 내 항목 1개"이고, 스크립트는 그 에셋 여럿에 대응할 수 있다**는
전제 위에 있다. 그래서 정체성의 단위는 클래스가 아니라 에셋이고, ID도 에셋에서 나와야 한다.

- 포션은 타입으로 식별할 수 없다 — `HealPotion.healAmount`처럼 튜닝 필드가 있어 회복량만 다른
  대/중/소 물약을 같은 클래스의 별도 에셋으로 만드는 것이 자연스럽다.
- `CharacterPreset`은 클래스 하나에 에셋 넷이라 애초에 타입 식별이 불가능하다.
- 유물도 마찬가지다. `ArtifactManager`가 [`RemoveArtifact<T>()`](../../Assets/Scripts/Core/Run/Artifact/ArtifactManager.cs)로
  타입을 정체성처럼 쓰고 있어 한때 `effect` 클래스명을 기본값으로 삼았으나, **같은 효과 클래스로
  에셋을 둘 이상 만들면 두 에셋이 같은 `saveId`로 자동 생성되어 충돌한다.** 1:N을 허용하는 이상
  클래스명은 ID가 될 수 없다.

결과적으로 복원 코드도 **한 갈래**로 유지된다. 유물만 별도 경로를 만들지 않는다.

### 에셋 조회 — `SaveIdCatalog`

`saveId`로 에셋을 되찾는 출처는 **획득 후보 풀이 아니다**(§1-8). `artifactPool`/`potionPool`은
상점 진열·드랍 후보를 담는 게임 디자인용 목록이고, 풀 밖 경로로도 포션·유물이 게임에 들어온다.
복원은 **프로젝트에 존재하는 모든 대상 에셋**을 볼 수 있어야 한다. 두 목록을 분리한다.

| 목록 | 역할 | 성격 |
|---|---|---|
| `artifactPool` / `potionPool` | 획득 후보 — 상점 진열, 랜덤 드랍 | 게임 디자인. 의도적으로 일부만 담는다 |
| `SaveIdCatalog` | 복원 조회 대상 | 세이브 인프라. 하나라도 빠지면 안 된다 |

```csharp
public class SaveIdCatalog : ScriptableObject
{
    [SerializeField] private List<ArtifactData>    artifacts;
    [SerializeField] private List<Potion>          potions;
    [SerializeField] private List<CharacterPreset> presets;

    private static SaveIdCatalog _cached;
    public static SaveIdCatalog Get() => _cached ??= Resources.Load<SaveIdCatalog>("SaveIdCatalog");

    public ArtifactData    FindArtifact(string saveId);   // Dictionary 인덱스, 최초 1회 빌드
    public Potion          FindPotion(string saveId);
    public CharacterPreset FindPreset(string saveId);
}
```

에셋은 `Assets/Resources/SaveIdCatalog.asset`에 둔다. 이 저장소는 이미 `TileAttributeVisualDatabase`,
`TooltipKeywordDatabase`, `TileVfxDatabase`를 **정확히 이 방식**(`Resources.Load` + 정적 캐시)으로
쓰고 있어 새로 만드는 관례가 아니다. `Resources` 아래에 있으므로 참조된 에셋이 빌드에서 스트립되지 않는다.

조회는 리스트 선형 순회가 아니라 `Dictionary<string, T>` 인덱스로 한다. 인덱스를 만드는 시점이
곧 **런타임 중복 `saveId` 검출 지점**이다.

**수동 갱신 단계를 두지 않는 것이 이 안의 전제 조건이다.** "카탈로그에 등록하는 걸 깜빡해서 복원이
깨진다"면 풀을 쓰던 때와 달라지는 게 없다. `AssetPostprocessor.OnPostprocessAllAssets`로 대상 타입
에셋이 임포트·삭제·이동될 때 카탈로그를 자동 재스캔한다. 새 포션 에셋을 만들면 저절로 들어간다.

대안으로 모든 대상 에셋을 `Assets/Resources/` 아래로 옮기고 `Resources.LoadAll<Potion>("")`로 런타임
열거하는 방식이 있다. 동기화 단계가 아예 없다는 장점이 있으나, 에셋을 스크립트 옆에 두는 현재 배치
(`Assets/Scripts/Data/Potions/HealPotion/`)를 포기해야 하고 폴더를 벗어난 에셋이 조용히 누락된다.
카탈로그 쪽을 택한다 — 목록이 눈에 보이고 검사 지점이 명확하다.

### 에셋 복제 문제

유니티에서 `Ctrl+D`로 에셋을 복제하면 `saveId`까지 복사된다. `OnValidate`는 값이 비어 있지 않으니
새로 굽지 않는다. 그러면 풀에 같은 `saveId`가 둘이 되고 복원이 둘 중 아무거나 집는다 — 정확히
이번에 없애려는 종류의 조용한 오류다. 에셋 복제는 흔한 작업이므로 반드시 막는다.

대응은 **카탈로그 재스캔에 얹은 중복 검사**다. 재스캔은 어차피 `AssetDatabase.FindAssets`로 대상
타입 에셋을 전부 훑으므로, 그 자리에서 같은 `saveId`가 둘 이상인지 확인해 콘솔에 에러를 띄운다.
에셋을 복제한 직후 임포트가 일어나면서 자동으로 걸린다. 에디터 전용 API이므로 `#if UNITY_EDITOR`
안에서만 쓴다.

추가로 **`도구 > Dice Orbit > 세이브 ID 전체 점검` 에디터 메뉴**를 만든다. 강제 재스캔 + 빈 `saveId`
채우기 + 중복 보고를 한 번에 실행하는 수동 진입점이다.

### 빈 값에는 폴백을 두지 않는다

`SaveId`가 비어 있을 때 표시 이름으로 폴백하지 **않는다**. 폴백이 있으면 빈 `saveId`를 가진 에셋이
이름으로 조용히 저장되고, 나중에 그 이름이 바뀌면 세이브가 깨진다 — 없애려던 문제가 폴백 뒤에 숨는다.
**빈 `saveId`는 복원 실패로 취급**하며, 이는 아래 "전부 아니면 전무" 정책과 일관된다.
`OnValidate`가 채워주므로 정상 경로에서는 빌 일이 없다.

### 모디파이어

모디파이어는 에셋이 아니라 [`ModifierRegistry`](../../Assets/Scripts/Data/Modifiers/ModifierRegistry.cs)의
팩토리 배열(7종)이므로 **클래스 타입명**을 ID로 쓴다. 표시용 `ModifierName`(한글 문구)보다 안정적이다.

```csharp
public static CharacterModifier Create(string id);   // 없으면 null
public static bool Exists(string id);                // Validate 단계용 — 인스턴스를 만들지 않는다
```

이 API가 생기면 [모디파이어 하나마다 `CreateAll()`을 새로 호출하던 버그](../../Assets/Scripts/Core/GameFlowManager.cs)도
함께 사라진다.

## 6. 복원 실패 정책 — 전부 아니면 전무

```csharp
public class RestoreReport
{
    public bool Success => Failures.Count == 0;
    public List<string> Failures = new List<string>();   // 하나라도 있으면 복원 포기
    public List<string> Warnings = new List<string>();   // 진행을 막지 않음
    public override string ToString();                   // 로그용 요약
}
```

**ID를 하나라도 해결하지 못하면 복원을 포기하고 새 게임을 안내한다.** 관대한 대안(유물·포션만
건너뛰고 진행)도 가능하지만 채택하지 않는다. `saveId` 도입 이후 이 실패는 **플레이어 상황이 아니라
개발 중 버그**를 뜻하고, 조용히 반쪽으로 복원되면 그 버그를 놓친다. "이어할 수 없습니다"가 정직하다.

`Failures`로 처리하는 것:

- 세이브 파일 파싱 실패, `Version != 2`
- `RunManager`/`PartyManager`/`CharacterSpawner` 부재, `firstAct` 미지정
- `SaveIdCatalog` 자체를 로드하지 못함
- 유물·포션 `Id`를 카탈로그에서 찾지 못함
- 프리셋 `PresetId`를 카탈로그에서 찾지 못함
- 모디파이어 `Id`가 레지스트리에 없음
- `saveId`가 빈 에셋

`Warnings`로 처리하는 것 (진행 가능):

- `BanishedPresetIds`의 ID 불일치 — 그 캐릭터가 다시 영입 가능해질 뿐 런은 정상 진행된다

### `Apply`는 기존 상태를 초기화하고 시작한다

같은 세션에서 런을 끝내고 다시 이어하기를 할 수 있으므로, `Apply`는 이전 런의 잔여 상태 위에
덮어써서는 안 된다. 현재 `GoldManager.ResetGold()` 외에는 초기화 API가 없으므로 아래를 추가한다.

| 매니저 | 추가 API |
|---|---|
| `ArtifactManager` | `ClearAll()` — 보유 유물 비우기 + `OnArtifactsChanged` 발행 |
| `PotionManager` | `ClearAll()` — 슬롯 비우기 + 변경 이벤트 발행 |
| `PartyManager` | `ClearAll()` — 파티 목록 비우기 및 기존 `Character` 오브젝트 파괴 |

파티 복원 시 `CharacterSpawner.Spawn`이 [`PartyManager.AddCharacter`를 자동 호출](../../Assets/Scripts/Core/CharacterSpawner.cs)하므로,
`PartyManager.Apply`는 스폰만 하면 되고 별도 등록은 필요 없다.

## 7. 파일 I/O

`RunSaveFile`이 파일 계층을 전담한다. 매니저도 서비스도 `File` API를 직접 만지지 않는다.

경로는 `Application.persistentDataPath` 아래 `run_save.json` / `.bak` / `.tmp` /
`run_save.corrupt.json` 넷이다.

```csharp
public static void Write(RunSaveData data)
{
    File.WriteAllText(TmpPath, JsonUtility.ToJson(data, true));
    if (File.Exists(SavePath)) File.Replace(TmpPath, SavePath, BakPath);  // 원자적 교체 + 백업
    else                       File.Move(TmpPath, SavePath);
}
```

임시 파일에 먼저 쓰고 교체하므로 **저장 중 크래시해도 기존 세이브가 살아남는다.**
`File.Replace`는 플랫폼에 따라 동작이 다를 수 있어 실패 시 `Delete` + `Move` 폴백을 둔다.

읽기는 3단 방어다.

1. `run_save.json` 파싱 시도
2. 실패하면 `run_save.bak`으로 재시도 (성공 시 `Warnings`에 기록)
3. 둘 다 실패하면 손상 파일을 `run_save.corrupt.json`으로 **보존**하고 `null` 반환

손상 파일을 지우지 않고 남기는 것은 개발 중 재현을 위해서다.

버전 검사는 로드 경로에서 한다. `Version != 2`면 마이그레이션 없이 "이어할 수 없는 세이브"로 처리한다.

`HasValidSave()`는 파일 존재만이 아니라 **파싱과 버전 검사까지 통과해야 `true`**를 반환한다.
현 `HasSave()`는 `File.Exists`만 보므로, [메인메뉴의 이어하기 버튼](../../Assets/Scripts/UI/MainMenuUI.cs)이
활성화돼 있는데 눌러보면 실패하는 상황이 생긴다. 호출 지점은 `MainMenuUI.Show()` 한 곳뿐이고
(`Update`가 아니다) 파일도 작으므로 전체 파싱 비용은 무시할 수준이다.

`Delete()`는 본 파일·`.bak`·`.tmp`를 함께 정리한다.

## 8. 영향 파일

### 신규 — `Assets/Scripts/Core/Run/Save/` (각각 `.meta` 동봉)

| 파일 | 내용 |
|---|---|
| `RunSaveData.cs` | v2 DTO 전체 (§4) |
| `IRunSaveParticipant.cs` | 참가자 인터페이스 |
| `RunRestoreContext.cs` | 복원 컨텍스트 |
| `RestoreReport.cs` | 실패·경고 보고서 |
| `RunSaveFile.cs` | 파일 I/O, 원자적 쓰기, 버전 검사 |
| `RunSaveService.cs` | 참가자 수집 및 2단계 복원 (기존 파일을 `.meta`와 함께 이동) |

네임스페이스는 `DiceOrbit.Core.Run` → `DiceOrbit.Core.Run.Save`로 바뀐다.

`SaveIdCatalog.cs`는 런타임 코드이므로 위 폴더에 함께 둔다. 에디터 전용 파일은
`Assets/Scripts/Editor/` 아래 두 개다.

| 파일 | 내용 |
|---|---|
| `SaveIdCatalogPostprocessor.cs` | `OnPostprocessAllAssets`에서 카탈로그 자동 재스캔 + 중복 검출 |
| `SaveIdValidator.cs` | `도구 > Dice Orbit > 세이브 ID 전체 점검` 메뉴 (수동 진입점) |

`Assets/Resources/SaveIdCatalog.asset`은 **에디터에서 생성해야 하므로 이 환경에서 만들 수 없다.**
Windows Unity 핸드오프 항목이다(§9).

### 수정

| 파일 | 내용 |
|---|---|
| `RunManager` | `IRunSaveParticipant` 구현. 기존 4인자 `RestoreRun(...)` 제거 (호출자는 `GameFlowManager` 하나뿐) |
| `GoldManager` | `IRunSaveParticipant` 구현 |
| `ArtifactManager` | `IRunSaveParticipant` 구현, `ClearAll()` 추가, `FindInPool` 삭제, 디버그 스캐폴딩 삭제(아래) |
| `PotionManager` | `IRunSaveParticipant` 구현, `ClearAll()` 추가, `FindInPool` 삭제, 디버그 스캐폴딩 삭제(아래) |
| `PartyManager` | `IRunSaveParticipant` 구현, `ClearAll()` 추가 |
| `ArtifactData` · `Potion` · `CharacterPreset` | `saveId` 필드 + `SaveId` + `OnValidate` |
| `ModifierRegistry` | `Create(string id)` · `Exists(string id)` 추가 |
| `GameFlowManager` | `ContinueGameFlow` 70줄 → 약 10줄 |
| `MainMenuUI` | 네임스페이스 변경 반영 |

`FindInPool`은 호출자가 [`GameFlowManager`의 복원 블록 두 줄](../../Assets/Scripts/Core/GameFlowManager.cs)뿐인
**복원 전용 API**이므로 카탈로그 조회로 대체되며 삭제된다.

### 디버그 스캐폴딩 삭제

모든 포션·유물이 대응하는 `.asset`을 갖도록 리팩토링하므로, 에셋 없이 굴러가게 하던 임시 코드를
함께 걷어낸다. 포션이 이미 간 길(`EnsureDefaultPool` 주석 처리)을 유물에도 적용하고, 양쪽 모두
정식으로 제거한다.

| 대상 | 삭제 내용 |
|---|---|
| `ArtifactManager` | `startingArtifacts` 필드, `EnsureDefaultPool()`, `CreateDefault()` |
| `PotionManager` | `startingPotions` 필드, `EnsureDefaultPool()`, `CreateDefault()`, `RuntimePotion` |

이것이 `saveId` 정책과 직결된다. `ScriptableObject.CreateInstance`로 런타임 생성한 SO에는
**`OnValidate`가 호출되지 않아 `saveId`가 빈 채로 남는다.** "빈 `saveId`는 복원 실패"(§5) 정책과
정면으로 충돌하므로, 런타임 생성 경로가 남아 있는 한 세이브가 성립하지 않는다.

**현재 씬 상태와 그 여파** — `BattleScene.unity`에서 `artifactPool`과 `potionPool`은 둘 다 비어
있고, `startingArtifacts`에 `PowerfullPunch.asset`, `startingPotions`에 `NewHealPotion.asset`이
하나씩 들어 있다. 즉 지금 게임에 유물·포션이 들어오는 유일한 경로가 시작 목록이고, 상점·드랍
후보는 전적으로 `EnsureDefaultPool`이 만든 런타임 5종에 의존한다. 삭제하면 **획득 후보가 0개가
된다.** 유물 효과 클래스는 6개(`CozyBedroll` `GoldenDice` `LifeAmulet` `PhoenixFeather`
`RegularStamp` `PowerfullPunch`) 있으나 `.asset`은 하나뿐이므로, 나머지 에셋 저작과 풀 등록이
Windows Unity 쪽 작업으로 남는다(§9). 클래스 코드는 그대로 두므로 에셋만 만들면 된다.

## 9. 검증

이 작업 환경에서는 Unity를 실행할 수 없으므로([CLAUDE.md](../../CLAUDE.md)) **Roslyn 컴파일로 타입·컴파일
오류까지만** 확인한다. 실제 동작 확인은 브랜치를 push해 Windows Unity에서 한다.

### 선행 작업 (에디터에서만 가능 — 이 환경에서 만들 수 없다)

1. `Create > DiceOrbit > SaveIdCatalog`로 카탈로그 생성 → `Assets/Resources/SaveIdCatalog.asset`에 배치
2. 유물 효과 클래스 5종(`CozyBedroll` `GoldenDice` `LifeAmulet` `PhoenixFeather` `RegularStamp`)의
   `ArtifactData` 에셋 저작 → `artifactPool`에 등록 (§8 '디버그 스캐폴딩 삭제' 참조)
3. 포션 에셋을 원하는 수만큼 저작 → `potionPool`에 등록

### 검증 절차

1. 대상 에셋을 한 번씩 선택 → `saveId`가 에셋 파일명으로 자동 채워지는지
2. 에셋을 `Ctrl+D`로 복제 → 중복 `saveId` 에러가 콘솔에 뜨는지
3. 새 에셋 생성 → 카탈로그에 **자동으로** 추가되는지 (수동 스캔 없이)
4. `도구 > Dice Orbit > 세이브 ID 전체 점검` 실행 → 빈 값·중복 없음 확인
5. 새 게임 → 유물·포션 획득 → 종료 → 이어하기 → 골드·유물·포션·파티 HP·모디파이어 일치
6. **풀에 없는 포션**을 코드로 직접 지급 → 저장 → 이어하기 → 복원되는지 (§1-8이 닫혔는지)
7. `CharacterSelectionUI`가 없는 씬에서 이어하기 → 파티가 복원되는지 (§1-9가 닫혔는지)
8. `persistentDataPath/run_save.json`을 열어 v2 포맷 확인
9. 세이브 파일을 중간에서 잘라 손상 → `.bak` 복구 또는 이어하기 버튼 비활성 확인
10. 프리셋 `saveId`를 일부러 바꾼 뒤 → 반쪽 복원이 아니라 "복원 불가"로 처리되는지
11. 한 세션에서 런 종료 후 다시 이어하기 → 이전 런의 유물·파티가 섞이지 않는지

## 10. 이번 범위 밖

목적에서 제외하기로 한 것들이다.

- 저장 시점 확대 (상점·이벤트·보상 도중 저장). 현행대로 **맵 진입 시 1회**를 유지한다.
- 메타 진행(영구 해금·통계), 세이브 슬롯 다중화
- `PlayerPrefs` 환경설정([`SettingsUI`](../../Assets/Scripts/UI/SettingsUI.cs))과의 통합 — 별개 축이며 건드리지 않는다
- 구 세이브 마이그레이션, 클래스 리네임용 별칭 테이블
- 전투 중 저장 — 노드 단위 스냅샷 구조를 유지한다
- **유물 1:N 대응에 따른 `ArtifactManager` API 정리.** 같은 효과 클래스로 에셋을 둘 이상 만들면
  [`RemoveArtifact<T>()`](../../Assets/Scripts/Core/Run/Artifact/ArtifactManager.cs)가 다른 에셋의
  유물까지 지우고, [`Owns(data)`](../../Assets/Scripts/Core/Run/Artifact/ArtifactManager.cs)의
  중복 획득 판정 의미도 흔들린다. 세이브와 독립된 문제이므로 1:N 리팩토링 때 함께 본다.
