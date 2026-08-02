#!/usr/bin/env bash
# 컴파일 검증용 참조 어셈블리 구축 (1회성, 약 4.5GB 다운로드).
#
# Unity 매니지드 DLL은 순수 IL이라 ARM64 리눅스에서도 참조할 수 있다.
# 에디터 자체는 x86_64 전용이라 실행할 수 없지만, 참조만 하는 데는 문제없다.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REFS="$HOME/unity-refs"

# ── 0. 버전 고정값 대조 ──
# 예전엔 UNITY_HASH가 주석만 달린 채 하드코딩돼 있었다. 프로젝트가 에디터 버전을 올리면
# 스크립트는 조용히 옛 버전의 참조 어셈블리를 받고, 컴파일 검증은 실제와 다른 API로
# 통과/실패하게 된다. 그래서 프로젝트 파일에서 읽어 고정값과 대조하고, 어긋나면 멈춘다.
#
# 고정값을 그냥 없애고 파일 값을 쓰지 않는 이유: ~/unity-refs와 prebuilt DLL은 특정 버전에
# 묶여 있다. 버전이 바뀌면 그것들을 지우고 다시 받아야 하므로, 사람이 알아채야 한다.
EXPECTED_UNITY_VERSION="6000.3.8f1"
EXPECTED_UNITY_HASH="1c7db571dde0"
EXPECTED_INPUTSYSTEM_VER="1.17.0"

VERSION_FILE="$ROOT/ProjectSettings/ProjectVersion.txt"
MANIFEST="$ROOT/Packages/manifest.json"
[ -f "$VERSION_FILE" ] || { echo "ERROR: $VERSION_FILE 이 없습니다." >&2; exit 2; }
[ -f "$MANIFEST" ]     || { echo "ERROR: $MANIFEST 이 없습니다." >&2; exit 2; }

# m_EditorVersionWithRevision: 6000.3.8f1 (1c7db571dde0)
REVISION_LINE="$(grep '^m_EditorVersionWithRevision:' "$VERSION_FILE" || true)"
UNITY_VERSION="$(sed -n 's/^m_EditorVersionWithRevision: *\([^ ]*\) *(.*)$/\1/p' <<<"$REVISION_LINE")"
UNITY_HASH="$(sed -n 's/^.*(\(.*\))$/\1/p' <<<"$REVISION_LINE")"
if [ -z "$UNITY_VERSION" ] || [ -z "$UNITY_HASH" ]; then
  echo "ERROR: ProjectVersion.txt에서 에디터 버전/해시를 읽지 못했습니다: '$REVISION_LINE'" >&2
  exit 2
fi
if [ "$UNITY_VERSION" != "$EXPECTED_UNITY_VERSION" ] || [ "$UNITY_HASH" != "$EXPECTED_UNITY_HASH" ]; then
  echo "ERROR: Unity 에디터 버전이 이 스크립트의 고정값과 다릅니다." >&2
  echo "  프로젝트: $UNITY_VERSION ($UNITY_HASH)" >&2
  echo "  스크립트: $EXPECTED_UNITY_VERSION ($EXPECTED_UNITY_HASH)" >&2
  echo "  확인 후 이 스크립트의 EXPECTED_* 값을 고치고, rm -rf '$REFS' 로 다시 받으세요." >&2
  exit 2
fi

# "com.unity.inputsystem": "1.17.0"
INPUTSYSTEM_VER="$(sed -n 's/.*"com\.unity\.inputsystem" *: *"\([^"]*\)".*/\1/p' "$MANIFEST")"
if [ "$INPUTSYSTEM_VER" != "$EXPECTED_INPUTSYSTEM_VER" ]; then
  echo "ERROR: com.unity.inputsystem 버전이 고정값과 다릅니다." >&2
  echo "  manifest.json: '${INPUTSYSTEM_VER:-(읽지 못함)}' / 스크립트: '$EXPECTED_INPUTSYSTEM_VER'" >&2
  echo "  확인 후 EXPECTED_INPUTSYSTEM_VER을 고치고, rm -rf '$REFS/packages' '$REFS/prebuilt' 하세요." >&2
  exit 2
fi

echo "── 대상: Unity $UNITY_VERSION ($UNITY_HASH), inputsystem $INPUTSYSTEM_VER"

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
# sort -V(버전 정렬)여야 한다. 그냥 sort는 사전식이라 9.0.100 < 9.0.9로 판정한다.
CSC="$(ls -d "$HOME"/.dotnet/sdk/*/Roslyn/bincore/csc.dll | sort -V | tail -1)"

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
  # -unsafe는 여기선 필수다 — com.unity.inputsystem이 91개 파일에서 unsafe를 쓴다.
  # 프로젝트 코드 컴파일(compile-check.sh)에서는 반대로 빼야 한다:
  # ProjectSettings의 allowUnsafeCode가 0이라 실제 Unity가 CS0227로 거부하기 때문이다.
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
