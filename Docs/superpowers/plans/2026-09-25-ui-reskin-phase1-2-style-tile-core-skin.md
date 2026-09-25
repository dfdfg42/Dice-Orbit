# UI 리스킨 1·2단계 — 스타일 타일 + 코어 세트 + UiSkin 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 힉스필드로 승인된 스타일 타일과 코어 UI 스프라이트 17장(9-slice 6 + 버튼 상태 8 + 아이콘 3)을 만들고, 이를 담는 `UiSkin` ScriptableObject·적용 헬퍼·점검 메뉴·자가 테스트를 완성한다. 이 단계가 끝나면 스킨 에셋은 완전하지만 아직 어떤 화면도 스킨을 읽지 않는다 (3단계에서 전환).

**Architecture:** `UiSkin`(Resources 로드, 없으면 예외)이 팔레트·스프라이트·버튼 세트를 소유하고 `ApplyPanel/Card/Chip/Tooltip/Slot/Divider/Button` 헬퍼로 Image·Button에 꽂는다. 스프라이트는 힉스필드 `gpt_image_2_5`로 생성 → 파이썬(PIL)으로 트리밍·분할 → `Assets/Sprites/UI Skin/`에 반입 → Unity MCP RunCommand로 임포터(9-slice·Mipmap·Trilinear·FullRect) 설정. 검증은 에디터 자가 테스트(`UiSkinSelfTests.RunAll`)와 점검 메뉴(`UiSkinValidator`).

**Tech Stack:** Unity 6000.3.8f1 (Assembly-CSharp, asmdef 없음), Unity MCP (`Unity_RunCommand`, `Unity_GetConsoleLogs`), 힉스필드 MCP (`media_upload`/`media_confirm`/`generate_image`/`generate_image_batch`/`jobs_wait`/`show_generation_by_ids`/`balance`), Python 3.11 + PIL 11.2, curl.

**스펙:** `Docs/superpowers/specs/2026-09-25-ui-reskin-uiskin-higgsfield-design.md` §1, §3.1~3.4, §4.1, §5, §6(1·2단계)

## Global Constraints

- 모델 `gpt_image_2_5`. 탐색 = `quality: "low", resolution: "1k"` (0.25크레딧/장). 최종 = `quality: "high", resolution: "2k", background: "transparent"` (2.75크레딧/장).
- 참조 이미지는 항상 첨부 (`medias[].role = "image_references"`): `node_battle.png` + 승인된 스타일 타일. 색 참조로 `titleScreenBG.png`, `dice button.png`.
- **사용자가 승인한 결과만 `Assets/`에 들어간다.** 후보는 `_workspace/2026-09-25-ui-reskin/candidates/`에 남긴다.
- 크레딧 상한 60. 누적 30 도달 시 `balance` 확인 후 사용자에게 중간 보고.
- 생성마다 프롬프트·파라미터·media_id·job_id·비용을 `Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md`에 기록.
- 글자를 이미지에 굽지 않는다 (프롬프트에 항상 "no text").
- 임포트: Sprite(2D and UI), Single, alphaIsTransparency, **Mipmap on + Trilinear**, Sliced 부품은 Mesh Type **FullRect**, 9-slice 경계 > 0.
- 스킨 에셋 부재·필드 부재는 **예외/에러로 드러낸다.** 기본색·기본 스프라이트 폴백 금지.
- `use_unlim`은 넘기지 않는다. 서버가 `unlim_choice`를 돌려주면 사용자에게 묻고 답대로 다시 호출.
- 컴파일 검증은 `typeof` 게이트가 아니라 `Unity_GetConsoleLogs(logTypes: "Error")` = 0건으로 한다.
- 커밋 메시지 끝: `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`

## File Structure

| 경로 | 책임 |
|---|---|
| `_workspace/2026-09-25-ui-reskin/refs/` | 힉스필드 참조 이미지 사본 (gitignore) |
| `_workspace/2026-09-25-ui-reskin/candidates/` | 다운로드한 생성 결과 원본 (gitignore) |
| `_workspace/2026-09-25-ui-reskin/final/` | 트리밍·분할 완료, 반입 직전 PNG (gitignore) |
| `_workspace/2026-09-25-ui-reskin/tools/trim_png.py` | 알파 bbox 트리밍 + 최대 변 리사이즈 + 패딩 |
| `_workspace/2026-09-25-ui-reskin/tools/split_strip.py` | 버튼 스트립을 투명 간격 기준으로 4조각 분할 (`--equal N` 옵션) |
| `_workspace/2026-09-25-ui-reskin/tools/contact_sheet.py` | 최종 PNG 한눈에 보는 시트 (사용자 육안 확인용) |
| `Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md` | 생성 로그 (재현용) |
| `Assets/Sprites/UI Skin/*.png` | 코어 세트 17장 (LFS 포인터로 커밋됨 — `.gitattributes` `*.png`) |
| `Assets/Scripts/UI/Skin/UiSkin.cs` | `SkinPart`, `ButtonKind`, `ButtonSpriteSet`, `UiSkin` (팔레트·스프라이트·Apply 헬퍼) |
| `Assets/Scripts/Editor/UiSkinValidator.cs` | 순수 `Validate(UiSkin)` + 메뉴 「도구/Dice Orbit/UI 스킨 점검」 |
| `Assets/Scripts/Editor/UiSkinSelfTests.cs` | 자가 테스트 (메뉴 + `RunAll()`) |
| `Assets/Resources/UI/UiSkin.asset` | 스킨 에셋 (RunCommand로 생성·배선) |

---

### Task 1: 브랜치·작업 폴더·생성 로그·참조 이미지 업로드

**Files:**
- Create: `_workspace/2026-09-25-ui-reskin/{refs,candidates,final,tools}/`
- Create: `Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md`
- Modify: `.gitignore` (끝에 1줄)

**Interfaces:**
- Produces: 힉스필드 media_id 3개 (`REF_NODE`, `REF_TITLE`, `REF_DICEBTN`) — 이후 모든 생성의 `medias`에 사용. 생성 로그 파일.

- [x] **Step 1: 브랜치 생성**

```bash
cd "D:/Dice Orbit" && git checkout design/ui-reskin-20260925 && git checkout -b feature/ui-skin-core-20260925
```
Expected: `Switched to a new branch 'feature/ui-skin-core-20260925'`

- [x] **Step 2: 작업 폴더 + gitignore + 참조 사본**

```bash
cd "D:/Dice Orbit" && mkdir -p _workspace/2026-09-25-ui-reskin/{refs,candidates,final,tools} \
 && printf '\n# UI 리스킨 생성 작업물 (2026-09-25) — 승인본은 Assets/로 반입, 나머지는 로컬만\n_workspace/2026-09-25-ui-reskin/\n' >> .gitignore \
 && cp "Assets/Sprites/노드/node_battle.png" _workspace/2026-09-25-ui-reskin/refs/ref_node_battle.png \
 && cp "Assets/Sprites/메인화면/main menu/titleScreenBG.png" _workspace/2026-09-25-ui-reskin/refs/ref_title_bg.png \
 && cp "Assets/Sprites/UI 이미지/dice button.png" _workspace/2026-09-25-ui-reskin/refs/ref_dice_button.png \
 && ls -la _workspace/2026-09-25-ui-reskin/refs && git check-ignore -q _workspace/2026-09-25-ui-reskin/refs/ref_node_battle.png && echo IGNORED_OK
```
Expected: 파일 3개 나열, 마지막 줄 `IGNORED_OK`.

- [x] **Step 3: 생성 로그 파일 작성**

`Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md`:
```markdown
# UI 리스킨 생성 로그

> 스펙 §5 재현성 규칙. 모든 힉스필드 호출을 기록한다. 비용은 호출 시점의 응답 기준.

## 참조 이미지 (media_id)

| 이름 | 원본 | media_id |
|---|---|---|
| REF_NODE | Assets/Sprites/노드/node_battle.png | (Task 1 Step 4) |
| REF_TITLE | Assets/Sprites/메인화면/main menu/titleScreenBG.png | (Task 1 Step 4) |
| REF_DICEBTN | Assets/Sprites/UI 이미지/dice button.png | (Task 1 Step 4) |
| REF_STYLE | 승인된 스타일 타일 (Task 2) | (Task 2 Step 5) |

## 생성 기록

| # | 부품 | 모델 / quality / resolution / bg / aspect | 참조 | job_id | 결과 | 비용 |
|---|---|---|---|---|---|---|

## 프롬프트 공통 접두 (스펙 §5)

`Single 2D game UI element, centered, no text, transparent background, cream paper fill (#FAF3E0), thick dark ink outline (#4B425C), rounded corners, a one-tone darker bottom edge line like a slightly raised paper card, soft pastel cartoon, flat vector, no gradients, no glow, no drop shadow. Same line weight and outline style as the reference images.`

## 누적 비용

| 시점 | 누적 크레딧 | balance 응답 |
|---|---|---|
| 시작 | 0 | 80 |
```

- [x] **Step 4: 참조 이미지 업로드**

힉스필드 도구 호출:
```
media_upload(files=[{"filename":"ref_node_battle.png","content_type":"image/png"},
                    {"filename":"ref_title_bg.png","content_type":"image/png"},
                    {"filename":"ref_dice_button.png","content_type":"image/png"}])
```
응답의 각 `upload_url`로 PUT (파일마다 1회):
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/refs" && curl -s -o /dev/null -w "%{http_code}\n" -X PUT -H "Content-Type: image/png" --data-binary "@ref_node_battle.png" "<upload_url_1>"
```
Expected: 각각 `200`.
그 다음:
```
media_confirm(media_ids=["<id1>","<id2>","<id3>"], type="image")
```
Expected: 3건 confirmed. media_id 3개를 로그 「참조 이미지」 표에 기입.

- [x] **Step 5: 커밋**

```bash
cd "D:/Dice Orbit" && git add .gitignore Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md && git commit -q -m "chore(ui-skin): 리스킨 작업 폴더·생성 로그·참조 media_id 기록

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>" && git log --oneline -1
```

---

### Task 2: 스타일 타일 생성 → 사용자 승인 → 참조 등록

**Files:**
- Create: `_workspace/2026-09-25-ui-reskin/candidates/style_tile_{1,2,3}.png`
- Create: `_workspace/2026-09-25-ui-reskin/final/style_tile.png`
- Modify: 생성 로그

**Interfaces:**
- Consumes: `REF_NODE`, `REF_TITLE`, `REF_DICEBTN`
- Produces: `REF_STYLE` media_id (승인 시트). 이후 Task 3·4의 모든 요청은 `medias = [REF_NODE, REF_STYLE]`.

- [x] **Step 1: 스타일 타일 3변형 생성 (위젯, 저화질)**

```
generate_image(params={
  "model": "gpt_image_2_5", "quality": "low", "resolution": "1k", "aspect_ratio": "4:3", "count": 3,
  "medias": [{"value":"<REF_NODE>","role":"image_references"},{"value":"<REF_TITLE>","role":"image_references"},{"value":"<REF_DICEBTN>","role":"image_references"}],
  "prompt": "Game UI style sheet for a cute pastel cartoon roguelike, neatly arranged on a single sheet with generous spacing on a plain flat light gray background. Items: (1) a large rounded rectangle panel with tiny star ornaments only at its top corners, (2) a smaller content card with a slightly tighter corner radius and no ornament, (3) a small pill-shaped label chip in a slightly deeper cream (#E8DBC3) with a thin outline, (4) a rectangular tooltip box without a tail, (5) an empty inset square slot with a subtle inner ring, (6) a thin horizontal divider line with a small star at its center, (7) a pill button filled with pastel pink (#FFA6F2) shown in four states side by side: normal, hover (slightly brighter, thicker outline), pressed (pushed down, bottom edge line gone), disabled (desaturated gray paper, dashed outline), (8) the same four states of a pill button filled with pastel sky blue (#A6DDFF), (9) three icons: a gold coin, an empty potion bottle outline, a bold close X. All items share: cream paper fill (#FAF3E0), thick dark ink outline (#4B425C), rounded corners, a one-tone darker bottom edge line like a slightly raised paper card, flat 2D vector look, no gradients, no glow, no drop shadows, no text or letters anywhere. Match the line weight and outline style of the reference battle icon; take the pastel palette from the reference night sky and the pink from the reference button."
})
```
`unlim_choice`가 오면 사용자에게 묻고 답대로 재호출. 응답의 job_id(들)를 로그에 기록 (비용 0.75).

- [x] **Step 2: 결과 다운로드**

```
jobs_wait(jobs=[{"index":0,"job_id":"<job>"} ...], timeout_seconds=15)
```
`all_terminal`이 false면 `poll_after_seconds` 뒤 재호출. 결과 URL마다:
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/candidates" && curl -sL -o style_tile_1.png "<url_1>" && curl -sL -o style_tile_2.png "<url_2>" && curl -sL -o style_tile_3.png "<url_3>" && python -c "from PIL import Image; [print(n, Image.open(n).size) for n in ['style_tile_1.png','style_tile_2.png','style_tile_3.png']]"
```
Expected: 3파일, 각 크기 출력 (1k 기준 약 1024×768).

- [x] **Step 3: 사용자에게 선택 요청**

위젯에 3장이 보인다. `AskUserQuestion`으로 1/2/3 중 선택 또는 "재생성(메모에 바꿀 점)" 을 묻는다. 재생성이면 메모를 프롬프트에 반영해 Step 1을 반복 (1회당 0.75, 최대 2회).

- [x] **Step 4: 승인본 확정**

```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && cp candidates/style_tile_<N>.png final/style_tile.png && ls -la final/style_tile.png
```

- [x] **Step 5: 승인본을 참조로 업로드**

```
media_upload(files=[{"filename":"style_tile.png","content_type":"image/png"}])
```
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/final" && curl -s -o /dev/null -w "%{http_code}\n" -X PUT -H "Content-Type: image/png" --data-binary "@style_tile.png" "<upload_url>"
```
Expected: `200`. 이어서 `media_confirm(media_ids=["<id>"], type="image")`. media_id를 로그 `REF_STYLE`에 기입.

- [x] **Step 6: 로그 커밋**

```bash
cd "D:/Dice Orbit" && git add Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md && git commit -q -m "docs(ui-skin): 스타일 타일 승인 — 생성 로그 갱신

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: 파이썬 후처리 도구 작성

**Files:**
- Create: `_workspace/2026-09-25-ui-reskin/tools/trim_png.py`
- Create: `_workspace/2026-09-25-ui-reskin/tools/split_strip.py`
- Create: `_workspace/2026-09-25-ui-reskin/tools/contact_sheet.py`
- Test: 같은 폴더에서 합성 이미지로 즉석 검증 (아래 Step 4)

**Interfaces:**
- Produces: `python tools/trim_png.py <src> <dst> <max_side>`, `python tools/split_strip.py <src> <out_prefix> <name1,name2,...> <max_side> [--equal N]`, `python tools/contact_sheet.py <dir> <out.png>`

- [x] **Step 1: trim_png.py**

```python
"""알파 bbox로 트리밍 → 최대 변 max_side로 축소(LANCZOS) → 투명 패딩 pad px."""
import sys
from PIL import Image


def trim_image(im: Image.Image, dst: str, max_side: int, pad: int = 4) -> Image.Image:
    im = im.convert("RGBA")
    bbox = im.getchannel("A").getbbox()
    if bbox is None:
        raise SystemExit(f"알파가 전부 0이라 트리밍할 수 없음: {dst}")
    im = im.crop(bbox)
    w, h = im.size
    scale = min(1.0, max_side / max(w, h))
    if scale < 1.0:
        im = im.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
    canvas = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
    canvas.paste(im, (pad, pad))
    canvas.save(dst)
    print(f"{dst}: {canvas.size}")
    return canvas


def main() -> None:
    if len(sys.argv) != 4:
        raise SystemExit("사용법: trim_png.py <src> <dst> <max_side>")
    trim_image(Image.open(sys.argv[1]), sys.argv[2], int(sys.argv[3]))


if __name__ == "__main__":
    main()
```

- [x] **Step 2: split_strip.py**

```python
"""가로 스트립을 투명 간격 기준으로 N조각 분할. 간격이 없으면 --equal N 으로 등분."""
import sys
from PIL import Image
from trim_png import trim_image


def find_segments(im: Image.Image, threshold: int = 8) -> list[tuple[int, int]]:
    alpha = im.getchannel("A")
    w, h = im.size
    data = alpha.tobytes()
    col_on = []
    for x in range(w):
        on = False
        for y in range(0, h, 2):
            if data[y * w + x] > threshold:
                on = True
                break
        col_on.append(on)
    segments, start = [], None
    for x, on in enumerate(col_on + [False]):
        if on and start is None:
            start = x
        elif not on and start is not None:
            segments.append((start, x))
            start = None
    # 폭이 전체의 3% 미만인 조각(먼지)은 버린다
    return [s for s in segments if (s[1] - s[0]) >= w * 0.03]


def main() -> None:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    equal = None
    for i, a in enumerate(sys.argv):
        if a == "--equal":
            equal = int(sys.argv[i + 1])
    if len(args) != 4:
        raise SystemExit("사용법: split_strip.py <src> <out_prefix> <name1,name2,...> <max_side> [--equal N]")
    src, out_prefix, names_csv, max_side = args[0], args[1], args[2], int(args[3])
    names = names_csv.split(",")
    im = Image.open(src).convert("RGBA")
    w, h = im.size
    if equal:
        step = w / equal
        segments = [(round(i * step), round((i + 1) * step)) for i in range(equal)]
    else:
        segments = find_segments(im)
    if len(segments) != len(names):
        raise SystemExit(f"조각 {len(segments)}개 발견, {len(names)}개 기대. 경계={segments}. --equal {len(names)} 로 등분하거나 수동 분할.")
    for (a, b), name in zip(segments, names):
        trim_image(im.crop((a, 0, b, h)), f"{out_prefix}{name}.png", max_side)


if __name__ == "__main__":
    main()
```

- [x] **Step 3: contact_sheet.py**

```python
"""폴더의 PNG를 체커보드 위에 격자로 배치한 시트 (육안 검토용)."""
import sys, glob, os
from PIL import Image, ImageDraw


def checker(size: tuple[int, int], cell: int = 16) -> Image.Image:
    im = Image.new("RGBA", size, (200, 200, 200, 255))
    d = ImageDraw.Draw(im)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2 == 0:
                d.rectangle([x, y, x + cell - 1, y + cell - 1], fill=(235, 235, 235, 255))
    return im


def main() -> None:
    src_dir, out = sys.argv[1], sys.argv[2]
    files = sorted(glob.glob(os.path.join(src_dir, "*.png")))
    if not files:
        raise SystemExit(f"PNG 없음: {src_dir}")
    thumb, cols, pad = 256, 6, 12
    rows = (len(files) + cols - 1) // cols
    sheet = checker((cols * (thumb + pad) + pad, rows * (thumb + pad + 20) + pad))
    d = ImageDraw.Draw(sheet)
    for i, f in enumerate(files):
        im = Image.open(f).convert("RGBA")
        im.thumbnail((thumb, thumb), Image.LANCZOS)
        x = pad + (i % cols) * (thumb + pad)
        y = pad + (i // cols) * (thumb + pad + 20)
        sheet.paste(im, (x + (thumb - im.width) // 2, y + (thumb - im.height) // 2), im)
        d.text((x, y + thumb + 2), os.path.basename(f), fill=(30, 30, 30, 255))
    sheet.save(out)
    print(f"{out}: {len(files)}장, {sheet.size}")


if __name__ == "__main__":
    main()
```

- [x] **Step 4: 도구 즉석 검증 (합성 스트립)**

```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && python - <<'EOF'
from PIL import Image, ImageDraw
im = Image.new("RGBA", (800, 200), (0,0,0,0)); d = ImageDraw.Draw(im)
for i in range(4):
    d.rounded_rectangle([i*200+20, 40, i*200+180, 160], radius=40, fill=(255,166,242,255), outline=(75,66,92,255), width=8)
im.save("candidates/_test_strip.png")
EOF
python tools/split_strip.py candidates/_test_strip.png final/_test_ a,b,c,d 128 && python tools/trim_png.py final/_test_a.png final/_test_a2.png 64 && python tools/contact_sheet.py final final/_test_sheet.png && rm final/_test_* candidates/_test_strip.png
```
Expected: `final/_test_a.png: (136, 128)` 류 4줄, `final/_test_a2.png: (72, 68)` 류 1줄, `final/_test_sheet.png: 5장, ...`. 에러 없음.

(도구는 gitignore 폴더라 커밋하지 않는다. 계획 문서에 코드가 있으므로 재현 가능.)

---

### Task 4: 코어 9종 생성 (9-slice 6 + 아이콘 3) → 승인 → 후처리

**Files:**
- Create: `_workspace/2026-09-25-ui-reskin/candidates/{panel,card,chip,tooltip,slot,divider,icon_coin,icon_potion_empty,icon_close}.png`
- Create: `_workspace/2026-09-25-ui-reskin/final/` 같은 이름 9장
- Modify: 생성 로그

**Interfaces:**
- Consumes: `REF_NODE`, `REF_STYLE`, `tools/trim_png.py`
- Produces: `final/` 9장 (Task 6이 반입). 최대 변: panel/card/tooltip/divider 512, chip 384, slot/icons 256.

- [x] **Step 1: 배치 제출 (9건, 고화질 투명)**

공통 `params`: `"model":"gpt_image_2_5","quality":"high","resolution":"2k","background":"transparent","medias":[{"value":"<REF_NODE>","role":"image_references"},{"value":"<REF_STYLE>","role":"image_references"}]`. 공통 접두 P = 생성 로그의 「프롬프트 공통 접두」 전문.

```
generate_image_batch(requests=[
 {"index":0,"params":{...공통,"aspect_ratio":"1:1","prompt": P + " Item: a large rounded rectangle panel, corner radius about 8% of its width, outline thickness about 3% of its width, tiny four-point star ornaments only at the top-left and top-right corners, completely empty center. Take the panel exactly as drawn in the reference style sheet."}},
 {"index":1,"params":{...공통,"aspect_ratio":"4:3","prompt": P + " Item: a content card, a smaller rounded rectangle with a tighter corner radius (about 5% of width), no ornaments, completely empty center. Take the card as drawn in the reference style sheet."}},
 {"index":2,"params":{...공통,"aspect_ratio":"21:9","prompt": P + " Item: a pill-shaped label chip, fully rounded ends, fill in a slightly deeper cream (#E8DBC3), thinner outline than the panel, empty center. Take the chip as drawn in the reference style sheet."}},
 {"index":3,"params":{...공통,"aspect_ratio":"4:3","prompt": P + " Item: a rectangular tooltip box with rounded corners, no tail, no pointer, outline slightly thinner than the panel, empty center. Take the tooltip as drawn in the reference style sheet."}},
 {"index":4,"params":{...공통,"aspect_ratio":"1:1","prompt": P + " Item: an empty inset square slot with rounded corners: a cream square whose inner area is a slightly deeper cream (#E8DBC3) with a subtle inner ring, suggesting a recessed socket for an item. Take the slot as drawn in the reference style sheet."}},
 {"index":5,"params":{...공통,"aspect_ratio":"21:9","prompt": P + " Item: a thin horizontal divider line in ink color (#4B425C) with rounded ends and a small four-point star at its center, the line spanning almost the full width. No fill, no box. Take the divider as drawn in the reference style sheet."}},
 {"index":6,"params":{...공통,"aspect_ratio":"1:1","prompt": P + " Item: a gold coin icon, round, pastel gold (#E6C15A) with a lighter inner disc, thick ink outline, no letters or symbols on the coin."}},
 {"index":7,"params":{...공통,"aspect_ratio":"1:1","prompt": P + " Item: an empty potion bottle icon, small round-bottom flask with a cork, drawn as an outline with a faint cream fill, no liquid inside, thick ink outline."}},
 {"index":8,"params":{...공통,"aspect_ratio":"1:1","prompt": P + " Item: a bold close X icon, two thick rounded strokes in ink color (#4B425C) crossing, no circle around it."}}
])
```
응답의 index→job_id 9쌍을 로그에 기록 (비용 9 × 2.75 = 24.75, 누적 ≈ 25.5).

- [x] **Step 2: 완료 대기 + 위젯 표시**

```
jobs_wait(jobs=[{index,job_id} ×9], timeout_seconds=15)   # all_terminal 될 때까지 반복
show_generation_by_ids(jobs=[{index,job_id} ×9])
```

- [x] **Step 3: 다운로드**

결과 URL마다 (index 순서 = panel, card, chip, tooltip, slot, divider, icon_coin, icon_potion_empty, icon_close):
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/candidates" && curl -sL -o panel.png "<url0>" && curl -sL -o card.png "<url1>" && curl -sL -o chip.png "<url2>" && curl -sL -o tooltip.png "<url3>" && curl -sL -o slot.png "<url4>" && curl -sL -o divider.png "<url5>" && curl -sL -o icon_coin.png "<url6>" && curl -sL -o icon_potion_empty.png "<url7>" && curl -sL -o icon_close.png "<url8>" && python -c "
from PIL import Image
for n in ['panel','card','chip','tooltip','slot','divider','icon_coin','icon_potion_empty','icon_close']:
    im = Image.open(n+'.png'); a = im.convert('RGBA').getchannel('A'); print(n, im.size, im.mode, 'alpha_min', a.getextrema()[0])"
```
Expected: 9줄, `mode RGBA`, `alpha_min 0` (투명 배경 확인). `alpha_min`이 255인 파일은 투명 실패 → 그 파일만 힉스필드 `remove_background(media_id=<job_id>, media_type="image")` 호출 후 결과를 다시 다운로드.

- [x] **Step 4: 사용자 승인**

`AskUserQuestion`: "9장 모두 승인 / 일부 재생성(메모에 번호·이유)". 재생성은 해당 index만 `generate_image_batch`로 재제출(프롬프트에 사용자 메모 반영), Step 2~4 반복. 재생성 1건 2.75. 누적 30 초과 시 `balance` 호출 후 사용자에게 잔액·누적 보고하고 계속 여부 확인.

- [x] **Step 5: 트리밍·리사이즈**

```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && for p in "panel 512" "card 512" "tooltip 512" "divider 512" "chip 384" "slot 256" "icon_coin 256" "icon_potion_empty 256" "icon_close 256"; do set -- $p; python tools/trim_png.py candidates/$1.png final/$1.png $2; done
```
Expected: 9줄 `final/<name>.png: (w, h)`, 최대 변이 지정값 + 8 이하.

- [x] **Step 6: 로그 커밋**

```bash
cd "D:/Dice Orbit" && git add Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md && git commit -q -m "docs(ui-skin): 코어 9종 생성 승인 — 생성 로그 갱신

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: 버튼 스트립 2종 생성 → 분할 → 승인 → 중간 보고

**Files:**
- Create: `candidates/button_primary_strip.png`, `candidates/button_secondary_strip.png`
- Create: `final/button_{primary,secondary}_{normal,hover,pressed,disabled}.png` (8장)
- Modify: 생성 로그

**Interfaces:**
- Consumes: `REF_NODE`, `REF_STYLE`, `tools/split_strip.py`
- Produces: `final/` 버튼 8장 (최대 변 384)

- [x] **Step 1: 배치 제출 (2건)**

```
generate_image_batch(requests=[
 {"index":0,"params":{...공통(Task 4와 동일),"aspect_ratio":"21:9","prompt": P + " Item: four states of the SAME pill button laid out in one horizontal row, left to right, equal size, with clear transparent gaps between them: (1) normal — pastel pink fill (#FFA6F2), thick ink outline, darker bottom edge line; (2) hover — same but slightly brighter fill and a thicker outline; (3) pressed — same shape shifted down slightly, bottom edge line gone, fill a touch darker; (4) disabled — desaturated gray paper fill (#D9D4CB), dashed ink outline, no bottom edge line. All four identical in width and height. No text on the buttons. Take the primary button row as drawn in the reference style sheet."}},
 {"index":1,"params":{...공통,"aspect_ratio":"21:9","prompt": P + " Item: four states of the SAME pill button laid out in one horizontal row, left to right, equal size, with clear transparent gaps between them: (1) normal — pastel sky blue fill (#A6DDFF), thick ink outline, darker bottom edge line; (2) hover — same but slightly brighter fill and a thicker outline; (3) pressed — same shape shifted down slightly, bottom edge line gone, fill a touch darker; (4) disabled — desaturated gray paper fill (#D9D4CB), dashed ink outline, no bottom edge line. All four identical in width and height. No text on the buttons. Take the secondary button row as drawn in the reference style sheet."}}
])
```
로그 기록 (비용 5.5, 누적 ≈ 31).

- [x] **Step 2: 대기·표시·다운로드**

```
jobs_wait(...) → show_generation_by_ids(...)
```
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/candidates" && curl -sL -o button_primary_strip.png "<url0>" && curl -sL -o button_secondary_strip.png "<url1>" && python -c "
from PIL import Image
for n in ['button_primary_strip','button_secondary_strip']:
    im = Image.open(n+'.png').convert('RGBA'); print(n, im.size, 'alpha_min', im.getchannel('A').getextrema()[0])"
```
Expected: 2줄, `alpha_min 0`.

- [x] **Step 3: 분할**

```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && python tools/split_strip.py candidates/button_primary_strip.png final/button_primary_ normal,hover,pressed,disabled 384 && python tools/split_strip.py candidates/button_secondary_strip.png final/button_secondary_ normal,hover,pressed,disabled 384
```
Expected: 8줄 `final/button_..._<state>.png: (w, h)`. 조각 수 오류가 나면 위젯의 이미지를 보고 간격이 없는 것이면 `--equal 4`를 붙여 재실행, 조각이 3개나 5개면 사용자 메모 없이 해당 스트립만 재생성(프롬프트에 "with wide clear gaps between the four buttons" 추가).

- [x] **Step 4: 컨택트 시트 + 사용자 승인**

```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && python tools/contact_sheet.py final final/_contact_sheet.png
```
`SendUserFile(files=["D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/final/_contact_sheet.png"], display="render")` 로 17장 시트를 보여주고 `AskUserQuestion`: "전부 승인 / 일부 재생성(메모)". 재생성은 스트립 단위(2.75).

- [x] **Step 5: 중간 보고 (누적 30 돌파)**

`balance()` 호출 → 로그 「누적 비용」표에 기입 → 사용자에게 한 줄 보고 ("누적 N, 잔액 M, 다음 단계는 생성 없음").

- [x] **Step 6: 로그 커밋**

```bash
cd "D:/Dice Orbit" && rm -f _workspace/2026-09-25-ui-reskin/final/_contact_sheet.png && git add Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md && git commit -q -m "docs(ui-skin): 버튼 스트립 2종 생성·분할 승인 — 생성 로그 갱신

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: 프로젝트 반입 + 임포터 설정

**Files:**
- Create: `Assets/Sprites/UI Skin/` + PNG 17장 (+ Unity가 만드는 .meta 17개)
- Test: `Unity_GetConsoleLogs(logTypes:"Error")` = 0, RunCommand 로그로 border 확인

**Interfaces:**
- Consumes: `final/` 17장
- Produces: 스프라이트 에셋 경로 `Assets/Sprites/UI Skin/<name>.png` (Task 9가 로드). 이름: `panel, card, chip, tooltip, slot, divider, button_primary_normal, button_primary_hover, button_primary_pressed, button_primary_disabled, button_secondary_normal, button_secondary_hover, button_secondary_pressed, button_secondary_disabled, icon_coin, icon_potion_empty, icon_close`

- [x] **Step 1: 복사**

```bash
cd "D:/Dice Orbit" && mkdir -p "Assets/Sprites/UI Skin" && cp _workspace/2026-09-25-ui-reskin/final/*.png "Assets/Sprites/UI Skin/" && ls "Assets/Sprites/UI Skin" | wc -l
```
Expected: `17` (style_tile.png은 final에 있으면 제외: `rm "Assets/Sprites/UI Skin/style_tile.png"` 후 다시 17 확인).

- [x] **Step 2: Unity 임포트 → 콘솔 확인**

```
Unity_RunCommand(Code: "using UnityEditor; internal class CommandScript : IRunCommand { public void Execute(ExecutionResult r) { AssetDatabase.Refresh(); r.Log(\"refreshed\"); } }")
Unity_GetConsoleLogs(logTypes: "Error", maxEntries: 20)
```
Expected: 에러 0건. `.meta` 17개 생성됨 (`ls "Assets/Sprites/UI Skin"/*.meta | wc -l` → 17).

- [x] **Step 3: 임포터 설정 RunCommand**

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    // (이름, 가로 경계 비율, 세로 경계 비율) — 경계 = 텍스처 크기 × 비율, 좌우/상하 대칭
    private static readonly (string name, float fx, float fy)[] Sliced =
    {
        ("panel", 0.30f, 0.30f), ("card", 0.30f, 0.30f), ("tooltip", 0.30f, 0.30f),
        ("chip", 0.40f, 0.45f), ("slot", 0.35f, 0.35f), ("divider", 0.20f, 0f),
        ("button_primary_normal", 0.40f, 0.45f), ("button_primary_hover", 0.40f, 0.45f),
        ("button_primary_pressed", 0.40f, 0.45f), ("button_primary_disabled", 0.40f, 0.45f),
        ("button_secondary_normal", 0.40f, 0.45f), ("button_secondary_hover", 0.40f, 0.45f),
        ("button_secondary_pressed", 0.40f, 0.45f), ("button_secondary_disabled", 0.40f, 0.45f),
    };
    private static readonly string[] Icons = { "icon_coin", "icon_potion_empty", "icon_close" };

    public void Execute(ExecutionResult result)
    {
        foreach (var (name, fx, fy) in Sliced) Configure(result, name, true, fx, fy);
        foreach (var name in Icons) Configure(result, name, false, 0f, 0f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        result.Log("임포터 설정 완료 — sliced {0}, icons {1}", Sliced.Length, Icons.Length);
    }

    private static void Configure(ExecutionResult result, string name, bool sliced, float fx, float fy)
    {
        string path = "Assets/Sprites/UI Skin/" + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { result.LogError("임포터 없음: " + path); return; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;

        // spriteMeshType은 TextureImporter 직접 속성이 아니라 TextureImporterSettings 경유 (실행 중 확인, 2026-09-25)
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = sliced ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        importer.SetTextureSettings(settings);

        if (sliced)
        {
            // LoadAssetAtPath<Texture2D>().width는 첫 임포트 직후 엉뚱한 값을 돌려준 적이 있다 (510 등).
            // 원본 픽셀 크기는 GetSourceTextureWidthAndHeight로 읽는다.
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            float bx = Mathf.Floor(w * fx), by = Mathf.Floor(h * fy);
            importer.spriteBorder = new Vector4(bx, by, bx, by);   // (left, bottom, right, top)
        }
        else importer.spriteBorder = Vector4.zero;
        importer.SaveAndReimport();
        result.Log("{0}: border={1} mip={2} filter={3} mesh={4}", name, importer.spriteBorder, importer.mipmapEnabled, importer.filterMode, settings.spriteMeshType);
    }
}
```
Expected: 17줄 로그, sliced 14개는 border 0 아님, `Unity_GetConsoleLogs(Error)` 0건.

- [x] **Step 4: 커밋 (LFS 포인터 확인)**

```bash
cd "D:/Dice Orbit" && git add "Assets/Sprites/UI Skin" && git -c core.quotepath=off status --short | head -40 && git lfs ls-files | grep -c "UI Skin"
```
Expected: 34개 A(png+meta), LFS 카운트 17.
```bash
cd "D:/Dice Orbit" && git commit -q -m "feat(ui-skin): 코어 스프라이트 17장 반입 — 9-slice·Mipmap·Trilinear 임포트 설정

패널/카드/칩/툴팁/슬롯/구분선 6, 버튼 2종×4상태 8, 아이콘 3.
힉스필드 gpt_image_2_5 생성 (로그: plans/2026-09-25-ui-reskin-generation-log.md).

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: UiSkin 코드 + 자가 테스트 (TDD)

**Files:**
- Create: `Assets/Scripts/Editor/UiSkinSelfTests.cs` (먼저)
- Create: `Assets/Scripts/UI/Skin/UiSkin.cs`

**Interfaces:**
- Produces (namespace `DiceOrbit.UI.Skin`):
  - `enum SkinPart { Panel, Card, Chip, Tooltip, Slot, Divider, ButtonPrimary, ButtonSecondary }`
  - `enum ButtonKind { Primary, Secondary }`
  - `[Serializable] class ButtonSpriteSet { Sprite Normal, Hover, Pressed, Disabled; }`
  - `class UiSkin : ScriptableObject` — `const string ResourcePath = "UI/UiSkin"`, `static UiSkin Current`, `static UiSkin LoadOrThrow(string)`, 팔레트 12 `Color` 필드, 스프라이트 6 필드, `ButtonSpriteSet ButtonPrimary/ButtonSecondary`, 아이콘 3 필드, `Sprite GetSprite(SkinPart)`, `ButtonSpriteSet GetButtonSet(ButtonKind)`, `void ApplyPanel/ApplyCard/ApplyChip/ApplyTooltip/ApplySlot/ApplyDivider(Image)`, `void ApplyButton(Button, ButtonKind)`
  - 테스트는 Task 8의 `UiSkinValidator.Validate(UiSkin) : List<string>`도 호출한다 — Task 8 완료 전에는 컴파일 에러가 정상.

- [x] **Step 1: 자가 테스트 먼저 작성**

`Assets/Scripts/Editor/UiSkinSelfTests.cs`:
```csharp
using System;
using DiceOrbit.UI.Skin;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// UiSkin 순수 로직 자가 테스트 (2026-09-25 리스킨 2단계). 씬·플레이 모드 불필요.
    /// 메뉴 [DiceOrbit → Run UiSkin Self-Tests] 또는 MCP RunCommand에서 RunAll() 호출.
    /// </summary>
    public static class UiSkinSelfTests
    {
        private static int _failures;

        [MenuItem("DiceOrbit/Run UiSkin Self-Tests")]
        public static void RunFromMenu() => RunAll();

        public static bool RunAll()
        {
            _failures = 0;

            TestLoadOrThrowOnMissingAsset();
            TestApplySlicedSetsSpriteTypeAndWhite();
            TestApplyThrowsWhenSpriteMissing();
            TestApplyButtonSetsSpriteSwap();
            TestGetSpriteCoversEveryPart();
            TestValidatorReportsEmptySkin();
            TestValidatorPassesCompleteSkin();

            if (_failures == 0) Debug.Log("[SelfTest] 전체 PASS — UiSkin");
            else Debug.LogError($"[SelfTest] 실패 {_failures}건 — 위 로그 확인");
            return _failures == 0;
        }

        private static void Check(bool condition, string label)
        {
            if (condition) return;
            _failures++;
            Debug.LogError("[SelfTest] FAIL: " + label);
        }

        private static Sprite MakeSprite(bool withBorder)
        {
            var tex = new Texture2D(8, 8);
            var border = withBorder ? new Vector4(2, 2, 2, 2) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static ButtonSpriteSet MakeSet() => new ButtonSpriteSet
        {
            Normal = MakeSprite(true), Hover = MakeSprite(true), Pressed = MakeSprite(true), Disabled = MakeSprite(true)
        };

        private static UiSkin MakeCompleteSkin()
        {
            var s = ScriptableObject.CreateInstance<UiSkin>();
            s.Panel = MakeSprite(true); s.Card = MakeSprite(true); s.Chip = MakeSprite(true);
            s.Tooltip = MakeSprite(true); s.Slot = MakeSprite(true); s.Divider = MakeSprite(true);
            s.ButtonPrimary = MakeSet(); s.ButtonSecondary = MakeSet();
            s.Coin = MakeSprite(false); s.PotionSlotEmpty = MakeSprite(false); s.Close = MakeSprite(false);
            return s;
        }

        private static void TestLoadOrThrowOnMissingAsset()
        {
            bool threw = false;
            try { UiSkin.LoadOrThrow("UI/DoesNotExist_UiSkin"); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, "없는 스킨 경로는 InvalidOperationException (폴백 없음)");
        }

        private static void TestApplySlicedSetsSpriteTypeAndWhite()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-test", typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = Color.red;
            skin.ApplyPanel(img);
            Check(img.sprite == skin.Panel, "ApplyPanel: 스프라이트 지정");
            Check(img.type == Image.Type.Sliced, "ApplyPanel: Image.Type.Sliced");
            Check(img.color == Color.white, "ApplyPanel: 색 = white (틴트 금지)");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyThrowsWhenSpriteMissing()
        {
            var skin = ScriptableObject.CreateInstance<UiSkin>();
            var go = new GameObject("skin-test", typeof(Image));
            bool threw = false;
            try { skin.ApplyCard(go.GetComponent<Image>()); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, "빈 Card로 ApplyCard는 InvalidOperationException (폴백 없음)");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyButtonSetsSpriteSwap()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-btn", typeof(Image), typeof(Button));
            var btn = go.GetComponent<Button>();
            skin.ApplyButton(btn, ButtonKind.Secondary);
            var set = skin.ButtonSecondary;
            Check(btn.transition == Selectable.Transition.SpriteSwap, "ApplyButton: transition = SpriteSwap");
            Check(btn.spriteState.highlightedSprite == set.Hover, "ApplyButton: highlighted = Hover");
            Check(btn.spriteState.pressedSprite == set.Pressed, "ApplyButton: pressed = Pressed");
            Check(btn.spriteState.disabledSprite == set.Disabled, "ApplyButton: disabled = Disabled");
            Check(btn.targetGraphic is Image tg && tg.sprite == set.Normal && tg.type == Image.Type.Sliced, "ApplyButton: targetGraphic = Normal, Sliced");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestGetSpriteCoversEveryPart()
        {
            var skin = MakeCompleteSkin();
            foreach (SkinPart part in Enum.GetValues(typeof(SkinPart)))
                Check(skin.GetSprite(part) != null, $"GetSprite({part}) != null");
            Check(skin.GetSprite(SkinPart.ButtonPrimary) == skin.ButtonPrimary.Normal, "GetSprite(ButtonPrimary) = Primary.Normal");
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestValidatorReportsEmptySkin()
        {
            var skin = ScriptableObject.CreateInstance<UiSkin>();
            var issues = UiSkinValidator.Validate(skin);
            Check(issues.Count == 17, $"빈 스킨 이슈 17건 (6 sliced + 8 button + 3 icon), 실제 {issues.Count}");
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestValidatorPassesCompleteSkin()
        {
            var skin = MakeCompleteSkin();
            var issues = UiSkinValidator.Validate(skin);
            Check(issues.Count == 0, "완전한 스킨 이슈 0 — " + string.Join(" / ", issues));
            UnityEngine.Object.DestroyImmediate(skin);
        }
    }
}
```

- [x] **Step 2: 컴파일 실패 확인**

```
Unity_RunCommand(Code: "using UnityEditor; internal class CommandScript : IRunCommand { public void Execute(ExecutionResult r) { AssetDatabase.Refresh(); r.Log(\"refreshed\"); } }")
Unity_GetConsoleLogs(logTypes: "Error", maxEntries: 10)
```
Expected: `CS0246: The type or namespace name 'UiSkin' could not be found` 류 에러 (아직 구현 전이므로 정상).

- [x] **Step 3: UiSkin.cs 구현**

`Assets/Scripts/UI/Skin/UiSkin.cs`:
```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI.Skin
{
    /// <summary>스킨 부품 이름 — Apply 헬퍼와 씬 배치용 UiSkinImage(리스킨 3단계)가 공유한다.</summary>
    public enum SkinPart { Panel, Card, Chip, Tooltip, Slot, Divider, ButtonPrimary, ButtonSecondary }

    public enum ButtonKind { Primary, Secondary }

    /// <summary>버튼 1종의 상태별 스프라이트. 틴트가 아니라 스프라이트 교체(SpriteSwap)로 상태를 표현한다.</summary>
    [Serializable]
    public class ButtonSpriteSet
    {
        public Sprite Normal;
        public Sprite Hover;
        public Sprite Pressed;
        public Sprite Disabled;
    }

    /// <summary>
    /// UI 스킨 단일 권위 (리스킨 스펙 2026-09-25 §3). 팔레트·9-slice·버튼 상태·아이콘을 한 에셋에 모은다.
    /// 코드 생성 UI는 색상 상수 대신 <see cref="Current"/>를 읽고, 씬 UI는 UiSkinImage로 파트를 지정한다.
    /// 에셋이 없거나 필드가 비면 예외 — 기본색·기본 스프라이트로 조용히 굴러가지 않는다.
    /// 점검: 「도구/Dice Orbit/UI 스킨 점검」.
    /// </summary>
    [CreateAssetMenu(fileName = "UiSkin", menuName = "Dice Orbit/UI/UI Skin")]
    public class UiSkin : ScriptableObject
    {
        public const string ResourcePath = "UI/UiSkin";
        private static UiSkin _current;

        /// <summary>Resources/UI/UiSkin.asset. 없으면 InvalidOperationException.</summary>
        public static UiSkin Current
        {
            get
            {
                if (_current == null) _current = LoadOrThrow(ResourcePath);
                return _current;
            }
        }

        public static UiSkin LoadOrThrow(string resourcePath)
        {
            var skin = Resources.Load<UiSkin>(resourcePath);
            if (skin == null)
                throw new InvalidOperationException(
                    $"[UiSkin] Resources/{resourcePath}.asset 이 없습니다. " +
                    "Create > Dice Orbit > UI > UI Skin 으로 만들어 Assets/Resources/UI/UiSkin.asset 에 두세요.");
            return skin;
        }

        [Header("팔레트")]
        public Color Paper     = new Color(0.980f, 0.953f, 0.878f);        // #FAF3E0 크림 종이
        public Color PaperDeep = new Color(0.910f, 0.859f, 0.765f);        // #E8DBC3 칩
        public Color Ink       = new Color(0.294f, 0.259f, 0.361f);        // #4B425C 잉크
        public Color InkMuted  = new Color(0.42f, 0.40f, 0.36f);           // 설명 회색
        public Color Accent    = new Color(0.79f, 0.61f, 0.25f);           // 골드
        public Color Primary   = new Color(1.00f, 0.65f, 0.95f);           // #FFA6F2 핑크
        public Color Secondary = new Color(0.65f, 0.87f, 1.00f);           // #A6DDFF 하늘
        public Color Danger    = new Color(0.773f, 0.227f, 0.227f);        // #C53A3A HP
        public Color Dice      = new Color(0.16f, 0.42f, 0.65f);           // 주사위 청색
        public Color Modifier  = new Color(0.42f, 0.28f, 0.72f);           // #6A48B8 보라
        public Color Passive   = new Color(0.514f, 0.624f, 0.557f);        // #839F8E 세이지
        public Color Scrim     = new Color(0.043f, 0.051f, 0.078f, 0.85f); // 어두운 반투명 배경

        [Header("9-slice 스프라이트")]
        public Sprite Panel;
        public Sprite Card;
        public Sprite Chip;
        public Sprite Tooltip;
        public Sprite Slot;
        public Sprite Divider;

        [Header("버튼 (상태별 스프라이트)")]
        public ButtonSpriteSet ButtonPrimary = new ButtonSpriteSet();
        public ButtonSpriteSet ButtonSecondary = new ButtonSpriteSet();

        [Header("아이콘")]
        public Sprite Coin;
        public Sprite PotionSlotEmpty;
        public Sprite Close;

        public Sprite GetSprite(SkinPart part) => part switch
        {
            SkinPart.Panel => Panel,
            SkinPart.Card => Card,
            SkinPart.Chip => Chip,
            SkinPart.Tooltip => Tooltip,
            SkinPart.Slot => Slot,
            SkinPart.Divider => Divider,
            SkinPart.ButtonPrimary => ButtonPrimary.Normal,
            SkinPart.ButtonSecondary => ButtonSecondary.Normal,
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, "알 수 없는 SkinPart"),
        };

        public ButtonSpriteSet GetButtonSet(ButtonKind kind)
            => kind == ButtonKind.Primary ? ButtonPrimary : ButtonSecondary;

        public void ApplyPanel(Image image)   => ApplySliced(image, Panel,   nameof(Panel));
        public void ApplyCard(Image image)    => ApplySliced(image, Card,    nameof(Card));
        public void ApplyChip(Image image)    => ApplySliced(image, Chip,    nameof(Chip));
        public void ApplyTooltip(Image image) => ApplySliced(image, Tooltip, nameof(Tooltip));
        public void ApplySlot(Image image)    => ApplySliced(image, Slot,    nameof(Slot));
        public void ApplyDivider(Image image) => ApplySliced(image, Divider, nameof(Divider));

        /// <summary>버튼에 상태별 스프라이트를 꽂는다 (SpriteSwap). targetGraphic이 없으면 같은 오브젝트의 Image를 쓴다.</summary>
        public void ApplyButton(Button button, ButtonKind kind)
        {
            if (button == null) throw new ArgumentNullException(nameof(button));
            var set = GetButtonSet(kind);
            string field = kind == ButtonKind.Primary ? nameof(ButtonPrimary) : nameof(ButtonSecondary);
            Require(set.Normal,   field + ".Normal");
            Require(set.Hover,    field + ".Hover");
            Require(set.Pressed,  field + ".Pressed");
            Require(set.Disabled, field + ".Disabled");

            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image == null)
                throw new InvalidOperationException($"[UiSkin] 버튼 '{button.name}'에 Image가 없어 스프라이트를 꽂을 수 없습니다.");

            ApplySliced(image, set.Normal, field + ".Normal");
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = set.Hover,
                selectedSprite = set.Hover,
                pressedSprite = set.Pressed,
                disabledSprite = set.Disabled,
            };
        }

        private static void ApplySliced(Image image, Sprite sprite, string field)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            Require(sprite, field);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;   // 스프라이트가 색을 가진다 — 틴트 금지
        }

        private static void Require(Sprite sprite, string field)
        {
            if (sprite == null)
                throw new InvalidOperationException(
                    $"[UiSkin] {field} 스프라이트가 비어 있습니다 — 「도구/Dice Orbit/UI 스킨 점검」으로 확인하세요.");
        }
    }
}
```

- [x] **Step 4: 컴파일 확인 (Validator 미구현 에러만 남아야 함)**

Refresh RunCommand + `Unity_GetConsoleLogs(Error)`.
Expected: 남은 에러는 `UiSkinValidator` 관련 CS0103/CS0246 뿐. `UiSkin` 관련 에러 0.

- [x] **Step 5: 커밋 (테스트 + 구현)**

```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/UI/Skin Assets/Scripts/Editor/UiSkinSelfTests.cs Assets/Scripts/Editor/UiSkinSelfTests.cs.meta Assets/Scripts/UI/Skin.meta && git commit -q -m "feat(ui-skin): UiSkin ScriptableObject + Apply 헬퍼 + 자가 테스트

팔레트 12색, 9-slice 6, 버튼 2종×4상태(SpriteSwap), 아이콘 3.
Resources/UI/UiSkin 없거나 필드가 비면 예외 — 폴백 없음.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```
(`.meta`가 아직 없으면 Refresh 후 다시 add.)

---

### Task 8: UiSkinValidator (순수 Validate + 점검 메뉴)

**Files:**
- Create: `Assets/Scripts/Editor/UiSkinValidator.cs`
- Test: Task 7의 `TestValidatorReportsEmptySkin`, `TestValidatorPassesCompleteSkin`

**Interfaces:**
- Consumes: `UiSkin`, `ButtonSpriteSet`
- Produces: `static List<string> UiSkinValidator.Validate(UiSkin skin)`, 메뉴 「도구/Dice Orbit/UI 스킨 점검」

- [x] **Step 1: 구현**

`Assets/Scripts/Editor/UiSkinValidator.cs`:
```csharp
#if UNITY_EDITOR
using System.Collections.Generic;
using DiceOrbit.UI.Skin;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// UiSkin 에셋 점검 — 빈 필드, Sliced 부품의 9-slice 경계 0, 텍스처 타입이 Sprite가 아닌 경우를 에러로 나열한다.
    /// 메뉴 「도구/Dice Orbit/UI 스킨 점검」. 순수 부분(Validate)은 자가 테스트 대상.
    /// </summary>
    public static class UiSkinValidator
    {
        [MenuItem("도구/Dice Orbit/UI 스킨 점검")]
        public static void ValidateFromMenu()
        {
            UiSkin skin;
            try { skin = UiSkin.LoadOrThrow(UiSkin.ResourcePath); }
            catch (System.InvalidOperationException e) { Debug.LogError(e.Message); return; }

            var issues = Validate(skin);
            foreach (var issue in issues) Debug.LogError("[UiSkin] " + issue);
            if (issues.Count == 0) Debug.Log("[UiSkin] 점검 완료 — 이슈 0");
            else Debug.LogError($"[UiSkin] 이슈 {issues.Count}건 — 위 로그 확인");
        }

        /// <summary>이슈 목록을 돌려준다. 비어 있으면 정상.</summary>
        public static List<string> Validate(UiSkin skin)
        {
            var issues = new List<string>();
            CheckSliced(issues, skin.Panel,   nameof(skin.Panel));
            CheckSliced(issues, skin.Card,    nameof(skin.Card));
            CheckSliced(issues, skin.Chip,    nameof(skin.Chip));
            CheckSliced(issues, skin.Tooltip, nameof(skin.Tooltip));
            CheckSliced(issues, skin.Slot,    nameof(skin.Slot));
            CheckSliced(issues, skin.Divider, nameof(skin.Divider));
            CheckButtonSet(issues, skin.ButtonPrimary,   nameof(skin.ButtonPrimary));
            CheckButtonSet(issues, skin.ButtonSecondary, nameof(skin.ButtonSecondary));
            CheckPresent(issues, skin.Coin,            nameof(skin.Coin));
            CheckPresent(issues, skin.PotionSlotEmpty, nameof(skin.PotionSlotEmpty));
            CheckPresent(issues, skin.Close,           nameof(skin.Close));
            return issues;
        }

        private static void CheckButtonSet(List<string> issues, ButtonSpriteSet set, string field)
        {
            if (set == null) { issues.Add($"{field}: 세트가 null"); return; }
            CheckSliced(issues, set.Normal,   field + ".Normal");
            CheckSliced(issues, set.Hover,    field + ".Hover");
            CheckSliced(issues, set.Pressed,  field + ".Pressed");
            CheckSliced(issues, set.Disabled, field + ".Disabled");
        }

        private static void CheckSliced(List<string> issues, Sprite sprite, string field)
        {
            if (!CheckPresent(issues, sprite, field)) return;
            if (sprite.border == Vector4.zero)
                issues.Add($"{field}: 9-slice 경계가 0 — 임포터 Sprite Border를 설정하세요 ({AssetDatabase.GetAssetPath(sprite)})");
        }

        private static bool CheckPresent(List<string> issues, Sprite sprite, string field)
        {
            if (sprite == null) { issues.Add($"{field}: 비어 있음"); return false; }
            var path = AssetDatabase.GetAssetPath(sprite);
            if (!string.IsNullOrEmpty(path)
                && AssetImporter.GetAtPath(path) is TextureImporter importer
                && importer.textureType != TextureImporterType.Sprite)
                issues.Add($"{field}: 텍스처 타입이 Sprite가 아님 ({path})");
            return true;
        }
    }
}
#endif
```

- [x] **Step 2: 컴파일 → 자가 테스트 실행**

Refresh RunCommand → `Unity_GetConsoleLogs(Error)` = 0건. 그 다음:
```
Unity_RunCommand(Code: "internal class CommandScript : IRunCommand { public void Execute(ExecutionResult r) { bool ok = DiceOrbit.EditorTools.UiSkinSelfTests.RunAll(); r.Log(\"UiSkin self-tests: {0}\", ok ? \"PASS\" : \"FAIL\"); } }")
```
Expected: 로그 `[SelfTest] 전체 PASS — UiSkin`, `UiSkin self-tests: PASS`. FAIL이면 FAIL 라벨을 보고 구현을 고친다 (테스트를 고치지 않는다).

- [x] **Step 3: 커밋**

```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/Editor/UiSkinValidator.cs Assets/Scripts/Editor/UiSkinValidator.cs.meta && git commit -q -m "feat(ui-skin): UI 스킨 점검 메뉴 — 빈 필드·9-slice 경계 0·텍스처 타입 검사

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: UiSkin.asset 생성·배선 → 점검 0건

**Files:**
- Create: `Assets/Resources/UI/UiSkin.asset` (+ .meta)
- Test: 점검 메뉴 0건, 자가 테스트 PASS, 콘솔 에러 0

**Interfaces:**
- Consumes: `Assets/Sprites/UI Skin/*.png` (Task 6), `UiSkin` (Task 7)
- Produces: `UiSkin.Current`가 로드 가능한 에셋 (3단계 마이그레이션의 전제)

- [x] **Step 1: 에셋 생성·배선 RunCommand**

```csharp
using UnityEngine;
using UnityEditor;
using DiceOrbit.UI.Skin;

internal class CommandScript : IRunCommand
{
    private const string Dir = "Assets/Sprites/UI Skin/";
    private const string AssetPath = "Assets/Resources/UI/UiSkin.asset";

    public void Execute(ExecutionResult result)
    {
        var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(AssetPath);
        if (skin == null)
        {
            skin = ScriptableObject.CreateInstance<UiSkin>();
            AssetDatabase.CreateAsset(skin, AssetPath);
            result.RegisterObjectCreation(skin);
        }
        else result.RegisterObjectModification(skin);

        skin.Panel   = Load(result, "panel");
        skin.Card    = Load(result, "card");
        skin.Chip    = Load(result, "chip");
        skin.Tooltip = Load(result, "tooltip");
        skin.Slot    = Load(result, "slot");
        skin.Divider = Load(result, "divider");
        skin.ButtonPrimary = new ButtonSpriteSet
        {
            Normal = Load(result, "button_primary_normal"), Hover = Load(result, "button_primary_hover"),
            Pressed = Load(result, "button_primary_pressed"), Disabled = Load(result, "button_primary_disabled"),
        };
        skin.ButtonSecondary = new ButtonSpriteSet
        {
            Normal = Load(result, "button_secondary_normal"), Hover = Load(result, "button_secondary_hover"),
            Pressed = Load(result, "button_secondary_pressed"), Disabled = Load(result, "button_secondary_disabled"),
        };
        skin.Coin = Load(result, "icon_coin");
        skin.PotionSlotEmpty = Load(result, "icon_potion_empty");
        skin.Close = Load(result, "icon_close");

        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        result.Log("UiSkin.asset 배선 완료: {0}", skin);
    }

    private static Sprite Load(ExecutionResult result, string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + name + ".png");
        if (sprite == null) result.LogError("스프라이트 없음: " + Dir + name + ".png");
        return sprite;
    }
}
```
Expected: `UiSkin.asset 배선 완료`, LogError 0건. (팔레트는 클래스 기본값이 그대로 저장된다.)

- [x] **Step 2: 점검 메뉴 실행**

```
Unity_RunCommand(Code: "internal class CommandScript : IRunCommand { public void Execute(ExecutionResult r) { DiceOrbit.EditorTools.UiSkinValidator.ValidateFromMenu(); r.Log(\"validated\"); } }")
Unity_GetConsoleLogs(logTypes: "Error", maxEntries: 30)
```
Expected: `[UiSkin] 점검 완료 — 이슈 0`, 에러 0건. 이슈가 있으면 Task 6 Step 3의 비율표를 고치고 재실행.

- [x] **Step 3: Current 로드 확인**

```
Unity_RunCommand(Code: "internal class CommandScript : IRunCommand { public void Execute(ExecutionResult r) { var s = DiceOrbit.UI.Skin.UiSkin.Current; r.Log(\"Current={0} panel={1} border={2}\", s, s.Panel, s.Panel.border); } }")
```
Expected: `Current=UiSkin (...) panel=panel border=(153.0, 153.0, 153.0, 153.0)` 류 (0이 아닌 경계).

- [x] **Step 4: 커밋**

```bash
cd "D:/Dice Orbit" && git add Assets/Resources/UI/UiSkin.asset Assets/Resources/UI/UiSkin.asset.meta && git commit -q -m "feat(ui-skin): Resources/UI/UiSkin.asset 생성·배선 — 점검 0건

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: 문서 갱신 + 마무리

**Files:**
- Modify: `Docs/README.md` (설계안·구현 계획 표에 3행 추가)
- Modify: `Docs/editor_owned_ui_pattern.md` (체크리스트 4·5 아래 1줄)
- Modify: `Docs/superpowers/specs/2026-09-25-ui-reskin-uiskin-higgsfield-design.md` (상태 줄)

- [x] **Step 1: Docs/README.md 표에 추가**

「📐 설계안 · 구현 계획 (기록)」 표 마지막 행 뒤에:
```markdown
| [superpowers/specs/2026-09-25-ui-reskin-uiskin-higgsfield-design.md](superpowers/specs/2026-09-25-ui-reskin-uiskin-higgsfield-design.md) | **UI 통일 리스킨 설계** — 크림 종이 + 굵은 외곽선, UiSkin 시스템, 힉스필드 생성 | 진행 중 (1·2단계 완료) |
| [superpowers/plans/2026-09-25-ui-reskin-phase1-2-style-tile-core-skin.md](superpowers/plans/2026-09-25-ui-reskin-phase1-2-style-tile-core-skin.md) | 스타일 타일 + 코어 세트 + UiSkin **구현 계획** | 완료 |
| [superpowers/plans/2026-09-25-ui-reskin-generation-log.md](superpowers/plans/2026-09-25-ui-reskin-generation-log.md) | 힉스필드 생성 로그 (프롬프트·job id·비용) | 기록 |
```

- [x] **Step 2: editor_owned_ui_pattern.md 체크리스트 갱신**

「## 새 UI 만들 때 체크리스트」의 4·5번을 다음으로 교체:
```markdown
4. 색·스프라이트는 `UiSkin.Current` (`Assets/Scripts/UI/Skin/UiSkin.cs`, 에셋 `Resources/UI/UiSkin.asset`) — 팔레트 상수를 파일에 복사하지 않는다. 패널/카드/칩/툴팁/슬롯/구분선은 `ApplyPanel/ApplyCard/...`, 버튼은 `ApplyButton(btn, ButtonKind)` (SpriteSwap)
5. 라운드 사각형 절차 생성(`UiRoundedSprite.Get`)은 리스킨 3단계에서 철거 예정 — 신규 UI에서 쓰지 않는다. 점검: 「도구/Dice Orbit/UI 스킨 점검」
```

- [x] **Step 3: 스펙 상태 갱신**

스펙 상단 `> 상태: **설계 승인, 미착수**` → `> 상태: **1·2단계 완료 (스타일 타일·코어 세트·UiSkin), 3단계 대기**`

- [x] **Step 4: 최종 검증 + 커밋**

```
Unity_GetConsoleLogs(logTypes: "Error", maxEntries: 10)   → 0건
Unity_RunCommand(... UiSkinSelfTests.RunAll ...)           → PASS
```
```bash
cd "D:/Dice Orbit" && git add Docs/README.md Docs/editor_owned_ui_pattern.md Docs/superpowers/specs/2026-09-25-ui-reskin-uiskin-higgsfield-design.md Docs/superpowers/plans/2026-09-25-ui-reskin-phase1-2-style-tile-core-skin.md && git commit -q -m "docs(ui-skin): 리스킨 1·2단계 문서 갱신 — README 인덱스·에디터 소유 UI 체크리스트·스펙 상태

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>" && git log --oneline -8
```

- [x] **Step 5: 완료 보고**

사용자에게: 브랜치 `feature/ui-skin-core-20260925`, 커밋 목록, 누적 크레딧·잔액, 3단계(코드 UI 13파일 마이그레이션) 계획 작성 여부.
