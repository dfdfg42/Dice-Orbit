# 세이브 시스템 리팩토링 구현 계획

> **작업자(에이전트) 안내:** 이 계획은 `superpowers:subagent-driven-development` 또는
> `superpowers:executing-plans`로 태스크 단위 실행을 전제로 한다. 각 단계는 체크박스(`- [ ]`)다.

**설계 원본:** [2026-07-27-save-system-refactor-design.md](../specs/2026-07-27-save-system-refactor-design.md)
설계와 이 계획이 어긋나면 **설계가 정답**이다. 어긋난 곳을 발견하면 사용자에게 알린다.

**목표:** 저장/복원이 `RunSaveService`와 `GameFlowManager`로 갈라진 구조를 매니저 자가 저장
(`IRunSaveParticipant`) 방식으로 통합하고, 이름 문자열 대신 불변 `saveId`로 에셋을 참조하게 한다.

**아키텍처:** 각 매니저가 자기 상태의 `Capture`/`Validate`/`Apply`를 직접 소유한다. `RunSaveService`는
고정 순서의 참가자 목록을 돌리는 오케스트레이터로만 남는다. 복원은 2단계(전원 검증 → 전원 적용)라
중간 상태가 생기지 않으므로 롤백 코드가 없다. 에셋 조회는 획득 후보 풀이 아니라 프로젝트 전체를
담는 `SaveIdCatalog`가 담당한다.

**기술 스택:** Unity 6000.3.8f1 / C# 9 / `JsonUtility` / ScriptableObject / `AssetPostprocessor`

---

## Global Constraints

이 절의 제약은 **모든 태스크에 암묵적으로 포함**된다.

- **엔진:** Unity 6000.3.8f1. 언어 버전 C# 9 (`netstandard2.1`). C# 10+ 문법(파일 스코프 네임스페이스,
  `record`, 전역 using)을 쓰지 않는다.
- **세이브 포맷 버전:** `2`. 마이그레이션 코드·별칭 테이블을 만들지 않는다. `Version != 2`는 폐기다.
- **`.meta` 동봉:** `Assets/` 아래 파일을 추가·삭제·이동하면 `.meta`도 같이 처리해 커밋한다.
  GUID가 사라지면 씬·프리팹 참조가 끊긴다.
- **손으로 편집 금지:** `.unity`, `.prefab`, `.asset`. 에디터 작업이 필요하면 핸드오프 목록에 적는다.
- **브랜치:** `refactor/save-system-20260727`. `main`에 직접 커밋하지 않는다.
- **커밋 신원:** 정수환 `<aibf0815@gmail.com>`. 커밋 메시지는 한국어.
- **`saveId` 폴백 금지:** 빈 `saveId`는 표시 이름으로 대체하지 않는다. 복원 실패로 취급한다.
- **네임스페이스:** 신규 세이브 코드는 전부 `DiceOrbit.Core.Run.Save`.
- **검증:** 각 태스크는 `Tools/compile-check.sh` 통과로 끝난다(태스크 0에서 구축). 실제 동작 검증은
  Unity가 있는 Windows PC 몫이다 — 이 환경에서는 **컴파일 이상을 검증할 수 없다.**

---

## 검증 수단에 대한 정직한 한계

이 저장소에는 **자동화된 동작 테스트가 없다.** NUnit 테스트 프로젝트가 없고(`asmdef`는 전부
서드파티 에셋 것), 이 VM에는 Unity를 설치할 수 없다. 세이브 코드는 `Application.persistentDataPath`,
`ScriptableObject`, `MonoBehaviour` 싱글톤에 묶여 있어 순수 C# 테스트로 떼어내기도 어렵다.

따라서 각 태스크의 검증 사이클은 TDD의 red-green이 아니라 **컴파일 red-green**이다.

1. 새 심볼을 참조하는 코드를 먼저 넣고 컴파일 → `CS0246`/`CS0117`로 **실패하는 것을 확인**
2. 구현
3. 컴파일 → **통과 확인**

"동작이 맞는가"는 §마지막의 Windows Unity 체크리스트가 유일한 게이트다. 이 계획은 그 사실을
숨기지 않는다. 컴파일이 통과했다고 "동작한다"고 보고하지 말 것.

**컴파일 검증은 Unity의 어셈블리 분할을 그대로 흉내낸다.** Unity가 스크립트를 쪼개는 방식과
같게 두 번 컴파일한다.

| 단계 | 대상 | `UNITY_EDITOR` | 참조 |
|---|---|---|---|
| `Assembly-CSharp` | `Assets/Scripts/` 중 `Editor/` 밖 (169개) | **끔** | `UnityEngine.*`만 |
| `Assembly-CSharp-Editor` | `Assets/Scripts/**/Editor/**` (3개) | 켬 | `UnityEngine.*` + `UnityEditor.*` + 위 결과 |

이 분할 덕분에 **단일 컴파일이 놓치던 두 종류를 잡는다.** 2026-07-28 세션에서 일부러 위반을
심어 확인했다.

| 위반 | 단일 컴파일 | 분할 컴파일 |
|---|---|---|
| 에디터 코드가 런타임 클래스의 `internal` 멤버 접근 | `COMPILE OK` (놓침) | `CS0117` 검출 |
| 런타임 코드가 `#if UNITY_EDITOR` 밖에서 `UnityEditor` API 사용 | `COMPILE OK` (놓침) | `CS0234` 검출 |

첫 번째가 실제로 이 계획에 영향을 줬다 — 태스크 2의 `EditorSetContents`/`EditorContentsEqual`이
`internal`이 아니라 `public`인 이유다.

**그래도 동작 검증은 아니다.** 잡히지 않는 것은 여전히 남는다.

- 씬·프리팹의 직렬화 참조가 살아 있는지 (`.meta` GUID, 인스펙터 배선)
- `OnValidate`·`AssetPostprocessor` 같은 에디터 수명주기가 실제로 도는지
- 런타임 로직이 의도대로 동작하는지

이것들은 Windows Unity가 유일한 게이트다.

---

## File Structure

### 신규 — `Assets/Scripts/Core/Run/Save/`

| 파일 | 책임 |
|---|---|
| `RunSaveData.cs` | v2 DTO 전부 (`RunSaveData` + 하위 5개 클래스). 로직 없음 |
| `IRunSaveParticipant.cs` | 참가자 계약 (3메서드) |
| `RestoreReport.cs` | 실패·경고 수집 |
| `RunRestoreContext.cs` | 복원 시 참가자에게 주입되는 협력자 묶음 |
| `SaveIdCatalog.cs` | `saveId` → 에셋 인덱스. `Resources`에서 로드 |
| `RunSaveFile.cs` | 파일 I/O 전담. 원자적 쓰기, 3단 읽기 방어, 버전 검사 |
| `RunSaveService.cs` | 참가자 수집 + 2단계 복원 오케스트레이션 |

### 신규 — `Assets/Scripts/Editor/`

| 파일 | 책임 |
|---|---|
| `SaveIdValidator.cs` | 카탈로그 재스캔 본체 + 빈 ID 채우기 + 중복 보고 + 메뉴 |
| `SaveIdCatalogPostprocessor.cs` | 에셋 임포트 훅 → 재스캔 호출 |

### 신규 — 저장소 루트

| 파일 | 책임 |
|---|---|
| `Tools/setup-compile-refs.sh` | 참조 어셈블리 구축 (1회성): .NET SDK · Unity 매니지드 DLL · 패키지 선빌드 |
| `Tools/compile-check.sh` | Roslyn 컴파일 검증 (매 태스크마다 실행, 1초 미만) |

둘 다 `Assets/` 밖이므로 `.meta`가 필요 없다.

### 수정

`ArtifactData` · `Potion` · `CharacterPreset` (saveId) / `RunManager` · `GoldManager` ·
`ArtifactManager` · `PotionManager` · `PartyManager` (참가자) / `ModifierRegistry` /
`GameFlowManager` / `MainMenuUI`

### 삭제

`Assets/Scripts/Core/Run/RunSaveService.cs` (+`.meta`) — 신규 위치로 대체

---

## Task 0: Roslyn 컴파일 검증 환경

**목적:** 이후 모든 태스크가 기댈 검증 수단을 만든다. 이게 없으면 컴파일 오류가 전부 Windows PC까지
git 왕복해서 돌아온다.

> **이 태스크의 내용은 실측으로 검증됐다.** 2026-07-28 세션에서 아래 스크립트를 그대로 만들어
> 돌렸고, 기준선이 `COMPILE OK`(0.66초)로 통과하는 것을 확인했다. 참조 어셈블리도 이미
> `~/unity-refs`에 구축돼 있다. 같은 머신에서 이어서 작업한다면 Step 1은 확인만 하고 넘어간다.

**Files:**
- Create: `Tools/setup-compile-refs.sh`
- Create: `Tools/compile-check.sh`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillData/SkillData.cs:6`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/OrbitManager.cs:6`
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave1/Goblin/RandMineTile.cs:3,5`
- Modify: `Assets/Scripts/Data/MonsterPresets/Wave4/LunaPriest/LunaPriest.cs:11`

`Tools/`는 `Assets/` 밖이므로 `.meta`가 필요 없다.

**Interfaces:**
- Produces: `Tools/compile-check.sh` — 인자 없이 실행. 성공 시 `COMPILE OK` + exit 0,
  실패 시 컴파일러 진단 + exit 1. 이후 모든 태스크가 이 스크립트 하나만 호출한다.

- [ ] **Step 1: 준비물 확인**

```bash
~/.dotnet/dotnet --version
ls ~/unity-refs/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll
ls ~/unity-refs/prebuilt/*.dll
```

기대: 버전 문자열 + `UnityEngine.CoreModule.dll` + 선빌드 DLL 4개
(`UnityEngine.UI` · `Unity.TextMeshPro` · `Unity.InputSystem` · `Unity.InternalAPIEngineBridge.004`).

셋 중 하나라도 없으면 Step 2의 셋업 스크립트를 만들어 실행한다. 전부 있으면 Step 2를 건너뛰고
Step 3으로 간다(스크립트 파일 자체는 저장소에 남겨야 하므로 작성은 한다).

- [ ] **Step 2: 셋업 스크립트 작성 및 실행**

`Tools/setup-compile-refs.sh`:

```bash
#!/usr/bin/env bash
# 컴파일 검증용 참조 어셈블리 구축 (1회성, 약 4.5GB 다운로드).
#
# Unity 매니지드 DLL은 순수 IL이라 ARM64 리눅스에서도 참조할 수 있다.
# 에디터 자체는 x86_64 전용이라 실행할 수 없지만, 참조만 하는 데는 문제없다.
set -euo pipefail

REFS="$HOME/unity-refs"
UNITY_HASH="1c7db571dde0"          # 6000.3.8f1 — ProjectSettings/ProjectVersion.txt와 일치해야 한다
INPUTSYSTEM_VER="1.17.0"

mkdir -p "$REFS/packages"

# ── 1. .NET SDK (Roslyn csc 제공) ──
if [ ! -x "$HOME/.dotnet/dotnet" ]; then
  echo "── .NET SDK 설치"
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 9.0 --install-dir "$HOME/.dotnet"
fi

# ── 2. Unity 매니지드 DLL + 내장 패키지 소스 ──
# xz가 필요하다: sudo apt-get install -y xz-utils
if [ ! -d "$REFS/Editor/Data/Managed/UnityEngine" ]; then
  echo "── Unity 참조 어셈블리 추출 (4.5GB 스트리밍, 수십 분)"
  curl -sL "https://download.unity3d.com/download_unity/$UNITY_HASH/LinuxEditorInstaller/Unity.tar.xz" \
    | tar -xJ -C "$REFS" --wildcards \
        'Editor/Data/Managed/*' \
        'Editor/Data/NetStandard/*' \
        'Editor/Data/Resources/PackageManager/BuiltInPackages/*'
fi

# ── 3. 레지스트리 패키지 소스 ──
# com.unity.ugui는 Unity 6에서 에디터 내장이라 레지스트리에 없다 — 위 tar에서 온다.
if [ ! -d "$REFS/packages/com.unity.inputsystem-$INPUTSYSTEM_VER" ]; then
  echo "── com.unity.inputsystem 내려받기"
  curl -sL "https://packages.unity.com/com.unity.inputsystem/-/com.unity.inputsystem-$INPUTSYSTEM_VER.tgz" \
       -o "$REFS/packages/inputsystem.tgz"
  mkdir -p "$REFS/packages/com.unity.inputsystem-$INPUTSYSTEM_VER"
  tar -xzf "$REFS/packages/inputsystem.tgz" -C "$REFS/packages/com.unity.inputsystem-$INPUTSYSTEM_VER"
fi

# ── 4. 패키지 소스를 DLL로 선빌드 ──
#
# 왜 미리 빌드하는가:
#  (a) 어셈블리 '이름'이 중요하다. Unity 모듈 DLL의 InternalsVisibleTo가 이름으로 걸려 있어서,
#      inputsystem 소스를 'DiceOrbit'이라는 이름으로 함께 컴파일하면 internal 접근이 CS0122로 막힌다.
#  (b) 매 태스크마다 패키지 수천 파일을 재컴파일할 이유가 없다. 선빌드하면 검증이 1초 미만이다.
PRE="$REFS/prebuilt"
UGUI="$REFS/Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.ugui"
IS="$REFS/packages/com.unity.inputsystem-$INPUTSYSTEM_VER/package"
mkdir -p "$PRE"

DOTNET="$HOME/.dotnet/dotnet"
CSC="$(ls -d "$HOME"/.dotnet/sdk/*/Roslyn/bincore/csc.dll | sort | tail -1)"

