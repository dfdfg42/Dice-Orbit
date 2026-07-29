#!/usr/bin/env bash
# Roslyn 컴파일 검증 — Unity 없이 타입/컴파일 오류를 확인한다.
#
# Unity가 실제로 컴파일하는 "맥락"은 둘이고, 둘 다 통과해야 한다.
# 어셈블리 분할(Editor/ 안/밖)과 맥락은 별개의 축이다 — 예전 스크립트는 이 둘을 섞어
# 실제로는 존재하지 않는 조합(UNITY_EDITOR 없는 Assembly-CSharp에 에디터 어셈블리를 링크)을
# 만들었고, 그래서 #if UNITY_EDITOR로 감싼 런타임 멤버를 에디터 스크립트가 부르는
# 정상 코드에서 거짓 실패가 났다.
#
#   패스 1 — 에디터 맥락 (에디터를 열었을 때)
#     Assembly-CSharp         UNITY_EDITOR 켬.  UnityEngine.* + UnityEditor.*
#     Assembly-CSharp-Editor  UNITY_EDITOR 켬.  위 + UnityEditor.* + 패스1의 Assembly-CSharp
#     → 에디터 스크립트가 가드된 런타임 멤버를 부르는 정상 패턴이 통과해야 한다.
#
#   패스 2 — 플레이어 빌드 (빌드했을 때)
#     Assembly-CSharp 만      UNITY_EDITOR 끔.  UnityEngine.* 만 (UnityEditor.*를 주지 않는다)
#     Assembly-CSharp-Editor는 아예 빌드되지 않으므로 컴파일하지 않는다.
#     → 런타임 코드가 가드 없이 에디터 API를 쓰면 CS0234로 잡힌다.
#
# 어셈블리 경계를 넘는 internal 접근은 패스 1에서 잡힌다(별도 어셈블리로 컴파일하므로).
#
# 다만 동작 검증은 아니다. 통과했다고 "동작한다"고 보고하지 말 것 —
# 씬·프리팹 직렬화 참조, OnValidate/AssetPostprocessor 수명주기, 런타임 로직은
# Windows Unity에서만 확인된다.
#
# 준비물은 Tools/setup-compile-refs.sh가 만든다.
#
# set -e를 켠다. 예전엔 없어서 find가 실패해도(= 소스 0건) 그냥 진행했고,
# csc는 소스 0건을 에러가 아닌 warning CS2008로 처리해 exit 0을 냈다.
# 결과는 아무것도 컴파일하지 않은 채로 나오는 "COMPILE OK" — 최악의 거짓 신호였다.
set -euo pipefail

ROOT="${ROOT_OVERRIDE:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
SRC="$ROOT/Assets/Scripts"
REFS="$HOME/unity-refs"
DOTNET="$HOME/.dotnet/dotnet"
OUT="${TMPDIR:-/tmp}/dice-orbit-compile"

# 맥락별로 산출물 디렉터리를 나눈다. 두 패스 모두 'Assembly-CSharp'라는 같은 이름으로
# 빌드되어야 하는데(어셈블리 이름은 -out 파일명에서 나오고, InternalsVisibleTo가 이름으로
# 걸린다), 같은 경로에 쓰면 서로 덮어써 어느 맥락의 산출물인지 알 수 없게 된다.
EDITOR_OUT="$OUT/editor-context"
PLAYER_OUT="$OUT/player-build"
# 이전 실행의 DLL이 남아 있으면 이번 컴파일이 실패해도 그게 참조되어 거짓 통과가 날 수 있다.
rm -rf "$EDITOR_OUT" "$PLAYER_OUT"
mkdir -p "$OUT" "$EDITOR_OUT" "$PLAYER_OUT"

if [ ! -d "$REFS/Editor/Data/Managed/UnityEngine" ] || [ ! -d "$REFS/prebuilt" ]; then
  echo "ERROR: 참조 어셈블리가 없습니다. Tools/setup-compile-refs.sh를 먼저 실행하세요." >&2
  exit 2
fi
if [ ! -d "$SRC" ]; then
  echo "ERROR: 스크립트 디렉터리가 없습니다: $SRC" >&2
  exit 2
fi

# sort -V(버전 정렬)여야 한다. 그냥 sort는 사전식이라 9.0.100 < 9.0.9로 판정한다.
CSC="$(ls -d "$HOME"/.dotnet/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | sort -V | tail -1 || true)"
if [ -z "$CSC" ]; then echo "ERROR: Roslyn csc.dll을 찾지 못했습니다." >&2; exit 2; fi

