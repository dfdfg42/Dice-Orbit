# 몬스터 영역 표시 리스킨 구현 계획 (구역 플레이트 + 의도 말풍선)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전투 화면의 구역 표시(LineRenderer 브래킷)를 힉스필드로 생성한 잉크 플레이트 스프라이트로, 몬스터 의도 말풍선을 크림 9-slice + 큰 아이콘으로 바꾸고, 공격 예정 타일 밴드를 진하게 한다.

**Architecture:** 스프라이트 2장(`zone_plate.png`, `intent_bubble.png`)을 `UiSkin`에 새 필드로 등록하고(단일 권위), 새 `ZonePlateRenderer`가 구역마다 바닥에 눕힌 `SpriteRenderer`를 주인 색으로 틴트한다. `MonsterUI`는 라벨·색 설정을 걷어내고 말풍선 루트 + 아이콘 슬롯만 남기며, 프리팹은 RunCommand로 재구성한다. 스펙: `Docs/superpowers/specs/2026-09-25-monster-zone-intent-reskin-design.md`.

**Tech Stack:** Unity 6000.3.8f1 (Assembly-CSharp, asmdef 없음), Unity MCP (`Unity_RunCommand` — `System.Reflection` 금지, `AssetDatabase.DeleteAsset` 금지 → 삭제는 `git rm` + `AssetDatabase.Refresh()`), 힉스필드 MCP (`media_upload`/`media_confirm`/`generate_image`/`balance`), Python 3.11 + PIL, curl. 자가 테스트는 에디터 정적 메서드(`RunAll()`)를 RunCommand로 호출.

## Global Constraints

- 폴백 금지: 스프라이트가 비면 `InvalidOperationException`/`LogError`, 기본 스프라이트로 조용히 굴러가지 않는다 (`UiSkin.Require` 패턴).
- 구조 > 레거시: 대체되는 코드(`ZoneFloorRenderer`, 말풍선 라벨·색 설정)는 남기지 않고 삭제한다.
- 신규 스프라이트 임포트: Sprite(Single), Mipmap ON, Trilinear, `alphaIsTransparency`. 9-slice는 `SpriteMeshType.FullRect` + `spriteBorder`.
- 플레이트 기하 (스펙 §2.1): 안쪽 반지름 2.5, 바깥 16.0, 각도 2.5°~87.5°, 모서리 반경 0.6, 캔버스 2048px, **1유닛 = 128px**, 원점 = 캔버스 좌하단, pivot (0,0), PPU 128.
- 플레이트 배치 (스펙 §2.2): 위치 (0, −0.02, 0), 회전 `Quaternion.Euler(90, −startDeg, 0)`, 유닛 스프라이트 레이어 order −200.
- 틴트 (스펙 §2.1): 주인 = HSV(h, 0.32, 1.0) 알파 0.85 / 중립 = (0.80, 0.80, 0.86, 0.5).
- 말풍선 (스펙 §3.2): `IntentBubble` 92×84 at (2.95, 78), `Icon` 56×56 at (0, 6), 라벨 없음.
- 공격 타일 밴드 알파 0.55 → 0.8.
- 힉스필드: gpt_image_2_5, 초안 low·1k → 승인 후 high·2k·transparent. 배치 2건 이하. 예산 약 6.5크레딧 (잔액 46.25).
- 씬/프리팹 편집 전 `EditorApplication.isPlaying == false` 확인. 씬이 dirty면 `OpenScene`이 다이얼로그를 띄워 실패하므로 `GetActiveScene().isDirty` 먼저 확인.
- 커밋 제외: `Assets/Fonts/Pretendard-Regular SDF.asset`, `GabrielAguiar*/*.mat`, `ProjectSettings/ProjectSettings.asset`, `.claude/`.
- 작업 폴더(gitignore): `D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/` — 하위 `tools/`, `refs/`, `candidates/`, `final/`, `review/`.

---

## 파일 구조

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/UI/Skin/UiSkin.cs` (수정) | `SkinPart.IntentBubble`, `ZonePlate`/`IntentBubble` 스프라이트 필드, `GetZonePlate()`, `ApplyIntentBubble()` |
| `Assets/Scripts/Editor/UiSkinValidator.cs` (수정) | 두 필드 점검 (플레이트 present, 말풍선 sliced) |
| `Assets/Scripts/Editor/UiSkinSelfTests.cs` (수정) | 빈 스킨 이슈 18 → 20, 완전 스킨에 두 필드 |
| `Assets/Scripts/Visuals/ZonePlateRenderer.cs` (신규) | 구역 플레이트 렌더러 (ZoneFloorRenderer 대체). 순수 헬퍼 `PlateRotation`, `OwnerTint` |
| `Assets/Scripts/Editor/ZonePlateSelfTests.cs` (신규) | 헬퍼 자가 테스트 |
| `Assets/Scripts/Visuals/ZoneFloorRenderer.cs` (삭제) | 브래킷 렌더러 |
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/EncounterSpawner.cs:89` (수정) | `ZonePlateRenderer.EnsureInstance()` |
| `Assets/Scripts/UI/MonsterUI.cs` (수정) | 말풍선 루트+아이콘만, 라벨·색 설정 제거, 아이콘 없으면 LogError |
| `Assets/Prefabs/TestMonster.prefab` (RunCommand 수정) | `MonsterCanvas/IntentBubble/Icon` 구조 |
| `Assets/Scripts/Visuals/MonsterTileColorOverlayManager.cs:19` (수정) | `overlayAlpha` 0.8 |
| `Assets/Sprites/UI Skin/zone_plate.png`, `intent_bubble.png` (신규) | 생성 스프라이트 |
| `Assets/Resources/UI/UiSkin.asset` (수정) | 두 스프라이트 배선 |
| `_workspace/.../tools/zone_silhouette.py`, `finalize_zone_plate.py`, `finalize_bubble.py` (신규, gitignore) | 실루엣 참조 / 마스킹·정규화 / 트림·경계 제안 |
| `Docs/battle_visual_language.md`, `Docs/README.md`, 스펙 상태, `Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md`, 메모리 | 문서 |

---

### Task 1: UiSkin에 플레이트·말풍선 슬롯 추가 (점검·자가 테스트 포함)

**Files:**
- Modify: `Assets/Scripts/UI/Skin/UiSkin.cs`
- Modify: `Assets/Scripts/Editor/UiSkinValidator.cs`
- Modify: `Assets/Scripts/Editor/UiSkinSelfTests.cs`

**Interfaces:**
- Produces: `SkinPart.IntentBubble`; `UiSkin.ZonePlate` (Sprite, 월드 데칼, 9-slice 아님); `UiSkin.IntentBubble` (Sprite, 9-slice); `Sprite UiSkin.GetZonePlate()` (비면 InvalidOperationException); `void UiSkin.ApplyIntentBubble(Image)` (Sliced·white).

- [ ] **Step 1: 자가 테스트 기대값을 먼저 바꾼다 (실패 확인용)**

`Assets/Scripts/Editor/UiSkinSelfTests.cs`에서:

`MakeCompleteSkin()`의 `s.Circle = MakeSprite(false);` 아래에 추가:
```csharp
            s.ZonePlate = MakeSprite(false); s.IntentBubble = MakeSprite(true);
```

`TestValidatorReportsEmptySkin()`의 Check를 교체:
```csharp
            Check(issues.Count == 20, $"빈 스킨 이슈 20건 (7 sliced + 8 button + 3 icon + circle + zone plate), 실제 {issues.Count}");
```

`RunAll()`의 `TestUiSkinImageAppliesPart();` 아래에 추가:
```csharp
            TestZonePlateRequire();
            TestApplyIntentBubble();
```