UNITY_REFS=()
while IFS= read -r d; do UNITY_REFS+=("-r:$d"); done < <(
  find "$REFS/Editor/Data/Managed/UnityEngine" -name '*.dll' \
       ! -name 'UnityEngine.dll' ! -name 'UnityEditor.dll'
  find "$REFS/Editor/Data/NetStandard" -name 'netstandard.dll'
)
DEFINES=(-define:UNITY_EDITOR -define:UNITY_2020_1_OR_NEWER -define:UNITY_6000_0_OR_NEWER)

build() {
  local name="$1" root="$2"; shift 2
  local rsp="$PRE/$name.rsp"
  # AssemblyInfo.cs를 제외하지 않는다 — 어셈블리 상수(InputSystem.kDocUrl 등)가 거기 산다.
  # 중복 속성(CS0579)은 여러 패키지를 한 컴파일에 넣을 때만 생기는데, 여기는 패키지별 빌드다.
  find "$root" -name '*.cs' -not -path '*/Tests/*' -not -path '*/Documentation*' \
       -printf '"%p"\n' > "$rsp"
  echo "── $name ($(wc -l < "$rsp")개 파일)"
  "$DOTNET" "$CSC" -nologo -noconfig -nostdlib -target:library -langversion:9 -unsafe -warn:0 \
    -out:"$PRE/$name.dll" "${UNITY_REFS[@]}" "$@" "${DEFINES[@]}" "@$rsp"
}

build Unity.InternalAPIEngineBridge.004 "$UGUI/Runtime/InternalBridge"
build UnityEngine.UI "$UGUI/Runtime/UGUI" -r:"$PRE/Unity.InternalAPIEngineBridge.004.dll"
build Unity.TextMeshPro "$UGUI/Runtime/TMP" \
      -r:"$PRE/UnityEngine.UI.dll" -r:"$PRE/Unity.InternalAPIEngineBridge.004.dll"
build Unity.InputSystem "$IS/InputSystem" \
      -r:"$PRE/UnityEngine.UI.dll" -r:"$PRE/Unity.TextMeshPro.dll"

