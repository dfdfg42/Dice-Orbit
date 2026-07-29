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