클래스 끝(`TestUiSkinImageAppliesPart` 메서드 뒤)에 추가:
```csharp
        private static void TestZonePlateRequire()
        {
            var empty = ScriptableObject.CreateInstance<UiSkin>();
            bool threw = false;
            try { empty.GetZonePlate(); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, "빈 ZonePlate로 GetZonePlate는 InvalidOperationException (폴백 없음)");
            UnityEngine.Object.DestroyImmediate(empty);

            var skin = MakeCompleteSkin();
            Check(skin.GetZonePlate() == skin.ZonePlate, "GetZonePlate: 채워진 스프라이트 반환");
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyIntentBubble()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-bubble", typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = Color.red;
            skin.ApplyIntentBubble(img);
            Check(img.sprite == skin.IntentBubble && img.type == Image.Type.Sliced && img.color == Color.white, "ApplyIntentBubble: IntentBubble·Sliced·white");
            Check(skin.GetSprite(SkinPart.IntentBubble) == skin.IntentBubble, "GetSprite(IntentBubble)");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }
```

- [ ] **Step 2: 컴파일이 깨지는지 확인 (필드 없음)**

`Unity_RunCommand`: `AssetDatabase.Refresh(); r.Log("refreshed");` → 이어서 `Unity_GetConsoleLogs(types=["error"])`.
Expected: `UiSkinSelfTests.cs`에서 `ZonePlate`/`IntentBubble`/`GetZonePlate`/`ApplyIntentBubble` 정의 없음 에러 (컴파일 실패 = 실패하는 테스트).

- [ ] **Step 3: UiSkin에 파트·필드·헬퍼 추가**

`Assets/Scripts/UI/Skin/UiSkin.cs`:

enum 교체:
```csharp
    /// <summary>스킨 부품 이름 — Apply 헬퍼와 씬 배치용 UiSkinImage가 공유한다. Scrim은 스프라이트 없이 색만. IntentBubble = 몬스터 의도 말풍선(9-slice).</summary>
    public enum SkinPart { Panel, Card, Chip, Tooltip, Slot, Divider, ButtonPrimary, ButtonSecondary, Scrim, IntentBubble }
```

`public Sprite Circle;` 줄 아래에 추가:
```csharp

        [Header("몬스터 (2026-09-25 몬스터 영역 표시 리스킨)")]
        public Sprite IntentBubble;   // 머리 위 의도 말풍선 — 9-slice, 아래 중앙 꼬리

        [Header("월드 데칼")]
        public Sprite ZonePlate;      // 구역 플레이트 — 사분면 조각, pivot (0,0), PPU 128 = 16×16 유닛. 틴트용 흰 바탕
```

`GetSprite` switch에 `SkinPart.ButtonSecondary => ButtonSecondary.Normal,` 아래 추가:
```csharp
            SkinPart.IntentBubble => IntentBubble,
```

`ApplyDivider` 줄 아래에 추가:
```csharp
        public void ApplyIntentBubble(Image image) => ApplySliced(image, IntentBubble, nameof(IntentBubble));

        /// <summary>구역 플레이트 스프라이트 (월드 SpriteRenderer용). 비면 예외 — 브래킷·기본 사각형으로 폴백하지 않는다.</summary>
        public Sprite GetZonePlate()
        {
            Require(ZonePlate, nameof(ZonePlate));
            return ZonePlate;
        }
```

- [ ] **Step 4: 점검기에 두 필드 추가**

`Assets/Scripts/Editor/UiSkinValidator.cs`의 `CheckPresent(issues, skin.Circle, nameof(skin.Circle));` 아래:
```csharp
            CheckSliced(issues, skin.IntentBubble, nameof(skin.IntentBubble));
            CheckPresent(issues, skin.ZonePlate, nameof(skin.ZonePlate));
```

- [ ] **Step 5: 컴파일 + 자가 테스트**

`Unity_RunCommand`:
```csharp
using UnityEditor;
internal class CommandScript : IRunCommand { public void Execute(ExecutionResult r) { AssetDatabase.Refresh(); r.Log("refreshed"); } }
```
`Unity_GetConsoleLogs(types=["error"])` → 0건. 그 다음:
```csharp
internal class CommandScript : IRunCommand { public void Execute(ExecutionResult r) { bool ok = DiceOrbit.EditorTools.UiSkinSelfTests.RunAll(); r.Log("selftest=" + ok); } }
```
Expected: `selftest=True`, 콘솔 `[SelfTest] 전체 PASS — UiSkin`. (실제 `UiSkin.asset` 점검은 이 시점에 이슈 2건 — 두 스프라이트가 비어 있어 정상. Task 2·3에서 채운다.)

- [ ] **Step 6: 커밋**

```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/UI/Skin/UiSkin.cs Assets/Scripts/Editor/UiSkinValidator.cs Assets/Scripts/Editor/UiSkinSelfTests.cs && git commit -q -m "feat(ui-skin): 구역 플레이트·의도 말풍선 스프라이트 슬롯 + 점검·자가 테스트

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: 구역 플레이트 스프라이트 생성 (힉스필드) + 임포트 + 스킨 배선

**Files:**
- Create: `_workspace/2026-09-25-ui-reskin/tools/zone_silhouette.py`, `finalize_zone_plate.py`
- Create: `Assets/Sprites/UI Skin/zone_plate.png` (+ .meta 임포트 설정)
- Modify: `Assets/Resources/UI/UiSkin.asset` (ZonePlate 배선)

**Interfaces:**
- Produces: `UiSkin.Current.ZonePlate` = 2048×2048 스프라이트, pivot (0,0), PPU 128 (월드 16×16), 실루엣 밖 알파 0.

- [ ] **Step 1: 실루엣 도구 작성**

`_workspace/2026-09-25-ui-reskin/tools/zone_silhouette.py`:
```python
"""구역 플레이트 실루엣 (스펙 §2.1). 2048px 캔버스, 1유닛=128px, 원점=좌하단.
사분면 조각: r 2.5~15.9유닛, 각도 2.5°~87.5°, 모서리 반경 0.6유닛(민코프스키 합 = 축소 조각 + 둥근 굵은 테두리).
출력: <out>_mask.png (흰 조각/투명 — 마스크·참조), <out>_ref.png (흰 조각 + 잉크 외곽선 — 모델 참조용)."""
import math, sys
from PIL import Image, ImageDraw

SIZE, PPU = 2048, 128
R_IN, R_OUT, A0, A1, CORNER = 2.5, 15.9, 2.5, 87.5, 0.6
INK = (75, 66, 92, 255)


def polar(r_units, deg):
    r = r_units * PPU
    a = math.radians(deg)
    return (r * math.cos(a), SIZE - 1 - r * math.sin(a))   # 원점 = 좌하단, +y 위


def sector_points(r_in, r_out, a0, a1, steps=120):
    pts = [polar(r_out, a0 + (a1 - a0) * i / steps) for i in range(steps + 1)]
    pts += [polar(r_in, a1 - (a1 - a0) * i / steps) for i in range(steps + 1)]
    return pts


def draw_rounded_sector(draw, fill, r_in, r_out, a0, a1, corner):
    """모서리 반경 corner로 둥근 조각 = (corner만큼 줄인 조각) ∪ (그 경계를 폭 2·corner 둥근 선으로 그린 것)."""
    inset_deg_in = math.degrees(corner / r_in)
    inset_deg_out = math.degrees(corner / r_out)
    inner = sector_points(r_in + corner, r_out - corner, a0 + inset_deg_in, a1 - inset_deg_in)
    draw.polygon(inner, fill=fill)
    w = int(2 * corner * PPU)
    draw.line(inner + [inner[0]], fill=fill, width=w, joint="curve")
    for p in inner:   # joint="curve"가 놓치는 끝점 보강
        draw.ellipse((p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2), fill=fill)