echo "완료:"; ls -la "$PRE"/*.dll
```

실행한다.

```bash
chmod +x Tools/setup-compile-refs.sh
./Tools/setup-compile-refs.sh
```

기대: 마지막에 `prebuilt/` DLL 4개가 나열된다.

- [ ] **Step 3: 컴파일 검증 스크립트 작성**

`Tools/compile-check.sh`:

```bash
#!/usr/bin/env bash
# Roslyn 컴파일 검증 — Unity 없이 타입/컴파일 오류를 확인한다.
#
# Unity의 어셈블리 분할을 그대로 흉내낸다:
#   1) Assembly-CSharp        Editor/ 밖.  UNITY_EDITOR 끔.  UnityEngine.* 만 참조
#   2) Assembly-CSharp-Editor Editor/ 안.  UNITY_EDITOR 켬.  + UnityEditor.* + 1)
#
# 그래서 어셈블리 경계를 넘는 internal 접근과, #if UNITY_EDITOR 밖으로 새어나온
# 에디터 API 사용이 잡힌다. 한 덩어리로 컴파일하면 둘 다 놓친다.
#
# 다만 동작 검증은 아니다. 통과했다고 "동작한다"고 보고하지 말 것 —
# 씬·프리팹 직렬화 참조, OnValidate/AssetPostprocessor 수명주기, 런타임 로직은
# Windows Unity에서만 확인된다.
#
# 준비물은 Tools/setup-compile-refs.sh가 만든다.
set -uo pipefail

ROOT="${ROOT_OVERRIDE:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
REFS="$HOME/unity-refs"
DOTNET="$HOME/.dotnet/dotnet"
OUT="${TMPDIR:-/tmp}/dice-orbit-compile"
mkdir -p "$OUT"

if [ ! -d "$REFS/Editor/Data/Managed/UnityEngine" ] || [ ! -d "$REFS/prebuilt" ]; then
  echo "ERROR: 참조 어셈블리가 없습니다. Tools/setup-compile-refs.sh를 먼저 실행하세요." >&2
  exit 2
fi

CSC="$(ls -d "$HOME"/.dotnet/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | sort | tail -1)"
if [ -z "$CSC" ]; then echo "ERROR: Roslyn csc.dll을 찾지 못했습니다." >&2; exit 2; fi

# ── 참조 ──
# UnityEngine.dll / UnityEditor.dll(집합체)은 제외한다 — 모듈 DLL과 같은 타입을 재노출해
# CS0433(타입 중복)을 5,000건 넘게 일으킨다. 실제 타입은 전부 *Module.dll에 있다.
#
# 런타임 어셈블리에는 UnityEditor.* 를 주지 않는다 — 실제 Unity도 주지 않는다.
# 이래야 #if UNITY_EDITOR 밖으로 새어나온 에디터 API 사용이 CS0234로 잡힌다.
RUNTIME_REFS=()
while IFS= read -r d; do RUNTIME_REFS+=("-r:$d"); done < <(
  find "$REFS/Editor/Data/Managed/UnityEngine" -name 'UnityEngine*.dll' ! -name 'UnityEngine.dll'
  find "$REFS/Editor/Data/NetStandard" -name 'netstandard.dll'
  find "$REFS/prebuilt" -name '*.dll'
)

EDITOR_REFS=("${RUNTIME_REFS[@]}")
while IFS= read -r d; do EDITOR_REFS+=("-r:$d"); done < <(
  find "$REFS/Editor/Data/Managed/UnityEngine" -name 'UnityEditor*.dll' ! -name 'UnityEditor.dll'
)

COMMON=(-nologo -noconfig -nostdlib -target:library -langversion:9 -unsafe -warn:0
        -define:UNITY_2020_1_OR_NEWER -define:UNITY_6000_0_OR_NEWER)

# ── 1) Assembly-CSharp (런타임) — UNITY_EDITOR를 켜지 않는다 ──
find "$ROOT/Assets/Scripts" -name '*.cs' -not -path '*/Editor/*' -printf '"%p"\n' > "$OUT/runtime.rsp"
echo "── Assembly-CSharp ($(wc -l < "$OUT/runtime.rsp"))"
"$DOTNET" "$CSC" "${COMMON[@]}" \
  -out:"$OUT/Assembly-CSharp.dll" "${RUNTIME_REFS[@]}" "@$OUT/runtime.rsp" \
  || { echo "COMPILE FAILED (Assembly-CSharp)"; exit 1; }

# ── 2) Assembly-CSharp-Editor — 1)을 참조 ──
find "$ROOT/Assets/Scripts" -name '*.cs' -path '*/Editor/*' -printf '"%p"\n' > "$OUT/editor.rsp"
if [ -s "$OUT/editor.rsp" ]; then
  echo "── Assembly-CSharp-Editor ($(wc -l < "$OUT/editor.rsp"))"
  "$DOTNET" "$CSC" "${COMMON[@]}" -define:UNITY_EDITOR \
    -out:"$OUT/Assembly-CSharp-Editor.dll" "${EDITOR_REFS[@]}" \
    -r:"$OUT/Assembly-CSharp.dll" "@$OUT/editor.rsp" \
    || { echo "COMPILE FAILED (Assembly-CSharp-Editor)"; exit 1; }
fi

echo "COMPILE OK"
```

```bash
chmod +x Tools/compile-check.sh
```

- [ ] **Step 4: 실패를 먼저 확인한다**

```bash
./Tools/compile-check.sh 2>&1 | tail -10
```

기대: `COMPILE FAILED (Assembly-CSharp)`와 함께 **정확히 아래 5건**의 `CS0234`.
다섯 파일 모두 `Editor/` 밖이므로 1단계에서 걸린다.

```
── Assembly-CSharp (169)
SkillData.cs(6,20):    'VisualScripting' does not exist in the namespace 'Unity'
OrbitManager.cs(6,13): 'VisualScripting' does not exist in the namespace 'Unity'
RandMineTile.cs(3,20): 'VisualScripting' does not exist in the namespace 'Unity'
RandMineTile.cs(5,36): 'DebugUI' does not exist in the namespace 'UnityEngine.Rendering'
LunaPriest.cs(11,36):  'DebugUI' does not exist in the namespace 'UnityEngine.Rendering'
COMPILE FAILED (Assembly-CSharp)
```

**다른 에러가 섞여 나오면 하네스 문제다.** 아직 코드를 하나도 바꾸지 않은 상태이므로,
그 에러는 참조 누락이나 `-define:` 부족을 뜻한다. 진단이 지목한 타입이 어느 패키지 것인지 보고
`setup-compile-refs.sh`에 그 패키지를 추가한 뒤 다시 선빌드한다. **기준선이 위 5건만 남을 때까지
태스크 1로 넘어가지 않는다.**

- [ ] **Step 5: 떠돌이 `using` 5줄 제거**

네 파일에 IDE가 자동 삽입한 미사용 `using`이 남아 있다. `Unity.VisualScripting`은 비주얼 스크립팅
패키지, `UnityEngine.Rendering.DebugUI`는 URP 것이며, 어느 파일도 해당 네임스페이스의 타입을
실제로 쓰지 않는다. 이걸 두면 검증 하네스가 두 패키지(소스 합계 1,500개 이상)를 함께 빌드해야 한다.

아래 줄들을 삭제한다.

`SkillData.cs:6`
```csharp
using static Unity.VisualScripting.Member;
```

`OrbitManager.cs:6`
```csharp
using Unity.VisualScripting.Antlr3.Runtime.Misc;
```

`RandMineTile.cs:3`
```csharp
using static Unity.VisualScripting.Member;
```

`RandMineTile.cs:5`
```csharp
using static UnityEngine.Rendering.DebugUI;
```

`LunaPriest.cs:11`
```csharp
using static UnityEngine.Rendering.DebugUI;
```

같은 파일들에 있는 `using static UnityEngine.GraphicsBuffer;`와
`using static UnityEngine.UI.GridLayoutGroup;`는 **건드리지 않는다.** 이들은 Unity 모듈·ugui에
실재하는 타입이라 컴파일에 지장이 없고, 이번 작업 범위 밖이다.

- [ ] **Step 6: 기준선 통과 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -5
```

기대 (약 0.8초):

```
── Assembly-CSharp (169)
── Assembly-CSharp-Editor (3)
COMPILE OK
```

- [ ] **Step 7: 커밋**

```bash
git add Tools/setup-compile-refs.sh Tools/compile-check.sh \
        "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Combat/SkillData/SkillData.cs" \
        "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/OrbitManager.cs" \
        "Assets/Scripts/Data/MonsterPresets/Wave1/Goblin/RandMineTile.cs" \
        "Assets/Scripts/Data/MonsterPresets/Wave4/LunaPriest/LunaPriest.cs"
git commit -m "chore: Roslyn 컴파일 검증 환경 추가

Unity 없이 타입·컴파일 오류를 확인한다. 리눅스 에디터 바이너리는 x86-64라
이 ARM64 VM에서 실행할 수 없지만(실측: ELF e_machine 0x3e, ARM64 빌드는
배포 안 됨), 매니지드 DLL은 순수 IL이라 참조는 된다.

Unity의 어셈블리 분할을 흉내내 두 번 컴파일한다 — Assembly-CSharp는
UNITY_EDITOR 없이 UnityEngine.*만 참조, Assembly-CSharp-Editor는 그 결과를
참조. 한 덩어리로 묶으면 어셈블리 경계 internal 접근과 #if 밖 에디터 API
사용을 둘 다 놓친다.

ugui/inputsystem 패키지 소스는 원래 어셈블리 이름으로 미리 DLL 빌드한다 —
이름이 다르면 Unity 모듈의 InternalsVisibleTo가 걸리지 않아 CS0122가 난다.

미사용 using 5줄(Unity.VisualScripting 3, UnityEngine.Rendering.DebugUI 2)을
함께 제거했다. 이게 있으면 하네스가 두 패키지를 통째로 빌드해야 한다."
```

---

## Task 1: 세 ScriptableObject에 `saveId` 추가

**Files:**
- Modify: `Assets/Scripts/Core/Run/Artifact/ArtifactData.cs`
- Modify: `Assets/Scripts/Core/Run/Potion.cs`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/CharacterPreset.cs`

**Interfaces:**
- Produces: `ArtifactData.SaveId`, `Potion.SaveId`, `CharacterPreset.SaveId` — 모두 `string` 읽기 전용
  프로퍼티. 태스크 2·9·10이 이걸 쓴다.

- [ ] **Step 1: `ArtifactData`에 필드 추가**

`Assets/Scripts/Core/Run/Artifact/ArtifactData.cs` 전체를 아래로 바꾼다.

```csharp
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 유물 에셋: 표시 데이터 + 로직 프로토타입 (스펙 2026-07-21).
    /// effect에 SubclassPicker로 유물 클래스를 고르고 파라미터를 인라인 튜닝한다.
    /// 획득 시 effect.CreateInstance(this)로 런타임 인스턴스가 만들어진다.
    /// </summary>
    [CreateAssetMenu(fileName = "New ArtifactData", menuName = "DiceOrbit/ArtifactData")]
    public class ArtifactData : ScriptableObject
    {
        [Tooltip("세이브 식별자 — 자동으로 채워집니다. 직접 수정하지 마세요.")]
        [SerializeField] private string saveId;

        /// <summary>세이브가 이 에셋을 다시 찾는 키. 한 번 정해지면 바뀌지 않는다.</summary>
        public string SaveId => saveId;

        public string artifactName = "유물 이름";
        [TextArea(2, 4)] public string artifactTooltip = "유물 설명";
        public Sprite artifactIcon;
        [Min(1)] public int shopPrice = 120;

        [Header("효과 — 유물 1개 = 클래스 1개 (Data/Artifacts/)")]
        [SerializeReference, SubclassPicker] public RuntimeArtifact effect;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 비어 있을 때만 = 최초 1회. 이후 파일명을 바꿔도 saveId는 그대로다.
            if (string.IsNullOrEmpty(saveId)) saveId = name;
        }
#endif
    }
}
```

- [ ] **Step 2: `Potion`에 필드 추가 — `protected virtual`**

`Assets/Scripts/Core/Run/Potion.cs`의 `Potion` 클래스에 아래를 추가한다(enum은 그대로 둔다).

```csharp
    public abstract class Potion : ScriptableObject
    {
        [Tooltip("세이브 식별자 — 자동으로 채워집니다. 직접 수정하지 마세요.")]
        [SerializeField] private string saveId;

        /// <summary>세이브가 이 에셋을 다시 찾는 키. 한 번 정해지면 바뀌지 않는다.</summary>
        public string SaveId => saveId;

        public string PotionName = "물약";
        [TextArea(2, 4)] public string Description = "물약 설명";
        public Sprite Icon;
        public PotionTargetType TargetType = PotionTargetType.None;
        [Min(1)] public int ShopPrice = 40;
        [Tooltip("전투 중에만 사용 가능")]
        public bool CombatOnly;

        /// <summary>
        /// 타겟을 받아 포션의 효과를 실행합니다.
        /// </summary>
        public abstract bool Use(Unit target = null);

#if UNITY_EDITOR
        /// <summary>
        /// protected virtual이어야 한다. Unity는 가장 파생된 클래스의 OnValidate 하나만 호출하므로,
        /// private으로 두면 HealPotion이 자기 OnValidate를 추가하는 순간 saveId 채우기가 조용히 멈춘다.
        /// 서브클래스가 OnValidate를 override하면 반드시 base.OnValidate()를 호출할 것.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(saveId)) saveId = name;
        }
#endif
    }
```

- [ ] **Step 3: `CharacterPreset`에 필드 추가**

`CharacterPreset.cs`의 클래스 선언 바로 다음(`[Header("Basic Info")]` 위)에 삽입한다.

```csharp
        [Tooltip("세이브 식별자 — 자동으로 채워집니다. 직접 수정하지 마세요.")]
        [SerializeField] private string saveId;

        /// <summary>세이브가 이 에셋을 다시 찾는 키. 한 번 정해지면 바뀌지 않는다.</summary>
        public string SaveId => saveId;
```

그리고 클래스 맨 끝(마지막 `}` 직전)에 삽입한다.

```csharp
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(saveId)) saveId = name;
        }
#endif
```

`CharacterPreset`은 서브클래스가 없으므로 `private`으로 둔다. 나중에 상속을 만들면
`protected virtual`로 바꿔야 한다.

- [ ] **Step 4: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Core/Run/Artifact/ArtifactData.cs \
        Assets/Scripts/Core/Run/Potion.cs \
        Assets/Scripts/Core/Stage/BattleStage/Units/Character/CharacterPreset.cs
git commit -m "feat: ArtifactData/Potion/CharacterPreset에 불변 saveId 추가

OnValidate가 최초 1회 에셋 파일명으로 채운다. 이후 표시 이름·파일명이
바뀌어도 세이브 키는 유지된다. Potion은 서브클래스를 가지므로
protected virtual — Unity가 최파생 OnValidate만 호출하기 때문."
```

---

## Task 2: `SaveIdCatalog` + 에디터 자동 재스캔

**Files:**
- Create: `Assets/Scripts/Core/Run/Save/SaveIdCatalog.cs` (+`.meta`)
- Create: `Assets/Scripts/Editor/SaveIdValidator.cs` (+`.meta`)
- Create: `Assets/Scripts/Editor/SaveIdCatalogPostprocessor.cs` (+`.meta`)

**Interfaces:**
- Consumes: `ArtifactData.SaveId`, `Potion.SaveId`, `CharacterPreset.SaveId` (태스크 1)
- Produces:
  - `SaveIdCatalog.Get()` → `SaveIdCatalog` (없으면 `null`)
  - `SaveIdCatalog.FindArtifact(string) → ArtifactData`
  - `SaveIdCatalog.FindPotion(string) → Potion`
  - `SaveIdCatalog.FindPreset(string) → CharacterPreset`
  - `SaveIdValidator.Rescan()` — 에디터 전용

- [ ] **Step 1: 카탈로그 본체 작성**

`Assets/Scripts/Core/Run/Save/SaveIdCatalog.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// saveId → 에셋 인덱스. 복원이 에셋을 되찾는 유일한 출처다.
    ///
    /// 획득 후보 풀(artifactPool/potionPool)과 역할이 다르다 — 풀은 상점 진열·드랍 후보를 담는
    /// 게임 디자인용 목록이라 의도적으로 일부만 담지만, 카탈로그는 프로젝트에 존재하는 모든
    /// 대상 에셋을 담아야 한다. 풀 밖 경로로도 유물·포션이 게임에 들어오기 때문이다.
    ///
    /// 내용은 에디터에서 자동 유지된다 (SaveIdCatalogPostprocessor).
    /// </summary>
    [CreateAssetMenu(fileName = "SaveIdCatalog", menuName = "DiceOrbit/SaveIdCatalog")]
    public class SaveIdCatalog : ScriptableObject
    {
        /// <summary>Resources.Load 경로. 에셋은 Assets/Resources/SaveIdCatalog.asset 이어야 한다.</summary>
        public const string ResourcePath = "SaveIdCatalog";

        [SerializeField] private List<ArtifactData> artifacts = new List<ArtifactData>();
        [SerializeField] private List<Potion> potions = new List<Potion>();
        [SerializeField] private List<CharacterPreset> presets = new List<CharacterPreset>();

        private Dictionary<string, ArtifactData> _artifactIndex;
        private Dictionary<string, Potion> _potionIndex;
        private Dictionary<string, CharacterPreset> _presetIndex;

        private static SaveIdCatalog _cached;

        /// <summary>Resources에서 카탈로그를 얻는다. 없으면 null.</summary>
        public static SaveIdCatalog Get()
        {
            if (_cached == null) _cached = Resources.Load<SaveIdCatalog>(ResourcePath);
            return _cached;
        }

        public ArtifactData FindArtifact(string saveId)
        {
            if (_artifactIndex == null) _artifactIndex = BuildIndex(artifacts, a => a.SaveId, "유물");
            return Lookup(_artifactIndex, saveId);
        }

        public Potion FindPotion(string saveId)
        {
            if (_potionIndex == null) _potionIndex = BuildIndex(potions, p => p.SaveId, "포션");
            return Lookup(_potionIndex, saveId);
        }

        public CharacterPreset FindPreset(string saveId)
        {
            if (_presetIndex == null) _presetIndex = BuildIndex(presets, p => p.SaveId, "프리셋");
            return Lookup(_presetIndex, saveId);
        }

        private static T Lookup<T>(Dictionary<string, T> index, string saveId) where T : Object
        {
            if (string.IsNullOrEmpty(saveId)) return null;
            return index.TryGetValue(saveId, out var found) ? found : null;
        }

        /// <summary>인덱스를 만드는 이 지점이 곧 런타임 중복·빈 saveId 검출 지점이다.</summary>
        private static Dictionary<string, T> BuildIndex<T>(
            List<T> source, System.Func<T, string> idOf, string label) where T : Object
        {
            var map = new Dictionary<string, T>(source.Count);
            foreach (var item in source)
            {
                if (item == null) continue;

                string id = idOf(item);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogError($"[SaveIdCatalog] {label} '{item.name}'의 saveId가 비어 있습니다 — 복원 불가.");
                    continue;
                }
                if (map.ContainsKey(id))
                {
                    Debug.LogError($"[SaveIdCatalog] {label} saveId 중복 '{id}' — '{map[id].name}' vs '{item.name}'.");
                    continue;
                }
                map[id] = item;
            }
            return map;
        }

#if UNITY_EDITOR
        // 아래 둘은 internal이 아니라 public이어야 한다. Assets/Scripts/Editor/ 아래 코드는
        // Assembly-CSharp-Editor라는 별도 어셈블리로 컴파일되므로 internal이 보이지 않는다.
        // compile-check.sh는 전부를 한 어셈블리로 묶어 컴파일하므로 이 실수를 잡지 못한다.

        /// <summary>에디터 재스캔 전용. 런타임에서 호출하지 않는다.</summary>
        public void EditorSetContents(
            List<ArtifactData> newArtifacts, List<Potion> newPotions, List<CharacterPreset> newPresets)
        {
            artifacts = newArtifacts;
            potions = newPotions;
            presets = newPresets;
            _artifactIndex = null;
            _potionIndex = null;
            _presetIndex = null;
        }

        /// <summary>재스캔 결과가 기존과 같은지 — 같으면 에셋을 더럽히지 않는다(임포트 루프 방지).</summary>
        public bool EditorContentsEqual(
            List<ArtifactData> newArtifacts, List<Potion> newPotions, List<CharacterPreset> newPresets)
        {
            return SameSequence(artifacts, newArtifacts)
                && SameSequence(potions, newPotions)
                && SameSequence(presets, newPresets);
        }

        private static bool SameSequence<T>(List<T> a, List<T> b) where T : Object
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }
#endif
    }
}
```

- [ ] **Step 2: 재스캔 본체 작성**

`Assets/Scripts/Editor/SaveIdValidator.cs`:

```csharp
#if UNITY_EDITOR
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Core.Run.Save;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// SaveIdCatalog를 프로젝트 전체 스캔으로 채우고, 빈 saveId를 굽고, 중복을 보고한다.
    /// 자동 호출은 SaveIdCatalogPostprocessor가 한다.
    /// </summary>
    public static class SaveIdValidator
    {
        // 재스캔이 SaveAssets를 부르면 다시 OnPostprocessAllAssets가 도므로 재진입을 막는다.
        private static bool _running;

        [MenuItem("도구/Dice Orbit/세이브 ID 전체 점검")]
        public static void RescanFromMenu()
        {
            Rescan();
            Debug.Log("[SaveId] 전체 점검 완료 — 위 콘솔에 에러가 없으면 정상입니다.");
        }

        public static void Rescan()
        {
            if (_running) return;
            _running = true;
            try
            {
                var catalog = FindCatalog();
                if (catalog == null)
                {
                    Debug.LogWarning(
                        "[SaveId] SaveIdCatalog 에셋이 없습니다. " +
                        "Create > DiceOrbit > SaveIdCatalog 로 만들어 Assets/Resources/SaveIdCatalog.asset 에 두세요.");
                    return;
                }

                var artifacts = LoadAll<ArtifactData>();
                var potions   = LoadAll<Potion>();
                var presets   = LoadAll<CharacterPreset>();

                FillMissingIds(artifacts, a => a.SaveId, "유물");
                FillMissingIds(potions,   p => p.SaveId, "포션");
                FillMissingIds(presets,   p => p.SaveId, "프리셋");

                ReportDuplicates(artifacts, a => a.SaveId, "유물");
                ReportDuplicates(potions,   p => p.SaveId, "포션");
                ReportDuplicates(presets,   p => p.SaveId, "프리셋");

                if (catalog.EditorContentsEqual(artifacts, potions, presets)) return;

                catalog.EditorSetContents(artifacts, potions, presets);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
                Debug.Log($"[SaveId] 카탈로그 갱신 — 유물 {artifacts.Count} · 포션 {potions.Count} · 프리셋 {presets.Count}");
            }
            finally
            {
                _running = false;
            }
        }

        private static SaveIdCatalog FindCatalog()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:SaveIdCatalog"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var found = AssetDatabase.LoadAssetAtPath<SaveIdCatalog>(path);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>t:이름 검색은 파생 타입도 잡는다 (t:Potion → HealPotion 포함).</summary>
        private static List<T> LoadAll<T>() where T : Object
        {
            var result = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) result.Add(asset);
            }
            return result;
        }

        /// <summary>
        /// OnValidate가 놓친 에셋(임포트 전이었던 것 등)의 saveId를 굽는다.
        /// SerializedObject를 쓰는 이유는 saveId가 private 필드이기 때문이다.
        /// </summary>
        private static void FillMissingIds<T>(List<T> assets, System.Func<T, string> idOf, string label)
            where T : Object
        {
            foreach (var asset in assets)
            {
                if (!string.IsNullOrEmpty(idOf(asset))) continue;

                var so = new SerializedObject(asset);
                var prop = so.FindProperty("saveId");
                if (prop == null)
                {
                    Debug.LogError($"[SaveId] {label} '{asset.name}'에 saveId 필드가 없습니다.");
                    continue;
                }
                prop.stringValue = asset.name;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                Debug.Log($"[SaveId] {label} '{asset.name}'의 saveId를 '{asset.name}'으로 채웠습니다.");
            }
        }

        private static void ReportDuplicates<T>(List<T> assets, System.Func<T, string> idOf, string label)
            where T : Object
        {
            var seen = new Dictionary<string, T>(assets.Count);
            foreach (var asset in assets)
            {
                string id = idOf(asset);
                if (string.IsNullOrEmpty(id)) continue;
                if (seen.TryGetValue(id, out var other))
                {
                    Debug.LogError(
                        $"[SaveId] {label} saveId 중복 '{id}' — '{other.name}' 와 '{asset.name}'. " +
                        "에셋을 복제(Ctrl+D)하면 saveId까지 복사됩니다. 한쪽을 고쳐 주세요.", asset);
                    continue;
                }
                seen[id] = asset;
            }
        }
    }
}
#endif
```

- [ ] **Step 3: 임포트 훅 작성**

`Assets/Scripts/Editor/SaveIdCatalogPostprocessor.cs`:

```csharp
#if UNITY_EDITOR
using UnityEditor;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 대상 에셋이 임포트·삭제·이동될 때 카탈로그를 자동 재스캔한다.
    /// 수동 등록 단계를 두지 않는 것이 이 설계의 전제다 — 등록을 깜빡해서 복원이 깨진다면
    /// 획득 후보 풀을 조회 출처로 쓰던 때와 달라지는 게 없다.
    /// </summary>
    public class SaveIdCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!HasAssetFile(importedAssets)
                && !HasAssetFile(deletedAssets)
                && !HasAssetFile(movedAssets)) return;

            SaveIdValidator.Rescan();
        }

        private static bool HasAssetFile(string[] paths)
        {
            foreach (var path in paths)
                if (path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
#endif
```

- [ ] **Step 4: `.meta` 파일 생성**

Unity가 `.meta`를 만들 수 없는 환경이므로 손으로 만든다. GUID는 32자리 16진수이며 프로젝트 안에서
유일해야 한다. 아래로 생성한다.

```bash
mkdir -p Assets/Scripts/Core/Run/Save

for f in "Assets/Scripts/Core/Run/Save/SaveIdCatalog.cs" \
         "Assets/Scripts/Editor/SaveIdValidator.cs" \
         "Assets/Scripts/Editor/SaveIdCatalogPostprocessor.cs"; do
  guid=$(python3 -c "import uuid;print(uuid.uuid4().hex)")
  printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" > "$f.meta"
done
```

폴더에도 `.meta`가 필요하다.

```bash
guid=$(python3 -c "import uuid;print(uuid.uuid4().hex)")
printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" > "Assets/Scripts/Core/Run/Save.meta"
```

GUID 중복이 없는지 확인한다.

```bash
grep -rhno "^guid: .*" Assets --include=*.meta | sed 's/.*guid: //' | sort | uniq -d
```

기대: 출력 없음(중복 0건).

- [ ] **Step 5: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Core/Run/Save.meta \
        Assets/Scripts/Core/Run/Save/SaveIdCatalog.cs \
        Assets/Scripts/Core/Run/Save/SaveIdCatalog.cs.meta \
        Assets/Scripts/Editor/SaveIdValidator.cs \
        Assets/Scripts/Editor/SaveIdValidator.cs.meta \
        Assets/Scripts/Editor/SaveIdCatalogPostprocessor.cs \
        Assets/Scripts/Editor/SaveIdCatalogPostprocessor.cs.meta
git commit -m "feat: SaveIdCatalog + 에디터 자동 재스캔 추가

복원의 에셋 조회 출처를 획득 후보 풀에서 분리한다. 카탈로그는
AssetDatabase 스캔으로 프로젝트 전체를 담고, AssetPostprocessor가
임포트마다 자동 갱신하므로 수동 등록 단계가 없다.

인덱스 빌드와 재스캔이 각각 런타임·에디터의 중복 saveId 검출 지점이다."
```

---

## Task 3: v2 DTO

**Files:**
- Create: `Assets/Scripts/Core/Run/Save/RunSaveData.cs` (+`.meta`)

**Interfaces:**
- Produces: `RunSaveData`, `RunProgressSave`, `ArtifactSaveData`, `PotionSaveData`,
  `ModifierSaveData`, `CharacterSaveData` — 태스크 4·5·8·9·10·11이 전부 쓴다.

- [ ] **Step 1: DTO 작성**

`Assets/Scripts/Core/Run/Save/RunSaveData.cs`:

```csharp
using System.Collections.Generic;

namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 런 저장 데이터 v2 — 노드 단위 스냅샷 (전투 중 저장 없음).
    /// 각 필드의 주인 참가자는 하나로 고정한다. 두 참가자가 같은 필드를 건드리지 않는다.
    /// </summary>
    [System.Serializable]
    public class RunSaveData
    {
        public int Version = 2;

        public RunProgressSave Progress = new RunProgressSave();                  // 주인: RunManager
        public int Gold;                                                          // 주인: GoldManager
        public List<ArtifactSaveData>  Artifacts = new List<ArtifactSaveData>();  // 주인: ArtifactManager
        public List<PotionSaveData>    Potions   = new List<PotionSaveData>();    // 주인: PotionManager
        public List<CharacterSaveData> Party     = new List<CharacterSaveData>(); // 주인: PartyManager
    }

    [System.Serializable]
    public class RunProgressSave
    {
        public int Seed;                    // 같은 시드 → MapGenerator가 같은 맵 재생성
        public int CurrentNodeId = -1;
        public List<int> VisitedNodeIds = new List<int>();
        public int BattlesCleared;

        // 엔티티 인스턴스가 아니라 집합 소속 여부라 부가 상태가 붙을 자리가 없다 → string 그대로.
        public List<string> BanishedPresetIds = new List<string>();
    }

    // 지금은 Id 하나뿐이어도 래퍼를 둔다. RuntimeArtifact가 상태(발동 횟수, 충전 등)를 갖게 되면
    // List<string> → List<...> 전환은 저장·복원 양쪽을 다시 쓰는 일이 되지만, 래퍼가 있으면
    // 필드 한 줄 추가로 끝난다. JsonUtility는 List<중첩 [Serializable] 클래스>를 문제없이 다룬다.

    [System.Serializable] public class ArtifactSaveData { public string Id; }   // ArtifactData.SaveId
    [System.Serializable] public class PotionSaveData   { public string Id; }   // Potion.SaveId
    [System.Serializable] public class ModifierSaveData { public string Id; }   // 모디파이어 클래스 타입명

    [System.Serializable]
    public class CharacterSaveData
    {
        public string PresetId;                                                // CharacterPreset.SaveId
        public int CurrentHp;
        public int MaxHp;
        public int RevivalStock;
        public List<ModifierSaveData> Modifiers = new List<ModifierSaveData>();
    }
}
```

- [ ] **Step 2: `.meta` 생성**

```bash
guid=$(python3 -c "import uuid;print(uuid.uuid4().hex)")
printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" > "Assets/Scripts/Core/Run/Save/RunSaveData.cs.meta"
```

- [ ] **Step 3: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

구 `RunSaveService.cs`에도 `RunSaveData`가 있지만 네임스페이스가 달라(`DiceOrbit.Core.Run` vs
`DiceOrbit.Core.Run.Save`) 충돌하지 않는다. 태스크 12에서 구 파일을 지운다.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Core/Run/Save/RunSaveData.cs Assets/Scripts/Core/Run/Save/RunSaveData.cs.meta
git commit -m "feat: 세이브 v2 DTO 추가

저장 대상마다 전용 클래스를 둔다. 지금은 Id 하나뿐이어도 마찬가지로,
나중에 유물이 상태를 갖게 되면 필드 한 줄 추가로 끝나게 하기 위함이다."
```

---

## Task 4: 참가자 계약 — `RestoreReport` · `RunRestoreContext` · `IRunSaveParticipant`

**Files:**
- Create: `Assets/Scripts/Core/Run/Save/RestoreReport.cs` (+`.meta`)
- Create: `Assets/Scripts/Core/Run/Save/RunRestoreContext.cs` (+`.meta`)
- Create: `Assets/Scripts/Core/Run/Save/IRunSaveParticipant.cs` (+`.meta`)

**Interfaces:**
- Consumes: `RunSaveData` (태스크 3), `SaveIdCatalog` (태스크 2)
- Produces:
  - `RestoreReport.Fail(string)` / `.Warn(string)` / `.Success` / `.Failures` / `.Warnings`
  - `RunRestoreContext.Spawner` (`CharacterSpawner`) / `.Catalog` (`SaveIdCatalog`) /
    `.Report` (`RestoreReport`) / `.FindPreset(string) → CharacterPreset`
  - `IRunSaveParticipant.Capture(RunSaveData)` /
    `.Validate(RunSaveData, RunRestoreContext)` / `.Apply(RunSaveData, RunRestoreContext)`

- [ ] **Step 1: `RestoreReport` 작성**

`Assets/Scripts/Core/Run/Save/RestoreReport.cs`:

```csharp
using System.Collections.Generic;
using System.Text;

namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 복원 중 수집한 실패·경고. 실패가 하나라도 있으면 복원을 포기한다.
    ///
    /// saveId 도입 이후 복원 실패는 플레이어 상황이 아니라 개발 중 버그를 뜻한다.
    /// 조용히 반쪽으로 복원되면 그 버그를 놓치므로, "이어할 수 없습니다"가 정직하다.
    /// </summary>
    public class RestoreReport
    {
        public readonly List<string> Failures = new List<string>();   // 하나라도 있으면 복원 포기
        public readonly List<string> Warnings = new List<string>();   // 진행을 막지 않음

        public bool Success => Failures.Count == 0;

        public void Fail(string message) => Failures.Add(message);
        public void Warn(string message) => Warnings.Add(message);

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var f in Failures) sb.AppendLine("[실패] " + f);
            foreach (var w in Warnings) sb.AppendLine("[경고] " + w);
            return sb.Length == 0 ? "(문제 없음)" : sb.ToString().TrimEnd();
        }
    }
}
```

- [ ] **Step 2: `RunRestoreContext` 작성**

`Assets/Scripts/Core/Run/Save/RunRestoreContext.cs`:

```csharp
namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 복원에 필요한 협력자 묶음. 서비스가 찾아서 참가자에게 주입한다.
    /// PartyManager가 UI를 직접 뒤지지 않게 하려는 것이다.
    /// </summary>
    public class RunRestoreContext
    {
        public CharacterSpawner Spawner;
        public SaveIdCatalog Catalog;
        public RestoreReport Report;

        /// <summary>못 찾으면 null. 카탈로그가 없으면 항상 null.</summary>
        public CharacterPreset FindPreset(string saveId)
            => Catalog != null ? Catalog.FindPreset(saveId) : null;
    }
}
```

`CharacterSpawner`와 `CharacterPreset`은 `DiceOrbit.Core`에 있고 이 네임스페이스가 그 아래에
중첩돼 있으므로 `using` 없이 해석된다.

- [ ] **Step 3: `IRunSaveParticipant` 작성**

`Assets/Scripts/Core/Run/Save/IRunSaveParticipant.cs`:

```csharp
namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 매니저가 자기 상태의 저장·복원을 직접 소유하기 위한 계약.
    ///
    /// Validate/Apply 2단계 분리가 핵심이다. 검증은 아무것도 바꾸지 않고 "이 세이브의 모든 ID를
    /// 카탈로그·레지스트리에서 해결할 수 있는가"만 확인한다. 참가자 전원이 통과했을 때만 Apply로
    /// 넘어가므로 "골드는 넣었는데 파티 스폰 중 실패" 같은 중간 상태가 생기지 않는다 — 롤백 코드가 없다.
    /// </summary>
    public interface IRunSaveParticipant
    {
        /// <summary>현재 상태 → DTO.</summary>
        void Capture(RunSaveData data);

        /// <summary>부작용 없이 해결 가능성만 확인. 실패는 ctx.Report.Fail에 기록한다.</summary>
        void Validate(RunSaveData data, RunRestoreContext ctx);

        /// <summary>참가자 전원이 검증을 통과한 뒤에만 호출된다.</summary>
        void Apply(RunSaveData data, RunRestoreContext ctx);
    }
}
```

- [ ] **Step 4: `.meta` 3개 생성**

```bash
for f in "Assets/Scripts/Core/Run/Save/RestoreReport.cs" \
         "Assets/Scripts/Core/Run/Save/RunRestoreContext.cs" \
         "Assets/Scripts/Core/Run/Save/IRunSaveParticipant.cs"; do
  guid=$(python3 -c "import uuid;print(uuid.uuid4().hex)")
  printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" > "$f.meta"
done
```

- [ ] **Step 5: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Core/Run/Save/RestoreReport.cs* \
        Assets/Scripts/Core/Run/Save/RunRestoreContext.cs* \
        Assets/Scripts/Core/Run/Save/IRunSaveParticipant.cs*
git commit -m "feat: 참가자 계약 3종 추가 (보고서·컨텍스트·인터페이스)

Validate/Apply 2단계 분리로 부분 적용 상태를 원천 차단한다.
검증 전원 통과 전에는 어떤 참가자도 상태를 바꾸지 않으므로 롤백이 없다."
```

---

## Task 5: 파일 I/O — `RunSaveFile`

**Files:**
- Create: `Assets/Scripts/Core/Run/Save/RunSaveFile.cs` (+`.meta`)

**Interfaces:**
- Consumes: `RunSaveData` (태스크 3), `RestoreReport` (태스크 4)
- Produces:
  - `RunSaveFile.CurrentVersion` (`const int` = 2)
  - `RunSaveFile.Write(RunSaveData)`
  - `RunSaveFile.Read(RestoreReport) → RunSaveData` (실패 시 `null`)
  - `RunSaveFile.HasValidSave() → bool`
  - `RunSaveFile.Delete()`

- [ ] **Step 1: 작성**

`Assets/Scripts/Core/Run/Save/RunSaveFile.cs`:

```csharp
using System.IO;
using UnityEngine;

namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 세이브 파일 계층 전담. 매니저도 서비스도 File API를 직접 만지지 않는다.
    /// </summary>
    public static class RunSaveFile
    {
        public const int CurrentVersion = 2;

        private static string Dir         => Application.persistentDataPath;
        private static string SavePath    => Path.Combine(Dir, "run_save.json");
        private static string BakPath     => Path.Combine(Dir, "run_save.bak");
        private static string TmpPath     => Path.Combine(Dir, "run_save.tmp");
        private static string CorruptPath => Path.Combine(Dir, "run_save.corrupt.json");

        /// <summary>임시 파일에 먼저 쓰고 교체한다 — 저장 중 크래시해도 기존 세이브가 살아남는다.</summary>
        public static void Write(RunSaveData data)
        {
            try
            {
                File.WriteAllText(TmpPath, JsonUtility.ToJson(data, true));

                if (!File.Exists(SavePath))
                {
                    File.Move(TmpPath, SavePath);
                }
                else
                {
                    try
                    {
                        File.Replace(TmpPath, SavePath, BakPath);   // 원자적 교체 + 백업
                    }
                    catch (System.Exception)
                    {
                        // File.Replace는 플랫폼에 따라 동작이 다르다 (일부 파일시스템 미지원).
                        if (File.Exists(BakPath)) File.Delete(BakPath);
                        File.Move(SavePath, BakPath);
                        File.Move(TmpPath, SavePath);
                    }
                }

                Debug.Log($"[RunSave] 저장됨 — 노드 {data.Progress.CurrentNodeId}, 파티 {data.Party.Count}명");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RunSave] 저장 실패: {ex.Message}");
            }
        }

        /// <summary>3단 방어: 본 파일 → .bak → 손상 파일 보존 후 null.</summary>
        public static RunSaveData Read(RestoreReport report)
        {
            var data = TryParse(SavePath);
            if (data != null) return data;

            data = TryParse(BakPath);
            if (data != null)
            {
                report?.Warn("본 세이브가 손상되어 .bak에서 복구했습니다.");
                return data;
            }

            // 손상 파일을 지우지 않고 남긴다 — 개발 중 재현을 위해서다.
            if (File.Exists(SavePath))
            {
                try
                {
                    if (File.Exists(CorruptPath)) File.Delete(CorruptPath);
                    File.Move(SavePath, CorruptPath);
                    report?.Fail($"세이브를 읽을 수 없습니다. 손상 파일을 {CorruptPath}에 보존했습니다.");
                }
                catch (System.Exception ex)
                {
                    report?.Fail($"세이브를 읽을 수 없고 손상 파일 보존도 실패했습니다: {ex.Message}");
                }
            }
            else
            {
                report?.Fail("세이브 파일이 없습니다.");
            }
            return null;
        }

        /// <summary>파일 존재만이 아니라 파싱과 버전 검사까지 통과해야 true.</summary>
        public static bool HasValidSave() => TryParse(SavePath) != null || TryParse(BakPath) != null;

        public static void Delete()
        {
            SafeDelete(SavePath);
            SafeDelete(BakPath);
            SafeDelete(TmpPath);
        }

        private static RunSaveData TryParse(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(path));
                if (data == null) return null;
                if (data.Version != CurrentVersion) return null;   // 마이그레이션 없음 — 폐기
                return data;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (System.Exception ex) { Debug.LogWarning($"[RunSave] 삭제 실패 {path}: {ex.Message}"); }
        }
    }
}
```

- [ ] **Step 2: `.meta` 생성**

```bash
guid=$(python3 -c "import uuid;print(uuid.uuid4().hex)")
printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" > "Assets/Scripts/Core/Run/Save/RunSaveFile.cs.meta"
```

- [ ] **Step 3: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Core/Run/Save/RunSaveFile.cs Assets/Scripts/Core/Run/Save/RunSaveFile.cs.meta
git commit -m "feat: 원자적 쓰기와 3단 읽기 방어를 갖춘 RunSaveFile 추가

.tmp에 먼저 쓰고 File.Replace로 교체하므로 저장 중 크래시해도 직전
세이브가 남는다. 읽기는 본 파일 → .bak → 손상 파일 보존 순이다.
HasValidSave는 존재가 아니라 파싱+버전 검사까지 통과해야 true."
```

---

## Task 6: `ModifierRegistry`에 ID 조회 API 추가

**Files:**
- Modify: `Assets/Scripts/Data/Modifiers/ModifierRegistry.cs`

**Interfaces:**
- Produces: `ModifierRegistry.Create(string id) → CharacterModifier` (없으면 `null`),
  `ModifierRegistry.Exists(string id) → bool`. 태스크 10이 쓴다.

- [ ] **Step 1: 두 메서드 추가**

`ModifierRegistry` 클래스 안, `CreateAll()` 바로 위에 삽입한다.

```csharp
        // 팩토리를 한 번씩만 돌려 얻은 타입명 캐시 — Exists가 매번 인스턴스를 만들지 않게 한다.
        private static string[] _ids;

        private static string[] Ids
        {
            get
            {
                if (_ids == null)
                {
                    _ids = new string[Factories.Length];
                    for (int i = 0; i < Factories.Length; i++) _ids[i] = Factories[i]().GetType().Name;
                }
                return _ids;
            }
        }

        /// <summary>
        /// 세이브 ID(클래스 타입명)로 모디파이어 새 인스턴스 생성. 없으면 null.
        /// 캐릭터마다 독립 인스턴스가 필요하므로 매번 새로 만든다.
        /// </summary>
        public static CharacterModifier Create(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var ids = Ids;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == id) return Factories[i]();
            return null;
        }

        /// <summary>Validate 단계용 — 인스턴스를 만들지 않는다.</summary>
        public static bool Exists(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var known in Ids)
                if (known == id) return true;
            return false;
        }
```

- [ ] **Step 2: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Data/Modifiers/ModifierRegistry.cs
git commit -m "feat: ModifierRegistry에 Create(id)/Exists(id) 추가

세이브 복원이 표시용 ModifierName(한글) 대신 클래스 타입명으로 조회한다.
모디파이어 하나마다 CreateAll()을 새로 호출하던 낭비도 이걸로 사라진다."
```

---

## Task 7: 디버그 스캐폴딩 삭제 + `ClearAll()` 추가

**Files:**
- Modify: `Assets/Scripts/Core/Run/Artifact/ArtifactManager.cs`
- Modify: `Assets/Scripts/Core/Run/PotionManager.cs`
- Modify: `Assets/Scripts/Core/Stage/PartyManager.cs`

**Interfaces:**
- Produces: `ArtifactManager.ClearAll()`, `PotionManager.ClearAll()`, `PartyManager.ClearAll()`.
  태스크 9·10의 `Apply`가 쓴다.

**왜 스캐폴딩을 지우는가:** `ScriptableObject.CreateInstance`로 런타임 생성한 SO에는
`OnValidate`가 호출되지 않아 `saveId`가 빈 채로 남는다. "빈 `saveId`는 복원 실패" 정책과 정면
충돌하므로, 런타임 생성 경로가 남아 있는 한 세이브가 성립하지 않는다.

**주의:** `FindInPool`은 아직 지우지 않는다. 구 `GameFlowManager.ContinueGameFlow`가 호출하고
있어 지금 지우면 컴파일이 깨진다. 태스크 12에서 함께 지운다.

- [ ] **Step 1: `ArtifactManager` — 필드·메서드 삭제**

아래 필드를 삭제한다(`[Header("시작 유물 ...")]` 줄 포함).

```csharp
        [Header("시작 유물 — 게임 시작 시 바로 보유 (테스트/디버그용)")]
        [SerializeField] private List<ArtifactData> startingArtifacts = new List<ArtifactData>();
```

`Awake()`를 아래로 바꾼다.

```csharp
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
```

파일 끝의 `EnsureDefaultPool()`과 `CreateDefault(...)` 두 메서드를 통째로 삭제한다
(`// ── 기본 풀 (에셋 미지정 폴백) ──` 주석 줄도 함께).

- [ ] **Step 2: `ArtifactManager` — `ClearAll()` 추가**

`RemoveArtifact(RuntimeArtifact artifact)` 바로 아래에 삽입한다.

```csharp
        /// <summary>보유 유물 전부 제거 — 세이브 복원이 이전 런의 잔여 상태 위에 덮어쓰지 않게 한다.</summary>
        public void ClearAll()
        {
            if (artifacts.Count == 0) return;
            artifacts.Clear();
            OnArtifactsChanged?.Invoke();
        }
```

- [ ] **Step 3: `PotionManager` — 필드·메서드 삭제**

아래 필드를 삭제한다.

```csharp
        [Header("시작 포션 — 게임 시작 시 슬롯에 지급 (테스트/디버그용)")]
        [SerializeField] private List<Potion> startingPotions = new List<Potion>();
```

`Awake()`를 아래로 바꾼다.

```csharp
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
```

파일 끝의 `// ── 기본 풀 (에셋 미지정 폴백) ──` 절 전체 — `RuntimePotion` 중첩 클래스,
`EnsureDefaultPool()`(주석 처리된 본문 포함), `CreateDefault(...)` — 를 삭제한다.

- [ ] **Step 4: `PotionManager` — `ClearAll()` 추가**

`Discard(int index)` 바로 아래에 삽입한다.

```csharp
        /// <summary>슬롯 전부 비우기 — 세이브 복원이 이전 런의 잔여 상태 위에 덮어쓰지 않게 한다.</summary>
        public void ClearAll()
        {
            if (_slots.Count == 0) return;
            _slots.Clear();
            OnChanged?.Invoke();
        }
```

- [ ] **Step 5: `PartyManager` — `ClearAll()` 추가**

`RemoveCharacter(Character character)` 메서드 바로 아래에 삽입한다.

```csharp
        /// <summary>
        /// 파티 전원 제거 + 오브젝트 파괴 — 세이브 복원이 이전 런의 잔여 파티 위에 스폰하지 않게 한다.
        /// </summary>
        public void ClearAll()
        {
            for (int i = party.Count - 1; i >= 0; i--)
            {
                var character = party[i];
                if (character != null) Destroy(character.gameObject);
            }
            party.Clear();
            selectedCharacter = null;
            OnPartyChanged?.Invoke(party.Count);
        }
```

- [ ] **Step 6: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

실패하면 지운 심볼을 다른 곳에서 참조하고 있다는 뜻이다. 진단이 가리키는 파일을 확인하고,
`startingArtifacts`/`startingPotions`/`EnsureDefaultPool`/`CreateDefault`/`RuntimePotion`을
쓰는 곳이 정말 있다면 계획을 벗어나므로 사용자에게 보고한다.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Core/Run/Artifact/ArtifactManager.cs \
        Assets/Scripts/Core/Run/PotionManager.cs \
        Assets/Scripts/Core/Stage/PartyManager.cs
git commit -m "refactor: 런타임 기본 풀·시작 목록 삭제, ClearAll 추가

CreateInstance로 만든 SO에는 OnValidate가 돌지 않아 saveId가 빈 채로
남는다. '빈 saveId는 복원 실패' 정책과 충돌하므로 런타임 생성 경로를
제거한다. 모든 유물·포션은 대응하는 .asset을 갖는다.

ClearAll은 복원이 이전 런의 잔여 상태 위에 덮어쓰지 않게 한다."
```

> **핸드오프 발생:** 이 커밋 이후 `BattleScene`의 획득 후보가 0개가 된다. Windows Unity에서
> 유물·포션 에셋을 저작해 `artifactPool`/`potionPool`에 등록해야 게임이 이전 수준으로 굴러간다.
> 자세한 목록은 이 문서 맨 끝의 핸드오프 절 참조.

---

## Task 8: 참가자 구현 — `RunManager` · `GoldManager`

**Files:**
- Modify: `Assets/Scripts/Core/Run/RunManager.cs`
- Modify: `Assets/Scripts/Core/GoldManager.cs`

**Interfaces:**
- Consumes: `IRunSaveParticipant`, `RunRestoreContext`, `RestoreReport` (태스크 4),
  `RunSaveData`·`RunProgressSave` (태스크 3), `CharacterPreset.SaveId` (태스크 1)
- Produces: `RunManager`와 `GoldManager`가 `IRunSaveParticipant`를 구현한다. 태스크 11이 목록에 넣는다.

- [ ] **Step 1: `RunManager` 선언과 using 변경**

```csharp
using System.Collections.Generic;
using DiceOrbit.Core.Run.Save;
using UnityEngine;
```

클래스 선언을 바꾼다.

```csharp
    public class RunManager : MonoBehaviour, IRunSaveParticipant
```

- [ ] **Step 2: `RunManager` — `RestoreRun`과 `BanishedNames` 삭제**

`RestoreRun(int, int, List<int>, int)` 메서드 전체를 삭제한다. 유일한 호출자는 구
`GameFlowManager.ContinueGameFlow`이며 태스크 12에서 사라진다.

`BanishedNames` 프로퍼티도 삭제한다. 유일한 사용처가 구 `RunSaveService`다.

```csharp
        public IEnumerable<string> BanishedNames
        {
            get { foreach (var p in _banishedPresets) if (p != null) yield return p.CharacterName; }
        }
```

- [ ] **Step 3: `RunManager` — 참가자 구현 추가**

`EndRun()` 바로 아래에 삽입한다.

```csharp
        // ── 세이브 참가자 ──────────────────────────────────────

        public void Capture(RunSaveData data)
        {
            var p = data.Progress;
            p.Seed = CurrentSeed;
            p.CurrentNodeId = CurrentNode != null ? CurrentNode.Id : -1;
            p.BattlesCleared = BattlesCleared;

            p.VisitedNodeIds.Clear();
            if (Map != null)
                foreach (var node in Map.Nodes)
                    if (node.Visited) p.VisitedNodeIds.Add(node.Id);

            p.BanishedPresetIds.Clear();
            foreach (var preset in _banishedPresets)
                if (preset != null) p.BanishedPresetIds.Add(preset.SaveId);
        }

        public void Validate(RunSaveData data, RunRestoreContext ctx)
        {
            if (firstAct == null)
                ctx.Report.Fail("RunManager.firstAct가 지정되지 않아 맵을 복원할 수 없습니다.");
        }

        public void Apply(RunSaveData data, RunRestoreContext ctx)
        {
            var p = data.Progress;

            CurrentAct = firstAct;
            CurrentSeed = p.Seed;
            Map = MapGenerator.Generate(CurrentAct, p.Seed);   // 같은 시드 → 같은 맵
            _banishedPresets.Clear();

            foreach (int id in p.VisitedNodeIds)
            {
                var node = Map.Get(id);
                if (node != null) node.Visited = true;
            }

            _currentNodeId = p.CurrentNodeId;
            BattlesCleared = p.BattlesCleared;

            // 소멸 캐릭터를 못 찾아도 런은 정상 진행된다 — 그 캐릭터가 다시 영입 가능해질 뿐.
            foreach (string saveId in p.BanishedPresetIds)
            {
                var preset = ctx.FindPreset(saveId);
                if (preset != null) _banishedPresets.Add(preset);
                else ctx.Report.Warn($"소멸 캐릭터 ID '{saveId}'를 찾지 못했습니다 — 재영입 가능해집니다.");
            }

            Debug.Log($"[RunManager] 런 복원 — 시드 {p.Seed}, 현재 노드 {p.CurrentNodeId}, 전투 {p.BattlesCleared}회 클리어");
        }
```

- [ ] **Step 4: `GoldManager` — 참가자 구현 추가**

using에 아래를 추가한다.

```csharp
using DiceOrbit.Core.Run.Save;
```

클래스 선언을 바꾼다.

```csharp
    public class GoldManager : MonoBehaviour, IRunSaveParticipant
```

`ResetGold()` 바로 아래에 삽입한다.

```csharp
        // ── 세이브 참가자 ──────────────────────────────────────

        public void Capture(RunSaveData data) => data.Gold = gold;

        public void Validate(RunSaveData data, RunRestoreContext ctx)
        {
            // 정수 하나라 해결할 ID가 없다 — 검증할 것이 없다.
        }

        public void Apply(RunSaveData data, RunRestoreContext ctx)
        {
            gold = Mathf.Max(0, data.Gold);
            OnGoldChanged?.Invoke(gold);
        }
```

- [ ] **Step 5: 컴파일 확인 — 실패를 먼저 본다**

`GameFlowManager:432`가 방금 지운 `RestoreRun`을 부르고 있으므로 **실패해야 정상**이다.

```bash
./Tools/compile-check.sh 2>&1 | grep -E "CS[0-9]+" | head -10
```

기대: `GameFlowManager.cs(432,...): error CS1061: ... 'RestoreRun'` 계열 에러.
이 에러는 태스크 12에서 해소된다.

**이 태스크는 컴파일 통과로 끝나지 않는 유일한 태스크다.** 이유는 구 복원 경로와 신 참가자
경로가 잠시 공존할 수 없기 때문이다. 아래 Step 6에서 임시 조치를 넣어 초록으로 되돌린다.

- [ ] **Step 6: 구 복원 경로를 임시로 비활성화**

`GameFlowManager.ContinueGameFlow()` 본문 전체를 아래로 임시 대체한다. 태스크 12에서 최종
형태로 다시 쓴다.

```csharp
        /// <summary>세이브 복원 — 태스크 12에서 RunSaveService 기반으로 다시 쓴다.</summary>
        private void ContinueGameFlow()
        {
            Debug.LogWarning("[GameFlow] 세이브 복원이 리팩토링 중입니다 — 새 게임으로 시작합니다.");
            StartGameFlow();
        }
```

`GameFlowManager` 상단의 `using System.Linq;`가 이 변경으로 미사용이 될 수 있으나, 다른 곳에서
쓰고 있으면 그대로 둔다. 컴파일 경고는 `-warn:0`으로 억제돼 있어 문제되지 않는다.

- [ ] **Step 7: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

- [ ] **Step 8: 커밋**

```bash
git add Assets/Scripts/Core/Run/RunManager.cs \
        Assets/Scripts/Core/GoldManager.cs \
        Assets/Scripts/Core/GameFlowManager.cs
git commit -m "feat: RunManager/GoldManager를 세이브 참가자로 전환

RestoreRun(4인자)과 BanishedNames를 제거하고 Capture/Validate/Apply로
대체한다. 소멸 캐릭터 ID 불일치는 실패가 아니라 경고다 — 그 캐릭터가
다시 영입 가능해질 뿐 런은 정상 진행된다.

ContinueGameFlow는 태스크 12까지 임시로 새 게임 폴백이다."
```

---

## Task 9: 참가자 구현 — `ArtifactManager` · `PotionManager`

**Files:**
- Modify: `Assets/Scripts/Core/Run/Artifact/ArtifactManager.cs`
- Modify: `Assets/Scripts/Core/Run/PotionManager.cs`

**Interfaces:**
- Consumes: `IRunSaveParticipant`·`RunRestoreContext` (태스크 4), `ArtifactSaveData`·`PotionSaveData`
  (태스크 3), `SaveIdCatalog.FindArtifact`/`FindPotion` (태스크 2), `ClearAll()` (태스크 7)
- Produces: 두 매니저가 `IRunSaveParticipant`를 구현한다.

- [ ] **Step 1: `ArtifactManager` 선언 변경**

using에 추가한다.

```csharp
using DiceOrbit.Core.Run.Save;
```

클래스 선언을 바꾼다.

```csharp
    public class ArtifactManager : MonoBehaviour, IRunSaveParticipant
```

- [ ] **Step 2: `ArtifactManager` — 참가자 구현 추가**

`ClearAll()` 바로 아래에 삽입한다.

```csharp
        // ── 세이브 참가자 ──────────────────────────────────────

        public void Capture(RunSaveData data)
        {
            data.Artifacts.Clear();
            foreach (var artifact in artifacts)
            {
                if (artifact == null || artifact.data == null) continue;
                data.Artifacts.Add(new ArtifactSaveData { Id = artifact.data.SaveId });
            }
        }

        public void Validate(RunSaveData data, RunRestoreContext ctx)
        {
            foreach (var saved in data.Artifacts)
            {
                if (string.IsNullOrEmpty(saved.Id))
                {
                    ctx.Report.Fail("유물 Id가 비어 있습니다.");
                    continue;
                }
                if (ctx.Catalog == null || ctx.Catalog.FindArtifact(saved.Id) == null)
                    ctx.Report.Fail($"유물 '{saved.Id}'를 카탈로그에서 찾지 못했습니다.");
            }
        }

        public void Apply(RunSaveData data, RunRestoreContext ctx)
        {
            ClearAll();
            foreach (var saved in data.Artifacts)
                Grant(ctx.Catalog.FindArtifact(saved.Id));
        }
```

- [ ] **Step 3: `PotionManager` 선언 변경**

using에 추가한다.

```csharp
using DiceOrbit.Core.Run.Save;
```

클래스 선언을 바꾼다.

```csharp
    public class PotionManager : MonoBehaviour, IRunSaveParticipant
```

- [ ] **Step 4: `PotionManager` — 참가자 구현 추가**

`ClearAll()` 바로 아래에 삽입한다.

```csharp
        // ── 세이브 참가자 ──────────────────────────────────────

        public void Capture(RunSaveData data)
        {
            data.Potions.Clear();
            foreach (var potion in _slots)
            {
                if (potion == null) continue;
                data.Potions.Add(new PotionSaveData { Id = potion.SaveId });
            }
        }

        public void Validate(RunSaveData data, RunRestoreContext ctx)
        {
            foreach (var saved in data.Potions)
            {
                if (string.IsNullOrEmpty(saved.Id))
                {
                    ctx.Report.Fail("포션 Id가 비어 있습니다.");
                    continue;
                }
                if (ctx.Catalog == null || ctx.Catalog.FindPotion(saved.Id) == null)
                    ctx.Report.Fail($"포션 '{saved.Id}'를 카탈로그에서 찾지 못했습니다.");
            }
        }

        public void Apply(RunSaveData data, RunRestoreContext ctx)
        {
            ClearAll();
            foreach (var saved in data.Potions)
                TryAdd(ctx.Catalog.FindPotion(saved.Id));
        }
```

- [ ] **Step 5: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Core/Run/Artifact/ArtifactManager.cs Assets/Scripts/Core/Run/PotionManager.cs
git commit -m "feat: ArtifactManager/PotionManager를 세이브 참가자로 전환

조회 출처가 획득 후보 풀이 아니라 SaveIdCatalog다. 풀 밖 경로로 얻은
유물·포션도 복원된다. Apply는 ClearAll로 시작해 이전 런의 잔여를 지운다."
```

---

## Task 10: 참가자 구현 — `PartyManager`

**Files:**
- Modify: `Assets/Scripts/Core/Stage/PartyManager.cs`

**Interfaces:**
- Consumes: `IRunSaveParticipant`·`RunRestoreContext` (태스크 4), `CharacterSaveData`·
  `ModifierSaveData` (태스크 3), `ModifierRegistry.Create`/`Exists` (태스크 6),
  `CharacterPreset.SaveId` (태스크 1), `ClearAll()` (태스크 7)
- Produces: `PartyManager`가 `IRunSaveParticipant`를 구현한다.

- [ ] **Step 1: 선언 변경**

using에 추가한다.

```csharp
using DiceOrbit.Core.Run.Save;
using DiceOrbit.Data.Modifiers;
```

클래스 선언을 바꾼다.

```csharp
    public class PartyManager : MonoBehaviour, IRunSaveParticipant
```

- [ ] **Step 2: 참가자 구현 추가**

`ClearAll()` 바로 아래에 삽입한다.

```csharp
        // ── 세이브 참가자 ──────────────────────────────────────

        public void Capture(RunSaveData data)
        {
            data.Party.Clear();
            foreach (var character in party)
            {
                if (character == null || character.Stats == null) continue;

                var save = new CharacterSaveData
                {
                    PresetId = character.Stats.SourcePreset != null
                        ? character.Stats.SourcePreset.SaveId
                        : null,
                    CurrentHp = character.Stats.CurrentHP,
                    MaxHp = character.Stats.MaxHP,
                    RevivalStock = character.Stats.RevivalStock,
                };

                var mods = character.Stats.Modifiers != null ? character.Stats.Modifiers.Modifiers : null;
                if (mods != null)
                    foreach (var mod in mods)
                        if (mod != null)
                            save.Modifiers.Add(new ModifierSaveData { Id = mod.GetType().Name });

                data.Party.Add(save);
            }
        }

        public void Validate(RunSaveData data, RunRestoreContext ctx)
        {
            if (ctx.Spawner == null)
                ctx.Report.Fail("CharacterSpawner를 찾지 못해 파티를 복원할 수 없습니다.");

            foreach (var save in data.Party)
            {
                if (string.IsNullOrEmpty(save.PresetId))
                {
                    ctx.Report.Fail("파티 구성원의 PresetId가 비어 있습니다.");
                }
                else if (ctx.FindPreset(save.PresetId) == null)
                {
                    ctx.Report.Fail($"프리셋 '{save.PresetId}'를 카탈로그에서 찾지 못했습니다.");
                }

                foreach (var mod in save.Modifiers)
                    if (!ModifierRegistry.Exists(mod.Id))
                        ctx.Report.Fail($"모디파이어 '{mod.Id}'가 레지스트리에 없습니다.");
            }
        }

        public void Apply(RunSaveData data, RunRestoreContext ctx)
        {
            ClearAll();

            for (int i = 0; i < data.Party.Count; i++)
            {
                var save = data.Party[i];

                // CharacterSpawner.Spawn이 PartyManager.AddCharacter를 자동 호출하므로 별도 등록은 없다.
                var character = ctx.Spawner.Spawn(ctx.FindPreset(save.PresetId), i, data.Party.Count);
                if (character == null || character.Stats == null) continue;

                character.Stats.MaxHP = save.MaxHp;
                character.Stats.CurrentHP = Mathf.Clamp(save.CurrentHp, 1, save.MaxHp);
                character.Stats.RevivalStock = save.RevivalStock;

                foreach (var mod in save.Modifiers)
                {
                    // 캐릭터마다 독립 인스턴스가 필요하므로 매번 새로 만든다.
                    var instance = ModifierRegistry.Create(mod.Id);
                    if (instance != null && character.Stats.Modifiers != null)
                        character.Stats.Modifiers.Add(instance);
                }
            }
        }
```

- [ ] **Step 3: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Core/Stage/PartyManager.cs
git commit -m "feat: PartyManager를 세이브 참가자로 전환

프리셋 조회가 CharacterSelectionUI가 아니라 카탈로그를 거치므로,
해당 UI가 없는 씬에서도 파티가 복원된다. 모디파이어는 표시 이름이
아니라 클래스 타입명으로 식별한다."
```

---

## Task 11: `RunSaveService` — 참가자 수집과 2단계 복원

**Files:**
- Create: `Assets/Scripts/Core/Run/Save/RunSaveService.cs` (+`.meta`)

**Interfaces:**
- Consumes: 태스크 2~10 전부
- Produces:
  - `RunSaveService.HasSave() → bool`
  - `RunSaveService.Delete()`
  - `RunSaveService.SaveCurrent()`
  - `RunSaveService.RestoreCurrent() → RestoreReport`

- [ ] **Step 1: 작성**

`Assets/Scripts/Core/Run/Save/RunSaveService.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 저장·복원 오케스트레이션. 상태는 전부 참가자가 소유하고, 여기는 순서만 안다.
    /// 맵 화면 진입마다 자동 저장 → 사망/승리/새 게임 시 삭제.
    /// </summary>
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
                // 기존 세이브를 덮어쓰지 않는다 — 반쪽 저장보다 직전 세이브 유지가 낫다.
                Debug.LogWarning($"[RunSave] 저장 중단 — {missing} 없음");
                return;
            }

            var data = new RunSaveData();
            foreach (var participant in participants) participant.Capture(data);
            RunSaveFile.Write(data);
        }

        public static RestoreReport RestoreCurrent()
        {
            var report = new RestoreReport();

            var data = RunSaveFile.Read(report);
            if (data == null) return report;

            var participants = CollectParticipants(out string missing);
            if (participants == null)
            {
                report.Fail($"{missing}이(가) 없어 복원할 수 없습니다.");
                return report;
            }

            var ctx = BuildContext(report);
            if (ctx.Catalog == null)
            {
                report.Fail(
                    "SaveIdCatalog를 로드하지 못했습니다 — Assets/Resources/SaveIdCatalog.asset을 확인하세요.");
                return report;
            }

            // 1단계 — 검증만. 첫 실패에서 멈추지 않고 전부 수집한다 (디버깅 편의).
            foreach (var participant in participants) participant.Validate(data, ctx);
            if (!report.Success) return report;   // 아무것도 적용되지 않은 상태

            // 2단계 — 적용
            foreach (var participant in participants) participant.Apply(data, ctx);
            return report;
        }

        /// <summary>
        /// 순서 = 복원 의존성 순서.
        /// FindObjectsByType 순회는 쓰지 않는다 — 순서가 비결정적이고, 인스턴스가 없으면
        /// 그 섹션이 '조용히' 누락된다. 씬 배치 매니저가 없으면 누락이 아니라 명시적 실패다.
        /// </summary>
        private static List<IRunSaveParticipant> CollectParticipants(out string missing)
        {
            missing = null;

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

        private static RunRestoreContext BuildContext(RestoreReport report)
        {
            return new RunRestoreContext
            {
                Spawner = Object.FindFirstObjectByType<CharacterSpawner>(),
                Catalog = SaveIdCatalog.Get(),
                Report  = report,
            };
        }
    }
}
```

- [ ] **Step 2: `.meta` 생성**

```bash
guid=$(python3 -c "import uuid;print(uuid.uuid4().hex)")
printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" > "Assets/Scripts/Core/Run/Save/RunSaveService.cs.meta"
```

- [ ] **Step 3: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

구 `DiceOrbit.Core.Run.RunSaveService`와 신 `DiceOrbit.Core.Run.Save.RunSaveService`가 공존하지만
네임스페이스가 다르고, 신 파일 안에서 이름 없이 쓰면 더 가까운 `Save` 쪽이 우선한다.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Core/Run/Save/RunSaveService.cs Assets/Scripts/Core/Run/Save/RunSaveService.cs.meta
git commit -m "feat: 참가자 기반 RunSaveService 추가

고정 목록으로 참가자를 모아 Capture / Validate 전원 → Apply 전원 순으로
돌린다. 검증이 하나라도 실패하면 아무것도 적용하지 않는다.
씬 배치 매니저 부재는 조용한 누락이 아니라 명시적 실패다."
```

---

## Task 12: 배선 교체 — `GameFlowManager` · `MainMenuUI` · 구 파일 삭제

**Files:**
- Modify: `Assets/Scripts/Core/GameFlowManager.cs`
- Modify: `Assets/Scripts/UI/MainMenuUI.cs:53`
- Modify: `Assets/Scripts/Core/Run/Artifact/ArtifactManager.cs` (`FindInPool` 삭제)
- Modify: `Assets/Scripts/Core/Run/PotionManager.cs` (`FindInPool` 삭제)
- Delete: `Assets/Scripts/Core/Run/RunSaveService.cs` (+`.meta`)

**Interfaces:**
- Consumes: `RunSaveService.HasSave`/`Delete`/`SaveCurrent`/`RestoreCurrent` (태스크 11)

- [ ] **Step 1: 구 `RunSaveService` 삭제 — 반드시 먼저**

```bash
git rm Assets/Scripts/Core/Run/RunSaveService.cs Assets/Scripts/Core/Run/RunSaveService.cs.meta
```

**순서가 중요하다.** `GameFlowManager`는 이미 `using DiceOrbit.Core.Run;`을 갖고 있으므로
(`GameFlowManager.cs:4`), 여기에 `using DiceOrbit.Core.Run.Save;`를 먼저 더하면 `RunSaveService`가
두 네임스페이스에 모두 존재해 `CS0104`(모호한 참조)가 난다. 구 파일을 먼저 지운다.

- [ ] **Step 2: `GameFlowManager` using 추가**

파일 상단 using 목록에 추가한다.

```csharp
using DiceOrbit.Core.Run.Save;
```

- [ ] **Step 3: `ContinueGameFlow` 최종 구현**

태스크 8에서 임시로 넣은 본문을 아래로 바꾼다.

```csharp
        /// <summary>세이브 복원 — 참가자 전원 검증 통과 시에만 적용된다.</summary>
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

            if (report.Warnings.Count > 0)
                Debug.LogWarning($"[GameFlow] 복원 경고\n{report}");

            ChangeState(GameState.Map);
        }
```

- [ ] **Step 4: `MainMenuUI` 호출 경로 갱신**

`MainMenuUI.cs:53`을 바꾼다.

```csharp
                continueButton.interactable = Core.Run.Save.RunSaveService.HasSave();
```

- [ ] **Step 5: `FindInPool` 두 개 삭제**

`ArtifactManager`에서 아래를 삭제한다.

```csharp
        /// <summary>이름으로 풀에서 찾기 (세이브 복원용).</summary>
        public ArtifactData FindInPool(string artifactName)
            => artifactPool.FirstOrDefault(d => d != null && d.artifactName == artifactName);
```

`PotionManager`에서 아래를 삭제한다.

```csharp
        /// <summary>이름으로 풀에서 찾기 (세이브 복원용).</summary>
        public Potion FindInPool(string potionName)
            => potionPool.FirstOrDefault(p => p != null && p.PotionName == potionName);
```

- [ ] **Step 6: 컴파일 확인**

```bash
./Tools/compile-check.sh 2>&1 | tail -20
```

기대: `COMPILE OK`.

`CS0104`(모호한 참조)가 나면 Step 4의 삭제가 빠진 것이다. `CS1061`이 `FindInPool`을 가리키면
아직 호출자가 남았다는 뜻이므로 진단이 지목한 줄을 확인한다.

- [ ] **Step 7: 잔여 참조 점검**

```bash
grep -rn "FindInPool\|RestoreRun\|BanishedNames\|EffectiveArtifactNames\|RelicNames\|startingArtifacts\|startingPotions" Assets/Scripts --include=*.cs
```

기대: 출력 없음.

- [ ] **Step 8: 커밋**

```bash
git add -A Assets/Scripts
git commit -m "refactor: 세이브 배선을 참가자 기반으로 교체하고 구 경로 삭제

ContinueGameFlow 70줄이 10줄로 줄었다. 이름 매칭 조회(FindInPool)와
구 RunSaveService, RelicNames/EffectiveArtifactNames 폴백이 모두 사라졌다."
```

---

## Task 13: 문서 갱신

**Files:**
- Modify: `Docs/project_structure.md`
- Modify: `Docs/README.md`

- [ ] **Step 1: 현재 서술 확인**

```bash
grep -n -i "RunSaveService\|세이브\|이어하기" Docs/project_structure.md Docs/README.md
```

- [ ] **Step 2: `project_structure.md` 갱신**

Step 1이 찾아낸 세이브 관련 서술을 아래 사실에 맞춘다. 문서와 코드가 어긋나면 코드가 정답이므로,
읽어서 확인한 내용만 적는다.

- 세이브 코드 위치: `Assets/Scripts/Core/Run/Save/` (7개 파일)
- 저장·복원 주체: 각 매니저 (`IRunSaveParticipant`), 오케스트레이션만 `RunSaveService`
- 에셋 조회 출처: `SaveIdCatalog` (`Assets/Resources/SaveIdCatalog.asset`)
- 식별자: 에셋의 불변 `saveId` (표시 이름 아님)
- 포맷: v2, 마이그레이션 없음

- [ ] **Step 3: `README.md` 인덱스에 계획 문서 등재**

`Docs/README.md`의 「설계안 · 구현 계획 (기록)」 섹션에 한 줄 추가한다.

```markdown
| [세이브 시스템 리팩토링 구현 계획](superpowers/plans/2026-07-28-save-system-refactor.md) | 2026-07-28 | 참가자 방식 전환 + saveId 도입 실행 계획 |
```

실제 표 형식은 파일의 기존 행을 그대로 따른다.

- [ ] **Step 4: 커밋**

```bash
git add Docs/project_structure.md Docs/README.md
git commit -m "docs: 세이브 시스템 구조 변경 반영"
```

---

## Windows Unity 핸드오프

이 환경에서 할 수 없는 작업이다. 브랜치를 push한 뒤 사용자가 Windows Unity에서 수행한다.

### 선행 작업 (이걸 안 하면 게임이 굴러가지 않는다)

1. **카탈로그 에셋 생성** — `Create > DiceOrbit > SaveIdCatalog` →
   `Assets/Resources/SaveIdCatalog.asset`으로 저장. (`Resources` 폴더 이름·위치가 정확해야 한다)
2. **유물 에셋 저작** — 태스크 7에서 런타임 기본 풀 5종이 사라졌다. 효과 클래스는 남아 있으므로
   `Create > DiceOrbit > ArtifactData`로 에셋을 만들고 `effect`에 아래를 지정한다.

   | 효과 클래스 | 기존 이름 | 기존 가격 |
   |---|---|---|
   | `RegularStamp` | 단골 도장 | 100 |
   | `CozyBedroll` | 포근한 침낭 | 110 |
   | `GoldenDice` | 황금 주사위 | 130 |
   | `PhoenixFeather` | 불사조 깃털 | 150 |
   | `LifeAmulet` | 생명의 부적 | 120 |

3. **풀 등록** — `BattleScene`의 `ArtifactManager.artifactPool`에 위 에셋 + 기존
   `PowerfullPunch.asset`을 넣는다. `PotionManager.potionPool`에는 `NewHealPotion.asset` 등
   원하는 포션을 넣는다. (두 풀은 현재 비어 있다)
4. **`도구 > Dice Orbit > 세이브 ID 전체 점검`** 실행 → 콘솔에 에러가 없어야 한다.

### 검증 절차

1. 대상 에셋을 하나씩 선택 → 인스펙터의 `saveId`가 에셋 파일명으로 채워졌는지
2. 에셋을 `Ctrl+D`로 복제 → 중복 `saveId` 에러가 콘솔에 뜨는지 (뜬 뒤 복제본 삭제)
3. 새 포션 에셋 생성 → 수동 스캔 없이 카탈로그에 자동 추가되는지
4. 새 게임 → 유물·포션 획득 → 종료 → 이어하기 → 골드·유물·포션·파티 HP·모디파이어 일치
5. **풀에 없는 포션**을 코드로 직접 지급 → 저장 → 이어하기 → 복원되는지
   (설계 §1-8이 닫혔는지 확인하는 항목)
6. `CharacterSelectionUI`가 없는 씬에서 이어하기 → 파티가 복원되는지 (§1-9 확인)
7. `%APPDATA%/../LocalLow/<회사>/<제품>/run_save.json` 열어 v2 포맷 확인
8. 세이브 파일을 중간에서 잘라 손상 → `.bak` 복구 또는 이어하기 버튼 비활성 확인
9. 프리셋 `saveId`를 일부러 바꾼 뒤 → 반쪽 복원이 아니라 "복원 불가"로 처리되는지
10. 한 세션에서 런 종료 후 다시 이어하기 → 이전 런의 유물·파티가 섞이지 않는지

### 알려진 한계

- **구 세이브는 열리지 않는다.** v1 파일은 버전 검사에서 폐기된다. 설계 결정 사항이다.
- 태스크 0에서 미사용 `using` 5줄을 제거했다 — `Unity.VisualScripting` 3건(`SkillData`,
  `OrbitManager`, `RandMineTile`), `UnityEngine.Rendering.DebugUI` 2건(`RandMineTile`,
  `LunaPriest`). Windows Unity에서 이 파일들에 컴파일 에러가 나면 실제로 쓰고 있었다는 뜻이므로
  되돌린다. (Roslyn 검증에서는 제거 후 통과를 확인했다)
- **컴파일 검증은 동작 검증이 아니다.** 위 10개 항목이 유일한 동작 게이트다.