# ── 참조 ──
# UnityEngine.dll / UnityEditor.dll(집합체)은 제외한다 — 모듈 DLL과 같은 타입을 재노출해
# CS0433(타입 중복)을 5,000건 넘게 일으킨다. 실제 타입은 전부 *Module.dll에 있다.
PLAYER_REFS=()
while IFS= read -r d; do PLAYER_REFS+=("-r:$d"); done < <(
  find "$REFS/Editor/Data/Managed/UnityEngine" -name 'UnityEngine*.dll' ! -name 'UnityEngine.dll'
  find "$REFS/Editor/Data/NetStandard" -name 'netstandard.dll'
  find "$REFS/prebuilt" -name '*.dll'
)
if [ "${#PLAYER_REFS[@]}" -eq 0 ]; then
  echo "ERROR: 참조 DLL을 한 개도 찾지 못했습니다 ($REFS). setup-compile-refs.sh를 다시 실행하세요." >&2
  exit 2
fi

EDITOR_REFS=("${PLAYER_REFS[@]}")
while IFS= read -r d; do EDITOR_REFS+=("-r:$d"); done < <(
  find "$REFS/Editor/Data/Managed/UnityEngine" -name 'UnityEditor*.dll' ! -name 'UnityEditor.dll'
)

# -unsafe를 주지 않는다. ProjectSettings.asset의 allowUnsafeCode가 0이므로 실제 Unity는
# unsafe 코드를 CS0227로 거부한다. 여기서 켜두면 Unity가 막을 코드를 통과시킨다.
# (setup-compile-refs.sh의 패키지 선빌드는 반대다 — inputsystem이 unsafe를 쓰므로 거기선 필요하다.)
COMMON=(-nologo -noconfig -nostdlib -target:library -langversion:9 -warn:0
        -define:UNITY_2020_1_OR_NEWER -define:UNITY_6000_0_OR_NEWER)

# ── 소스 수집 ──
# 0건이면 하드 실패다. csc는 0건을 경고로 넘기므로 여기서 막지 않으면 검증이 무력화된다.
find "$SRC" -name '*.cs' -not -path '*/Editor/*' -printf '"%p"\n' > "$OUT/runtime.rsp"
find "$SRC" -name '*.cs' -path '*/Editor/*' -printf '"%p"\n' > "$OUT/editor.rsp"
RUNTIME_N="$(wc -l < "$OUT/runtime.rsp")"
EDITOR_N="$(wc -l < "$OUT/editor.rsp")"

if [ "$RUNTIME_N" -eq 0 ]; then
  echo "ERROR: 런타임 소스(.cs)를 한 개도 찾지 못했습니다: $SRC" >&2
  echo "       경로가 맞는지 확인하세요. 소스 0건은 통과가 아니라 실패입니다." >&2
  exit 2
fi
# 에디터 소스는 0건도 정상이다 — Editor/ 폴더가 없는 상태가 있을 수 있다.

# ══ 패스 1: 에디터 맥락 (UNITY_EDITOR 켬) ══
echo "══ 패스 1: 에디터 맥락 (UNITY_EDITOR 켬, UnityEditor.* 참조 가능)"

echo "── Assembly-CSharp ($RUNTIME_N)"
"$DOTNET" "$CSC" "${COMMON[@]}" -define:UNITY_EDITOR \
  -out:"$EDITOR_OUT/Assembly-CSharp.dll" "${EDITOR_REFS[@]}" "@$OUT/runtime.rsp" \
  || { echo "COMPILE FAILED (에디터 맥락 / Assembly-CSharp)"; exit 1; }

if [ "$EDITOR_N" -gt 0 ]; then
  echo "── Assembly-CSharp-Editor ($EDITOR_N)"
  "$DOTNET" "$CSC" "${COMMON[@]}" -define:UNITY_EDITOR \
    -out:"$EDITOR_OUT/Assembly-CSharp-Editor.dll" "${EDITOR_REFS[@]}" \
    -r:"$EDITOR_OUT/Assembly-CSharp.dll" "@$OUT/editor.rsp" \
    || { echo "COMPILE FAILED (에디터 맥락 / Assembly-CSharp-Editor)"; exit 1; }
else
  echo "── Assembly-CSharp-Editor (0) — 건너뜀"
fi

# ══ 패스 2: 플레이어 빌드 (UNITY_EDITOR 끔) ══
# UnityEditor.*를 참조로 주지 않는다. 실제 플레이어 빌드도 주지 않는다.
echo "══ 패스 2: 플레이어 빌드 (UNITY_EDITOR 끔, UnityEditor.* 참조 불가)"

echo "── Assembly-CSharp ($RUNTIME_N)"
"$DOTNET" "$CSC" "${COMMON[@]}" \
  -out:"$PLAYER_OUT/Assembly-CSharp.dll" "${PLAYER_REFS[@]}" "@$OUT/runtime.rsp" \
  || { echo "COMPILE FAILED (플레이어 빌드 / Assembly-CSharp)"; exit 1; }

echo "COMPILE OK (에디터 맥락 + 플레이어 빌드)"