def main(out_prefix):
    mask = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw_rounded_sector(ImageDraw.Draw(mask), (255, 255, 255, 255), R_IN, R_OUT, A0, A1, CORNER)
    mask.save(out_prefix + "_mask.png")

    ref = mask.copy()
    d = ImageDraw.Draw(ref)
    ring = 0.35   # 잉크 외곽선 두께(유닛) — 조각 안쪽에
    inner = Image.new("L", (SIZE, SIZE), 0)
    draw_rounded_sector(ImageDraw.Draw(inner), 255, R_IN + ring, R_OUT - ring, A0 + math.degrees(ring / R_IN), A1 - math.degrees(ring / R_OUT), max(0.05, CORNER - ring))
    ink = Image.new("RGBA", (SIZE, SIZE), INK)
    outline = Image.composite(Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0)), ink, inner)   # 안쪽은 투명, 나머지 잉크
    outline.putalpha(Image.composite(mask.getchannel("A"), Image.new("L", (SIZE, SIZE), 0), outline.getchannel("A")))
    ref.alpha_composite(outline)
    ref.save(out_prefix + "_ref.png")
    print("saved", out_prefix + "_mask.png", out_prefix + "_ref.png", "alpha px:", sum(1 for a in mask.getchannel("A").getdata() if a > 0))


if __name__ == "__main__":
    main(sys.argv[1])
```

실행:
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && mkdir -p refs && python tools/zone_silhouette.py refs/zone_plate && python -c "from PIL import Image; im=Image.open('refs/zone_plate_ref.png'); print(im.size, im.getbbox())"
```
Expected: `(2048, 2048)`, bbox 왼쪽 x≈0~40, 아래 y≈2047, 오른쪽 x≈2035, 위 y≈0~15 (조각이 좌하단 원점에서 우상단으로 펼쳐짐). `Read`로 `refs/zone_plate_ref.png`를 열어 둥근 피자 조각 + 잉크 테두리인지 확인.

- [ ] **Step 2: 참조 업로드 (실루엣 ref) — 스타일 참조는 기존 REF_STYLE 재사용**

`media_upload(files=[{"filename":"zone_plate_ref.png","content_type":"image/png"}])` → `upload_url`, `media_id`.
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/refs" && curl -s -o /dev/null -w "%{http_code}\n" -X PUT -H "Content-Type: image/png" --data-binary "@zone_plate_ref.png" "<upload_url>"
```
Expected: `200`. `media_confirm(media_ids=["<id>"], type="image")`. 생성 로그에 `REF_ZONE_SHAPE = <id>`로 기록. (REF_STYLE `5d029aa6-d2ec-4fd7-9a41-ef6e157d2ffe`가 만료·실패하면 `final/style_tile.png`를 같은 방식으로 재업로드.)

- [ ] **Step 3: 초안 생성 (low, 1k, 2변형) — 사용자 승인 체크포인트**

`generate_image(params={"model":"gpt_image_2_5","prompt":"<아래>","aspect_ratio":"1:1","count":2,"medias":[{"value":"<REF_ZONE_SHAPE>","role":"reference"},{"value":"5d029aa6-d2ec-4fd7-9a41-ef6e157d2ffe","role":"reference"}],"quality":"low","size":"1024x1024","background":"transparent"})` (파라미터 이름은 `models_explore`로 확인한 값을 쓴다 — 리스킨 1·2단계와 동일).

프롬프트:
```
Single 2D game floor decal, transparent background, flat vector, soft pastel cartoon, no text. Redraw the FIRST reference image's exact silhouette — a rounded quarter-ring pizza-slice plate with the pointed end at the bottom-left corner and the wide arc at the top-right — as a cream paper plate (#FAF3E0, keep it very light so it can be tinted), with a thick dark ink outline (#4B425C) running just inside the edge of the shape, and a faint sparse dotted texture on the paper. Keep the whole drawing inside the silhouette; nothing outside it. No stars, no ornaments, no gradients, no glow, no drop shadow. Match the line weight and style of the second reference (the UI style sheet).
```
위젯 결과를 사용자에게 보여 주고 승인 변형을 고른다. 거절 시 프롬프트에서 어긋난 점을 한 줄 고쳐 1회 재생성(0.5). 승인 전에는 고해상 생성으로 넘어가지 않는다.

- [ ] **Step 4: 최종 생성 (high, 2k, transparent) + 다운로드**

승인 변형과 같은 프롬프트로 `generate_image(... "quality":"high","size":"2048x2048","background":"transparent","count":1)`. 결과 URL:
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && mkdir -p candidates final && curl -sL -o candidates/zone_plate_raw.png "<url>" && python -c "from PIL import Image; im=Image.open('candidates/zone_plate_raw.png'); print(im.size, im.mode)"
```
Expected: `(2048, 2048) RGBA`. 생성 로그에 job id·비용(2.75) 기록.

- [ ] **Step 5: 마스킹·정규화 도구 작성 + 실행**

`_workspace/2026-09-25-ui-reskin/tools/finalize_zone_plate.py`:
```python
"""생성 플레이트를 실루엣 마스크로 자르고(밖 알파 0) 종이 영역 밝기를 틴트용으로 정규화한다.
잉크선(어두운 픽셀, L<0.45)은 그대로 두고 그 외 픽셀의 평균 L을 target_l로 맞춘다.
--ink-ring 45 : 생성 결과의 외곽선이 마스크에 잘려 나갔을 때 PIL로 잉크 링(px)을 합성한다."""
import sys
from PIL import Image, ImageChops, ImageFilter

INK = (75, 66, 92)


def main(raw, mask_path, out, target_l=0.94, ink_ring=0):
    im = Image.open(raw).convert("RGBA")
    mask = Image.open(mask_path).convert("RGBA").getchannel("A")
    if im.size != mask.size:
        im = im.resize(mask.size, Image.LANCZOS)

    # 1) 실루엣 밖 알파 0 (안쪽은 생성 알파 유지, 단 안쪽의 구멍은 메운다: 알파 최대 255)
    alpha = ImageChops.multiply(im.getchannel("A"), mask)
    alpha = ImageChops.lighter(alpha, mask.point(lambda a: 255 if a > 200 else 0))
    im.putalpha(alpha)

    # 2) 종이 영역 밝기 정규화
    rgb = im.convert("RGB")
    lum = rgb.convert("L")
    px, lp = rgb.load(), lum.load()
    w, h = rgb.size
    paper = [lp[x, y] / 255 for y in range(0, h, 4) for x in range(0, w, 4) if alpha.getpixel((x, y)) > 200 and lp[x, y] / 255 >= 0.45]
    mean_l = sum(paper) / max(1, len(paper))
    gain = target_l / max(0.01, mean_l)
    print(f"paper mean L={mean_l:.3f} gain={gain:.3f}")
    out_rgb = Image.new("RGB", rgb.size)
    op = out_rgb.load()
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if lp[x, y] / 255 < 0.45:
                op[x, y] = (r, g, b)                       # 잉크선 유지
            else:
                op[x, y] = (min(255, int(r * gain)), min(255, int(g * gain)), min(255, int(b * gain)))
    result = out_rgb.convert("RGBA")
    result.putalpha(alpha)

    # 3) (선택) 잉크 링 합성
    if ink_ring > 0:
        inner = mask.filter(ImageFilter.MinFilter(ink_ring * 2 + 1))
        ring = ImageChops.subtract(mask, inner)
        ink = Image.new("RGBA", mask.size, INK + (255,))
        ink.putalpha(ring)
        result.alpha_composite(ink)

    result.save(out)
    print("saved", out, result.size)


if __name__ == "__main__":
    raw, mask_path, out = sys.argv[1:4]
    ring = int(sys.argv[sys.argv.index("--ink-ring") + 1]) if "--ink-ring" in sys.argv else 0
    main(raw, mask_path, out, ink_ring=ring)
```

실행:
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && python tools/finalize_zone_plate.py candidates/zone_plate_raw.png refs/zone_plate_mask.png final/zone_plate.png
```
Expected: `paper mean L=0.8~0.95 gain≈1.0~1.15`, `saved final/zone_plate.png (2048, 2048)`. `Read`로 확인 — 외곽선이 마스크에 잘려 끊겼으면 `--ink-ring 45`를 붙여 재실행.

- [ ] **Step 6: 에셋 복사 + 임포트 설정 (RunCommand)**

```bash
cp "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/final/zone_plate.png" "D:/Dice Orbit/Assets/Sprites/UI Skin/zone_plate.png"
```
`Unity_RunCommand`:
```csharp
using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh();
        const string path = "Assets/Sprites/UI Skin/zone_plate.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { result.LogError("importer 없음: " + path); return; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.spritePixelsPerUnit = 128f;                 // 2048px = 16유닛
        importer.spritePivot = new Vector2(0f, 0f);          // 궤도 중심 = 좌하단
        importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(0f, 0f);
        importer.SetTextureSettings(settings);
        importer.spriteBorder = Vector4.zero;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        result.Log($"sprite={sprite != null} pivot={sprite.pivot} ppu={sprite.pixelsPerUnit} bounds={sprite.bounds.size}");
    }
}
```
Expected: `pivot=(0.0, 0.0) ppu=128 bounds=(16.0, 16.0, 0.0)`.

- [ ] **Step 7: UiSkin.asset 배선 + 점검**

`Unity_RunCommand`:
```csharp
using UnityEngine;
using UnityEditor;
using DiceOrbit.UI.Skin;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var skin = AssetDatabase.LoadAssetAtPath<UiSkin>("Assets/Resources/UI/UiSkin.asset");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI Skin/zone_plate.png");
        if (skin == null || sprite == null) { result.LogError("skin/sprite 없음"); return; }
        skin.ZonePlate = sprite;
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        var issues = DiceOrbit.EditorTools.UiSkinValidator.Validate(skin);
        result.Log("issues=" + issues.Count + " : " + string.Join(" / ", issues));
    }
}
```
Expected: `issues=1 : IntentBubble: 비어 있음` (말풍선은 Task 3).

- [ ] **Step 8: 커밋 (LFS 포인터 확인)**

```bash
cd "D:/Dice Orbit" && git add "Assets/Sprites/UI Skin/zone_plate.png" "Assets/Sprites/UI Skin/zone_plate.png.meta" Assets/Resources/UI/UiSkin.asset && git commit -q -m "feat(ui-skin): 구역 플레이트 스프라이트 (힉스필드 생성, 실루엣 마스킹) + 스킨 배선

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>" && git show --stat HEAD | tail -5
```

---

### Task 3: 의도 말풍선 스프라이트 생성 + 임포트(9-slice) + 스킨 배선

**Files:**
- Create: `_workspace/2026-09-25-ui-reskin/tools/finalize_bubble.py`
- Create: `Assets/Sprites/UI Skin/intent_bubble.png` (+ .meta)
- Modify: `Assets/Resources/UI/UiSkin.asset` (IntentBubble 배선)

**Interfaces:**
- Produces: `UiSkin.Current.IntentBubble` = 9-slice 스프라이트, 경계 (좌, 하=꼬리+몸통 15%, 우, 상), 「UI 스킨 점검」 이슈 0.

- [ ] **Step 1: 초안 생성 (low, 1k, 2변형) — 사용자 승인 체크포인트**

`generate_image(params={"model":"gpt_image_2_5","prompt":"<아래>","aspect_ratio":"1:1","count":2,"medias":[{"value":"5d029aa6-d2ec-4fd7-9a41-ef6e157d2ffe","role":"reference"}],"quality":"low","size":"1024x1024","background":"transparent"})`

프롬프트:
```
Single 2D game UI element, centered, no text, transparent background, cream paper fill (#FAF3E0), thick dark ink outline (#4B425C), rounded corners, a one-tone darker bottom edge line like a slightly raised paper card, soft pastel cartoon, flat vector, no gradients, no glow, no drop shadow. Same line weight and outline style as the reference image. Item: an empty speech bubble — a wide rounded rectangle (about 5:4) with a short pointed tail at the bottom center pointing straight down. The inside is plain empty cream paper with nothing drawn in it (an icon will be placed there later). No stars, no ornaments, no text.
```
위젯 결과를 사용자에게 보여 승인 변형을 고른다.

- [ ] **Step 2: 최종 생성 (high, 2k, transparent) + 다운로드**

`generate_image(... "quality":"high","size":"2048x2048","background":"transparent","count":1)` →
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && curl -sL -o candidates/intent_bubble_raw.png "<url>" && python -c "from PIL import Image; im=Image.open('candidates/intent_bubble_raw.png'); print(im.size, im.getbbox())"
```

- [ ] **Step 3: 트림 + 9-slice 경계 제안 도구**

`_workspace/2026-09-25-ui-reskin/tools/finalize_bubble.py`:
```python
"""말풍선: 알파 트림(잔여 얼룩 제거) → 최대 변 max_side로 축소 → 꼬리 높이 검출 → 9-slice 경계 제안.
꼬리 = 아래쪽에서 알파 폭이 몸통 최대 폭의 40% 미만인 행들. 경계: 좌/우/상 = 몸통 폭·높이의 15%, 하 = 꼬리 높이 + 몸통 높이 15%."""
import sys
from PIL import Image

ALPHA_THRESHOLD = 8


def main(src, dst, max_side=560):
    im = Image.open(src).convert("RGBA")
    a = im.getchannel("A").point(lambda v: v if v > ALPHA_THRESHOLD else 0)
    im.putalpha(a)
    im = im.crop(im.getbbox())
    k = max_side / max(im.size)
    if k < 1:
        im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    im.save(dst)

    a = im.getchannel("A")
    w, h = im.size
    widths = []
    for y in range(h):
        row = [x for x in range(w) if a.getpixel((x, y)) > 64]
        widths.append((row[-1] - row[0] + 1) if row else 0)
    body_w = max(widths)
    tail_rows = 0
    for y in range(h - 1, -1, -1):
        if widths[y] < body_w * 0.4: tail_rows += 1
        else: break
    body_h = h - tail_rows
    left = right = round(body_w * 0.15)
    top = round(body_h * 0.15)
    bottom = tail_rows + round(body_h * 0.15)
    print(f"size={w}x{h} body={body_w}x{body_h} tail={tail_rows}px border L{left} B{bottom} R{right} T{top}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
```
실행:
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && python tools/finalize_bubble.py candidates/intent_bubble_raw.png final/intent_bubble.png
```
Expected: `size≈560x460 body≈560x380 tail≈80px border L84 B137 R84 T57` 근처 값. 숫자를 Step 4에 넣는다.

- [ ] **Step 4: 에셋 복사 + 임포트(9-slice) + 배선 + 점검**

```bash
cp "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/final/intent_bubble.png" "D:/Dice Orbit/Assets/Sprites/UI Skin/intent_bubble.png"
```
`Unity_RunCommand` (`L/B/R/T`는 Step 3 출력값):
```csharp
using UnityEngine;
using UnityEditor;
using DiceOrbit.UI.Skin;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh();
        const string path = "Assets/Sprites/UI Skin/intent_bubble.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { result.LogError("importer 없음: " + path); return; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.spriteBorder = new Vector4(L, B, R, T);   // (left, bottom, right, top) — finalize_bubble.py 출력
        importer.SaveAndReimport();

        var skin = AssetDatabase.LoadAssetAtPath<UiSkin>("Assets/Resources/UI/UiSkin.asset");
        skin.IntentBubble = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        var issues = DiceOrbit.EditorTools.UiSkinValidator.Validate(skin);
        result.Log($"border={skin.IntentBubble.border} issues={issues.Count} : {string.Join(" / ", issues)}");
        result.Log("selftest=" + DiceOrbit.EditorTools.UiSkinSelfTests.RunAll());
    }
}
```
Expected: `border=(L, B, R, T)` 0 아님, `issues=0`, `selftest=True`.

- [ ] **Step 5: 커밋**

```bash
cd "D:/Dice Orbit" && git add "Assets/Sprites/UI Skin/intent_bubble.png" "Assets/Sprites/UI Skin/intent_bubble.png.meta" Assets/Resources/UI/UiSkin.asset && git commit -q -m "feat(ui-skin): 의도 말풍선 9-slice 스프라이트 (힉스필드 생성) + 스킨 배선

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: ZonePlateRenderer (브래킷 렌더러 대체)

**Files:**
- Create: `Assets/Scripts/Editor/ZonePlateSelfTests.cs`
- Create: `Assets/Scripts/Visuals/ZonePlateRenderer.cs`
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/EncounterSpawner.cs:89`
- Delete: `Assets/Scripts/Visuals/ZoneFloorRenderer.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `UiSkin.Current.GetZonePlate()` (Task 1·2), `CombatZoneManager.Instance` (`ZoneCount`, `IsGeometryReady`, `GetZoneAngularRangeDeg(int, out float, out float)`, `GetOwner(int)`), `MonsterIdentityManager.Instance.GetColor(Monster)`, `MonsterIdentityManager.FindVisibleSprite(Transform)`, `Monster.IntroBaseScale`.
- Produces: `DiceOrbit.Visuals.ZonePlateRenderer` — `static ZonePlateRenderer EnsureInstance()`, `static Quaternion PlateRotation(float startDeg)`, `static Color OwnerTint(Color identity, float saturation, float alpha)`.

- [ ] **Step 1: 헬퍼 자가 테스트 작성 (실패 확인용)**

`Assets/Scripts/Editor/ZonePlateSelfTests.cs`:
```csharp
using DiceOrbit.Visuals;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>ZonePlateRenderer 순수 헬퍼 자가 테스트 — 회전 매핑과 틴트. 메뉴 [DiceOrbit → Run ZonePlate Self-Tests] 또는 RunAll().</summary>
    public static class ZonePlateSelfTests
    {
        private static int _failures;

        [MenuItem("DiceOrbit/Run ZonePlate Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;
            TestRotationAtZeroLaysSpriteFlatFacingPlusZ();
            TestRotationAt90TurnsCounterClockwise();
            TestOwnerTintKeepsHueSoftensSaturation();
            if (_failures == 0) Debug.Log("[SelfTest] 전체 PASS — ZonePlate");
            else Debug.LogError($"[SelfTest] 실패 {_failures}건 — ZonePlate");
            return _failures == 0;
        }

        private static void Check(bool condition, string label)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[SelfTest] FAIL: " + label);
        }

        private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-6f;

        private static void TestRotationAtZeroLaysSpriteFlatFacingPlusZ()
        {
            var q = ZonePlateRenderer.PlateRotation(0f);
            Check(Near(q * Vector3.right, Vector3.right), "startDeg 0: 스프라이트 +x → 월드 +x");
            Check(Near(q * Vector3.up, Vector3.forward), "startDeg 0: 스프라이트 +y(위) → 월드 +z (눕힘)");
        }

        private static void TestRotationAt90TurnsCounterClockwise()
        {
            var q = ZonePlateRenderer.PlateRotation(90f);
            Check(Near(q * Vector3.right, Vector3.forward), "startDeg 90: 스프라이트 +x → 월드 +z (반시계 90°, 구역 각도 체계)");
            Check(Near(q * Vector3.up, Vector3.left), "startDeg 90: 스프라이트 +y → 월드 −x");
        }

        private static void TestOwnerTintKeepsHueSoftensSaturation()
        {
            var c = ZonePlateRenderer.OwnerTint(Color.red, 0.32f, 0.85f);
            Check(Mathf.Approximately(c.r, 1f) && Mathf.Abs(c.g - 0.68f) < 0.01f && Mathf.Abs(c.b - 0.68f) < 0.01f, $"OwnerTint(red): (1, 0.68, 0.68) 근처, 실제 {c}");
            Check(Mathf.Approximately(c.a, 0.85f), "OwnerTint: 알파 유지");
        }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인**

`Unity_RunCommand`: `AssetDatabase.Refresh()` → `Unity_GetConsoleLogs(types=["error"])`.
Expected: `ZonePlateRenderer` 정의 없음 에러.

- [ ] **Step 3: ZonePlateRenderer 작성**

`Assets/Scripts/Visuals/ZonePlateRenderer.cs`:
```csharp
using DiceOrbit.Core;
using DiceOrbit.Core.Zones;
using DiceOrbit.UI.Skin;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 구역 플레이트 — 사분면 조각 스프라이트(UiSkin.ZonePlate)를 타일 아래 바닥에 눕혀 주인 몬스터 색으로 틴트한다
    /// (스펙 2026-09-25-monster-zone-intent-reskin §2). 구 ZoneFloorRenderer(V자·ㄱ자 브래킷)를 대체.
    ///
    /// 스프라이트: pivot (0,0) = 궤도 중심, PPU 128 → 16×16 유닛, 조각은 +x·+y(위) 사분면.
    /// 배치: 위치 (0, plateY, 0), 회전 Euler(90, −startDeg, 0) — 눕힌 뒤 구역 시작각만큼 반시계로 돌린다.
    /// 타일(불투명 메시, y 0~0.03) 아래에 있어 타일이 그 위에 올라앉고, 사이·안팎으로 플레이트가 드러난다.
    ///
    /// 등장: 주인 몬스터가 인트로 팝인으로 드러난 뒤 페이드인, 중립 구역은 주인 있는 몬스터가 전부 드러난 뒤.
    /// </summary>
    public class ZonePlateRenderer : MonoBehaviour
    {
        public static ZonePlateRenderer Instance { get; private set; }

        [Tooltip("플레이트 높이. 타일 바닥(0)보다 아래여야 타일이 위에 올라앉는다")]
        [SerializeField] private float plateY = -0.02f;
        [Header("틴트")]
        [SerializeField, Range(0f, 1f)] private float ownerSaturation = 0.32f;
        [SerializeField, Range(0f, 1f)] private float ownerAlpha = 0.85f;
        [SerializeField] private Color neutralTint = new Color(0.80f, 0.80f, 0.86f, 0.5f);
        [SerializeField] private float fadeDuration = 0.25f;
        [Tooltip("유닛 스프라이트 대비 렌더 순서. 음수여야 유닛·발밑 마커 뒤로 간다")]
        [SerializeField] private int sortingOrderOffset = -200;

        private SpriteRenderer[] _plates = new SpriteRenderer[0];
        private int[] _lastOwnerIds;      // 파괴된 몬스터는 == null이라 InstanceID로 추적
        private bool[] _lastVisible;
        private Color[] _targetTint;
        private float[] _fadeT;
        private bool _sortingSynced;

        // ── 순수 헬퍼 (ZonePlateSelfTests) ─────────────────────

        /// <summary>눕힌 뒤(X 90°) 구역 시작각만큼 반시계로 돌린 회전. 스프라이트 +x → 각도 startDeg 방향.</summary>
        public static Quaternion PlateRotation(float startDeg) => Quaternion.Euler(90f, -startDeg, 0f);

        /// <summary>정체성 색의 색상(H)은 두고 채도를 낮춰 종이 위에 얹기 좋은 틴트로 만든다.</summary>
        public static Color OwnerTint(Color identity, float saturation, float alpha)
        {
            Color.RGBToHSV(identity, out float h, out _, out _);
            var c = Color.HSVToRGB(h, saturation, 1f);
            c.a = alpha;
            return c;
        }

        // ── 수명 ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static ZonePlateRenderer EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ZonePlateRenderer>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            return new GameObject("[ZonePlateRenderer]").AddComponent<ZonePlateRenderer>();
        }

        private void LateUpdate()
        {
            var zones = CombatZoneManager.Instance;
            if (zones == null || !zones.IsGeometryReady) return;   // 준비 전 각도로 놓으면 반 칸 밀기가 빠진다

            if (_plates.Length != zones.ZoneCount) Build(zones);
            SyncSortingBehindUnits();
            Refresh(zones);
            Fade();
        }

        // ── 생성 ─────────────────────────────────────────────

        private void Build(CombatZoneManager zones)
        {
            foreach (var p in _plates) if (p != null) Destroy(p.gameObject);

            var sprite = UiSkin.Current.GetZonePlate();   // 비면 예외 — 브래킷·사각형 폴백 없음
            int n = zones.ZoneCount;
            _plates = new SpriteRenderer[n];
            _lastOwnerIds = new int[n];
            _lastVisible = new bool[n];
            _targetTint = new Color[n];
            _fadeT = new float[n];

            for (int zone = 0; zone < n; zone++)
            {
                zones.GetZoneAngularRangeDeg(zone, out float startDeg, out _);
                var go = new GameObject($"_ZonePlate{zone}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(0f, plateY, 0f);
                go.transform.rotation = PlateRotation(startDeg);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(1f, 1f, 1f, 0f);
                sr.enabled = false;
                _plates[zone] = sr;
                _lastOwnerIds[zone] = int.MinValue;   // 첫 프레임에 반드시 칠하도록
            }
            _sortingSynced = false;
        }

        // ── 색·표시 갱신 ─────────────────────────────────────

        private void Refresh(CombatZoneManager zones)
        {
            var identity = MonsterIdentityManager.Instance;
            bool allOwnersRevealed = AreAllOwnersRevealed(zones);

            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                var owner = zones.GetOwner(zone);
                bool visible = owner != null ? IsMonsterRevealed(owner) : allOwnersRevealed;
                int ownerId = owner != null ? owner.GetInstanceID() : 0;
                if (_lastOwnerIds[zone] == ownerId && _lastVisible[zone] == visible) continue;

                bool wasVisible = _lastVisible[zone];
                _lastOwnerIds[zone] = ownerId;
                _lastVisible[zone] = visible;

                _targetTint[zone] = owner != null && identity != null
                    ? OwnerTint(identity.GetColor(owner), ownerSaturation, ownerAlpha)
                    : neutralTint;

                var sr = _plates[zone];
                if (sr == null) continue;
                sr.enabled = visible;
                if (visible && !wasVisible)
                {
                    _fadeT[zone] = 0f;                                   // 새로 나타남 → 알파 0에서 페이드인
                    var c = _targetTint[zone]; c.a = 0f; sr.color = c;
                }
                else if (visible)
                {
                    _fadeT[zone] = fadeDuration;                         // 주인만 바뀜 → 즉시 색 교체
                    sr.color = _targetTint[zone];
                }
            }
        }

        private void Fade()
        {
            for (int zone = 0; zone < _plates.Length; zone++)
            {
                var sr = _plates[zone];
                if (sr == null || !sr.enabled || _fadeT[zone] >= fadeDuration) continue;
                _fadeT[zone] += Time.deltaTime;
                float k = fadeDuration <= 0f ? 1f : Mathf.Clamp01(_fadeT[zone] / fadeDuration);
                var c = _targetTint[zone];
                c.a = _targetTint[zone].a * k;
                sr.color = c;
            }
        }

        /// <summary>몬스터가 인트로 팝인으로 충분히 드러났는지 — 발밑 마커(MonsterIdentityManager)와 같은 판정.</summary>
        private static bool IsMonsterRevealed(Monster monster)
        {
            float baseX = monster.IntroBaseScale.x;
            float threshold = Mathf.Max(0.02f, baseX * 0.15f);
            return monster.transform.localScale.x > threshold;
        }

        private static bool AreAllOwnersRevealed(CombatZoneManager zones)
        {
            for (int zone = 0; zone < zones.ZoneCount; zone++)
            {
                var owner = zones.GetOwner(zone);
                if (owner != null && !IsMonsterRevealed(owner)) return false;
            }
            return true;
        }

        // ── 정렬 ─────────────────────────────────────────────

        /// <summary>유닛 스프라이트와 같은 정렬 레이어에서 그보다 뒤로. 레이어가 다르면 order를 낮춰도 유닛 위로 튀어나온다.</summary>
        private void SyncSortingBehindUnits()
        {
            if (_sortingSynced || _plates.Length == 0) return;
            var reference = FindAnyUnitSprite();
            if (reference == null) return;   // 유닛이 아직 없다 — 다음 프레임에 다시 시도
            foreach (var sr in _plates)
            {
                if (sr == null) continue;
                sr.sortingLayerID = reference.sortingLayerID;
                sr.sortingOrder = reference.sortingOrder + sortingOrderOffset;
            }
            _sortingSynced = true;
        }

        private static SpriteRenderer FindAnyUnitSprite()
        {
            var combat = CombatManager.Instance;
            if (combat != null && combat.ActiveMonsters != null)
                foreach (var monster in combat.ActiveMonsters)
                    if (monster != null)
                    {
                        var sprite = MonsterIdentityManager.FindVisibleSprite(monster.transform);
                        if (sprite != null) return sprite;
                    }

            var party = PartyManager.Instance != null ? PartyManager.Instance.Party : null;
            if (party != null)
                foreach (var character in party)
                    if (character != null)
                    {
                        var sprite = MonsterIdentityManager.FindVisibleSprite(character.transform);
                        if (sprite != null) return sprite;
                    }
            return null;
        }
    }
}
```

- [ ] **Step 4: 스포너 훅 교체 + 브래킷 렌더러 삭제**

`EncounterSpawner.cs:89` `Visuals.ZoneFloorRenderer.EnsureInstance();` → `Visuals.ZonePlateRenderer.EnsureInstance();`

```bash
cd "D:/Dice Orbit" && git rm -q Assets/Scripts/Visuals/ZoneFloorRenderer.cs Assets/Scripts/Visuals/ZoneFloorRenderer.cs.meta && grep -rn "ZoneFloorRenderer" Assets/Scripts --include=*.cs; echo "refs left: $?"
```
Expected: grep 출력 없음 (`refs left: 1`).

- [ ] **Step 5: 컴파일 + 자가 테스트**

`Unity_RunCommand`: `AssetDatabase.Refresh()` → 콘솔 에러 0 →
```csharp
internal class CommandScript : IRunCommand { public void Execute(ExecutionResult r) { r.Log("zone=" + DiceOrbit.EditorTools.ZonePlateSelfTests.RunAll() + " skin=" + DiceOrbit.EditorTools.UiSkinSelfTests.RunAll()); } }
```
Expected: `zone=True skin=True`.

- [ ] **Step 6: Play 검증 — 플레이트 방향·정렬**

`Unity_ManageEditor(action="play")` → `Unity_RunCommand` (전투 진입 투어 — 기존 `[UiTour]` 코루틴과 같은 흐름):
```csharp
using System.Collections;
using UnityEngine;
using UnityEditor;
using DiceOrbit.Core;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!Application.isPlaying) { result.LogError("플레이 모드가 아닙니다"); return; }
        var go = new GameObject("[UiTour]");
        var runner = go.AddComponent<ZoneTour>();
        runner.StartCoroutine(runner.Run());
        result.Log("구역 투어 시작");
    }

    internal class ZoneTour : MonoBehaviour
    {
        private const string Dir = "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/review/";
        public IEnumerator Run()
        {
            var spawner = Object.FindFirstObjectByType<CharacterSpawner>();
            var warrior = AssetDatabase.LoadAssetAtPath<CharacterPreset>("Assets/Scripts/Data/Character Preset/Warrior/Warrior.asset");
            var mage = AssetDatabase.LoadAssetAtPath<CharacterPreset>("Assets/Scripts/Data/Character Preset/Mage/Mage.asset");
            spawner.Spawn(warrior, 0, 2); spawner.Spawn(mage, 1, 2);
            var sel = Object.FindFirstObjectByType<DiceOrbit.UI.CharacterSelectionUI>(FindObjectsInactive.Include);
            if (sel != null) sel.Hide();
            yield return new WaitForSeconds(0.5f);
            var flow = GameFlowManager.Instance;
            flow.OnRecruitComplete();
            yield return new WaitForSeconds(0.6f);
            flow.OnNodeSelected(0);
            yield return new WaitForSeconds(6f);
            ScreenCapture.CaptureScreenshot(Dir + "zone_01_battle.png");
            yield return new WaitForSeconds(0.5f);
            var zones = DiceOrbit.Core.Zones.CombatZoneManager.Instance;
            var plates = Object.FindFirstObjectByType<DiceOrbit.Visuals.ZonePlateRenderer>();
            for (int z = 0; z < zones.ZoneCount; z++)
            {
                zones.GetZoneAngularRangeDeg(z, out float a, out float b);
                var t = plates.transform.Find($"_ZonePlate{z}");
                var sr = t.GetComponent<SpriteRenderer>();
                Debug.Log($"[UiTour] zone{z} {a:F0}..{b:F0} owner={(zones.GetOwner(z) != null ? zones.GetOwner(z).name : "-")} rot={t.eulerAngles} enabled={sr.enabled} color={sr.color} layer={sr.sortingLayerName}/{sr.sortingOrder}");
            }
            Debug.Log("[UiTour] 완료");
        }
    }
}
```
1~2초 뒤 `Read`로 `review/zone_01_battle.png` 확인. Expected: 사분면마다 플레이트가 타일 아래 깔리고 주인 색으로 옅게 틴트, 중립은 회색; 구역 0(−9°~81°) 플레이트가 화면 우상단 타일 5장 아래; 유닛·발밑 마커가 플레이트 위에 그려짐. 로그: 4구역 `enabled=True`, 잉크선이 타일 사이로 보임. 어긋나면(예: 좌우 반전) `PlateRotation` 부호를 고치고 자가 테스트 기대값도 함께 수정한다 — 둘이 어긋난 채 두지 않는다. `Unity_ManageEditor(action="stop")`.

- [ ] **Step 7: 커밋**

```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/Visuals/ZonePlateRenderer.cs Assets/Scripts/Visuals/ZonePlateRenderer.cs.meta Assets/Scripts/Editor/ZonePlateSelfTests.cs Assets/Scripts/Editor/ZonePlateSelfTests.cs.meta Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/EncounterSpawner.cs && git commit -q -m "feat(visuals): 구역 잉크 플레이트 렌더러 — 브래킷 LineRenderer 철거, 주인 색 틴트·페이드인

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```
(`git rm`한 ZoneFloorRenderer는 이미 스테이징되어 함께 커밋된다. `.meta`가 아직 생성 전이면 Refresh 후 다시 add.)

---

### Task 5: MonsterUI 말풍선 — 코드 정리 + 프리팹 재구성

**Files:**
- Modify: `Assets/Scripts/UI/MonsterUI.cs`
- Modify (RunCommand): `Assets/Prefabs/TestMonster.prefab`

**Interfaces:**
- Consumes: `UiSkin.ApplyIntentBubble(Image)`, `SkinPart.IntentBubble`, `UiSkinImage.SetPart` (Task 1), `UiSkin.IntentBubble` 스프라이트 (Task 3).
- Produces: `MonsterUI` 슬롯 `intentBubbleRoot`(RectTransform, 말풍선 루트 — UiSkinImage IntentBubble), `intentIcon`(Image, 스킬 아이콘). 공개 API `IntentBubbleRect`/`IsIntentBubbleActive`/`IntentWorldCamera`는 그대로.

- [ ] **Step 1: MonsterUI 필드·의도 갱신 코드 정리**

`Assets/Scripts/UI/MonsterUI.cs`:

필드 블록 교체 — `[SerializeField] private RectTransform intentBubbleRoot;` ~ `[SerializeField] private TextMeshProUGUI intentText;` 4줄을:
```csharp
        [Header("의도 말풍선 (프리팹 MonsterCanvas/IntentBubble — UiSkinImage IntentBubble 9-slice, 아이콘만·라벨 없음, 2026-09-25)")]
        [SerializeField] private RectTransform intentBubbleRoot;   // 말풍선 루트 (스프라이트는 UiSkinImage가 꽂는다)
        [SerializeField] private Image intentIcon;                 // 스킬 아이콘 — AttackIntent.Icon
```

`[Header("Intent Colors")]` ~ `neutralBubbleColor` 6줄 삭제.

`[Header("Settings")]` 블록에서 `hideBubbleWhenNoIntent` 아래 두 줄(`tintBubbleByIntent`, `showIntentText`) 삭제.

`private void Awake()`의 `AutoResolveIntentRefs();` → `RequireIntentRefs();`

`AutoResolveIntentRefs()` 메서드 전체 교체:
```csharp
        /// <summary>말풍선 슬롯은 프리팹 배선 필수 — 비면 에러로 알리고 말풍선을 끈다 (형제 오브젝트를 뒤져 채우는 폴백 없음).</summary>
        private void RequireIntentRefs()
        {
            if (intentBubbleRoot == null || intentIcon == null)
                Debug.LogError("[MonsterUI] intentBubbleRoot/intentIcon 슬롯이 비어 있습니다 — TestMonster.prefab MonsterCanvas/IntentBubble/Icon을 배선하세요.", this);
        }
```

`UpdateIntent()` 전체 교체:
```csharp
        /// <summary>공격 의도 말풍선 갱신 — 아이콘만 (라벨·타입 색 없음). 아이콘 없는 의도는 데이터 누락으로 보고 에러 1회.</summary>
        private void UpdateIntent()
        {
            if (monster == null || intentBubbleRoot == null) { SetIntentVisible(false); return; }

            AttackIntent intent = monster.CurrentIntent;
            if (intent == null)
            {
                if (hideBubbleWhenNoIntent) SetIntentVisible(false);
                return;
            }

            SetIntentVisible(true);

            int visualKey = BuildIntentVisualKey(intent);
            bool changed = visualKey != lastIntentVisualKey;
            lastIntentVisualKey = visualKey;

            if (intentIcon != null)
            {
                if (intent.Icon != null)
                {
                    intentIcon.sprite = intent.Icon;
                    intentIcon.enabled = true;
                }
                else
                {
                    intentIcon.enabled = false;
                    if (changed) Debug.LogError($"[MonsterUI] {monster.name}의 의도 '{intent.Type}'에 아이콘이 없습니다 — 몬스터 스킬 데이터에 Icon을 지정하세요.", monster);
                }
            }

            if (animateOnIntentChange && changed) PlayIntentPop();
        }
```

`SetIntentVisible()` 교체:
```csharp
        private void SetIntentVisible(bool visible)
        {
            if (intentBubbleRoot != null && intentBubbleRoot.gameObject.activeSelf != visible)
                intentBubbleRoot.gameObject.SetActive(visible);
        }
```

`GetIntentLabel(...)` 메서드와 `GetIntentColor(...)` 메서드(및 그 `/// 의도 타입별 색상` 주석) 삭제.

- [ ] **Step 2: 컴파일 확인**

`Unity_RunCommand`: `AssetDatabase.Refresh()` → `Unity_GetConsoleLogs(types=["error"])` 0건 (남은 참조가 있으면 그 줄을 삭제).

- [ ] **Step 3: 프리팹 재구성 (RunCommand)**

```csharp
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.UI;
using DiceOrbit.UI.Skin;
using UImage = UnityEngine.UI.Image;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.LogError("플레이 모드입니다"); return; }
        const string path = "Assets/Prefabs/TestMonster.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var ui = root.GetComponentInChildren<MonsterUI>(true);
            var canvas = root.transform.Find("MonsterCanvas") ?? ui.GetComponentInChildren<Canvas>(true).transform;
            var hpBar = canvas.Find("HPBar") as RectTransform;
            var old = canvas.Find("IntentIcon");
            if (ui == null || canvas == null || hpBar == null || old == null) { result.LogError($"구조 확인 실패 ui={ui != null} canvas={canvas != null} hp={hpBar != null} old={old != null}"); return; }

            // 옛 말풍선(IntentIcon: 흰 말풍선 스프라이트 + IntentText + 빈 Image) 제거
            Object.DestroyImmediate(old.gameObject);

            // 새 말풍선: 9-slice 스킨 + 아이콘
            var bubble = new GameObject("IntentBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(UImage)).GetComponent<RectTransform>();
            bubble.SetParent(canvas, false);
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
            bubble.pivot = new Vector2(0.5f, 0.5f);
            bubble.sizeDelta = new Vector2(92f, 84f);
            bubble.anchoredPosition = new Vector2(hpBar.anchoredPosition.x, 78f);   // HP바 위 중앙
            var bubbleImg = bubble.GetComponent<UImage>();
            bubbleImg.raycastTarget = false;
            bubble.gameObject.AddComponent<UiSkinImage>().SetPart(SkinPart.IntentBubble);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UImage)).GetComponent<RectTransform>();
            icon.SetParent(bubble, false);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(56f, 56f);
            icon.anchoredPosition = new Vector2(0f, 6f);   // 꼬리 위 몸통 중앙
            var iconImg = icon.GetComponent<UImage>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var so = new SerializedObject(ui);
            so.FindProperty("intentBubbleRoot").objectReferenceValue = bubble;
            so.FindProperty("intentIcon").objectReferenceValue = iconImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            result.Log($"saved: bubble={bubble.sizeDelta} at {bubble.anchoredPosition}, icon={icon.sizeDelta}, sprite={bubbleImg.sprite?.name} type={bubbleImg.type}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
```
Expected: `sprite=intent_bubble type=Sliced`. 이어서 `python dump_subtree.py Assets/Prefabs/TestMonster.prefab MonsterCanvas`(스크래치패드 도구)로 `IntentBubble`(92×84) → `Icon`(56×56) 구조와 `IntentText` 부재 확인.

- [ ] **Step 4: Play 검증 — 말풍선**

Task 4 Step 6의 투어를 다시 실행하되 캡처 이름 `zone_02_bubble.png`, 추가로 몬스터 1마리 `BattleInfoPanelUI.Instance.ShowUnitExternal(m)` 후 `zone_03_monster_hover.png` 캡처(호버 붉은 외곽선과 플레이트 공존). `Read`로 확인: 크림 말풍선이 HP바 위 중앙, 스킬 아이콘이 크게, 글자 없음, 꼬리가 몬스터를 가리킴. 콘솔에 `[MonsterUI] … 아이콘이 없습니다` 에러가 있으면 해당 몬스터 스킬 데이터의 Icon 누락 — 목록을 보고서에 적는다(데이터 수정은 범위 밖). `Unity_ManageEditor(action="stop")`.

- [ ] **Step 5: 커밋**

```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/UI/MonsterUI.cs Assets/Prefabs/TestMonster.prefab && git commit -q -m "feat(ui): 몬스터 의도 말풍선 — 크림 9-slice + 아이콘 56, 라벨·타입 색 제거, 슬롯 배선 필수

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: 공격 예정 타일 밴드 진하게

**Files:**
- Modify: `Assets/Scripts/Visuals/MonsterTileColorOverlayManager.cs:19`

- [ ] **Step 1: 기본값 변경**

```csharp
        [Tooltip("공격 예정 타일 색 밴드 알파. 0.55는 살구색이 연해 위협이 약했다 → 0.8 (2026-09-25 몬스터 영역 표시 리스킨)")]
        [SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.8f;
```
(씬·프리팹에 이 컴포넌트가 직렬화돼 있지 않음 — `EnsureInstance`로 런타임 생성 — 을 `grep -rn overlayAlpha Assets/Scenes Assets/Prefabs`로 재확인. 출력 없음이면 기본값만으로 충분.)

- [ ] **Step 2: 컴파일 + 커밋**

`AssetDatabase.Refresh()` → 에러 0.
```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/Visuals/MonsterTileColorOverlayManager.cs && git commit -q -m "feat(visuals): 공격 예정 타일 색 밴드 알파 0.55 → 0.8

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: 통합 검증 + 전후 비교 + 문서·메모리

**Files:**
- Modify: `Docs/battle_visual_language.md`, `Docs/README.md`, `Docs/superpowers/specs/2026-09-25-monster-zone-intent-reskin-design.md`(상태), `Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md`
- Modify: `C:/Users/kokyungwoo/.claude/projects/D--Dice-Orbit/memory/project_ui_reskin.md`

- [ ] **Step 1: 최종 투어 캡처 + 전후 비교 시트**

Play → Task 4 Step 6 투어(캡처 `zone_final_battle.png`, `zone_final_hover.png`) → stop. 콘솔 에러 0 확인.
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/review" && python - <<'EOF'
from PIL import Image
a = Image.open("info2_01_warrior.png").crop((280,150,1220,930)); b = Image.open("zone_final_battle.png").crop((280,150,1220,930))
s = Image.new("RGB",(a.width*2+12,a.height),(40,40,40)); s.paste(a,(0,0)); s.paste(b,(a.width+12,0)); s.save("zone_before_after.png"); print(s.size)
EOF
```
`SendUserFile`로 `zone_before_after.png` 전달.

- [ ] **Step 2: 시각 언어 사전 갱신**

`Docs/battle_visual_language.md` 의미→형태 표에서 "몬스터 공격 예고 타일" 행 아래에 추가하고 기각 목록에 브래킷을 넣는다:
```markdown
| 구역(사분면) 소유 | 타일 아래 **잉크 플레이트**(사분면 조각 스프라이트, 주인 색 틴트 / 중립 회색) | `Visuals/ZonePlateRenderer.cs` + `UiSkin.ZonePlate` (2026-09-25) |
| 몬스터 다음 행동 | 머리 위 **크림 말풍선 + 스킬 아이콘**(라벨 없음) | `UI/MonsterUI.cs`, `UiSkin.IntentBubble` (2026-09-25) |
```
기각 목록:
```markdown
- **구역 브래킷(중앙 V자 + 모서리 ㄱ자 LineRenderer)** — 낙서처럼 읽히고 구역 범위가 안 보임 → 잉크 플레이트로 대체 (2026-09-25)
```

- [ ] **Step 3: README·스펙 상태·생성 로그**

`Docs/README.md` 스펙 행 상태 `설계 승인, 구현 대기` → `구현됨`, 계획 행 추가:
```markdown
| [superpowers/plans/2026-09-25-monster-zone-intent-reskin.md](superpowers/plans/2026-09-25-monster-zone-intent-reskin.md) | 구역 플레이트·의도 말풍선 **구현 계획** | 완료 |
```
스펙 상단 상태를 `**구현 완료 (2026-09-25)** — …` 로. 생성 로그 표에 zone_plate·intent_bubble 행(모델/품질/참조/job id/결과/비용)과 `REF_ZONE_SHAPE` media_id, 누적 비용·`balance` 응답을 추가.

- [ ] **Step 4: 메모리 갱신**

`project_ui_reskin.md`에 한 단락: 몬스터 영역 표시 리스킨 완료(플레이트·말풍선·밴드), 스프라이트 2장 위치, 플레이트 기하(1유닛=128px, pivot 0,0, Euler(90,−startDeg,0)), 남은 것(발밑 마커·HP바·FloatingIntentUI·모집 화면). `MEMORY.md` 한 줄 갱신.

- [ ] **Step 5: 커밋**

```bash
cd "D:/Dice Orbit" && git add Docs/battle_visual_language.md Docs/README.md "Docs/superpowers/specs/2026-09-25-monster-zone-intent-reskin-design.md" "Docs/superpowers/plans/2026-09-25-monster-zone-intent-reskin.md" Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md && git commit -q -m "docs(ui): 몬스터 영역 표시 리스킨 — 시각 언어 사전·계획·생성 로그 갱신

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```
`git status --short`에 부수변경(폰트 SDF, .mat, ProjectSettings)만 남는지 확인.
