# UI 리스킨 4단계 — 씬 드롭인 스프라이트 교체 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 씬·프리팹이 직접 참조하는 구 UI 스프라이트 19개 파일을 **같은 파일명으로 덮어써**(GUID 유지, 재배선 없음) 전투 HUD·메인메뉴·모집 화면 버튼·상점 배경을 크림 종이 스킨으로 바꾼다.

**Architecture:** 코어 세트(`Assets/Sprites/UI Skin/`)를 원본 파일 크기로 9-slice 렌더(패널·버튼), 작은 도형(HP바·주사위 면·하단 바)은 PIL로 스킨 팔레트대로 직접 그리고, 글자가 박힌 버튼(메인메뉴 4·선택/취소 2)은 알약 렌더 위에 ONE Mobile POP 폰트로 잉크색 글자를 합성한다. 힉스필드는 상점 배경 1장만 생성한다. 덮어쓴 뒤 Unity 임포터의 9-slice 경계를 새 그림에 맞게 RunCommand로 재설정한다.

**Tech Stack:** Python 3.11 + PIL (9-slice 렌더·도형·텍스트), 힉스필드 MCP (`generate_image_batch`/`jobs_wait`), Unity MCP (`Unity_RunCommand`/`Unity_GetConsoleLogs`/`Unity_ManageEditor`).

**스펙:** `Docs/superpowers/specs/2026-09-25-ui-reskin-uiskin-higgsfield-design.md` §4.2, §5, §6(4단계). 조사(2026-09-25)로 §4.2 표에서 달라진 점: 턴 배너 2종은 "Player Turn" 일러스트라 **제외**, HP바 3종·주사위 면·Rectangle 41은 힉스필드 대신 **PIL 도형**, 메인메뉴 4종은 배경(Rectangle 16)과 글자 이미지가 분리돼 있어 **글자 합성**, `new act 2/3`는 이동/취소 버튼 배경(글자는 TMP 자식)이라 **버튼 렌더**, `new cardNormal`은 타일 그림이라 **제외**.

## Global Constraints

- 브랜치: `feature/ui-skin-migration-20260925`에서 `feature/ui-skin-dropin-20260925`를 딴다.
- **같은 파일명 덮어쓰기**로 GUID를 유지한다. 씬·프리팹은 편집하지 않는다. `.meta`는 Unity 임포터 API로만 만진다(9-slice 경계·Mipmap·Trilinear).
- 팔레트 값은 스킨과 동일: Paper `#FAF3E0`, PaperDeep `#E8DBC3`, Ink `#4B425C`, Danger `#C53A3A`, Primary `#FFA6F2`, Secondary `#A6DDFF`.
- 글자 합성 폰트: `Assets/Fonts/ONE Mobile POP OTF.otf` (한글·영문 지원, 둥근 팝체). 색 = Ink.
- 힉스필드: 모델 `gpt_image_2_5`, `quality: "high", resolution: "2k"`, 상점 배경은 **opaque**(투명 아님), aspect `3:2`. 참조 = REF_NODE `555522a5-a336-4bad-bfba-a103ae1d7999`, REF_STYLE `5d029aa6-d2ec-4fd7-9a41-ef6e157d2ffe`, REF_TITLE `7650ffb6-0a1d-4957-bfdf-2349d520d66c` (2026-09-25 업로드, 24h 만료 — 만료됐으면 `refs/`에서 재업로드). 비용 2.75/장, 누적 상한 60(현재 31, 잔액 49).
- 승인 규칙: 컨택트 시트로 사용자 승인 후에만 `Assets/`에 복사.
- 검증: 복사 후 `Unity_GetConsoleLogs(Error)` 0건, 자가 테스트 PASS, Play 모드에서 모집 화면(선택/취소·HP바·로스터 프레임이 보임) 캡처.
- 작업 폴더: `_workspace/2026-09-25-ui-reskin/` (gitignore). 렌더 결과는 `dropin/<원본 상대경로와 같은 파일명>`.
- 커밋 메시지 끝: `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`

## 드롭인 매핑 (조사 결과)

| 원본 파일 | 크기 | 씬 용도 (Image 타입) | 만드는 법 | 새 9-slice 경계 (l,b,r,t) |
|---|---|---|---|---|
| `UI 이미지/주사위 패널.png` | 1000×175 | DicePanel(Sliced), RunHud TopBar(Simple 380×66), DiceHoverTooltip 패널(Sliced), TileCard(Sliced) | `panel.png` 네이티브 9-slice 렌더 | 156,78,156,78 |
| `UI 이미지/오른쪽 패널.png` | 410×1040 | InfoPanelCanvas/Panel(Simple), Event RightColumn(Simple) | `panel.png` 네이티브 9-slice 렌더 | 156,78,156,78 |
| `UI 이미지/패널 1차.png` | 1007×482 | TooltipCanvas/MainPanel(Sliced), GlossaryCard(Sliced) | `card.png` 네이티브 9-slice 렌더 | 156,87,156,87 |
| `UI 이미지/Rectangle 16.png` | 632×149 | 메인메뉴 버튼 4개 배경(Simple, ColorTint) | `button_secondary_normal.png` 높이맞춤 렌더 | 스크립트 출력값 |
| `UI 이미지/dice button.png` | 731×213 | Roll Dice·End Turn(Simple, TMP 자식) | `button_primary_normal.png` 높이맞춤 렌더 | 〃 |
| `UI 이미지/new act 2.png` | 325×152 | CharacterActionPanel/MoveButton(Simple, TMP 자식) | `button_primary_normal.png` 높이맞춤 렌더 | 〃 |
| `UI 이미지/new act3.png` | 379×149 | CharacterActionPanel/CancelButton(Simple, TMP 자식) | `button_secondary_normal.png` 높이맞춤 렌더 | 〃 |
| `UI 이미지/파티 로스터 패널.png` | 140×140 | PartyRosterEntry 루트(Simple) | `slot.png` 네이티브 9-slice 렌더 | 90,92,90,92 |
| `UI 이미지/선택.png` / `취소.png` | 268×102 | 모집 SelectButton/CancelButton(Simple, 글자 박힘) | Primary/Secondary 높이맞춤 렌더 + "선택"/"취소" 합성 | 스크립트 출력값 |
| `UI 이미지/Start Game.png` 394×67, `Continue.png` 309×67, `Settings.png` 286×80, `Quit.png` 154×81 | — | 메인메뉴 버튼 글자 이미지(Simple, 자식) | 투명 배경에 잉크 글자만 (폰트 합성) | 0 |
| `Info UI 이미지/체력바.png` | 153×18 | HP 트랙 3곳(Simple) | PIL: Paper 알약 + Ink 2px 외곽선 | 0 |
| `Info UI 이미지/체력_피.png` | 149×10 | HP 채움 2곳(Simple) | PIL: Danger 알약 | 0 |
| `Info UI 이미지/hp.png` | 255×23 | 캐릭터 HP 채움(Simple) | PIL: Danger 알약 + 상단 하이라이트 | 0 |
| `Sprites/Dice.png` | 266×266 | DicePrefab 면(Simple, TMP 숫자) + 툴팁 면 | PIL: Paper 둥근 사각 + Ink 12px 외곽선 + PaperDeep 안쪽 링 | 0 |
| `Sprites/Rectangle 41.png` | 1920×188 | DicePanel 하단 바(Simple 1920×150) | PIL: Paper 바 + 상단 Ink 8px 선 | 0 |
| `상점/상점 임시배경.png` | 1536×1024 | ShopCanvas/Background + ShopUI.backgroundSprite | **힉스필드 1장** → 1536×1024 리사이즈 | 0 |

제외(유지): `플레이어턴_푸른색.png`·`몬스터턴_붉은색.png`(일러스트), `Group 5 (1).png`(모집 설명 패널), `new cardNormal.png`(타일 그림), `상점캐.png`(상인 캐릭터).

---

### Task 1: 렌더 도구 3종 + 자체 검증

**Files:**
- Create: `_workspace/2026-09-25-ui-reskin/tools/render_slice.py`
- Create: `_workspace/2026-09-25-ui-reskin/tools/draw_shapes.py`
- Create: `_workspace/2026-09-25-ui-reskin/tools/text_label.py`

**Interfaces:**
- `python tools/render_slice.py <src.png> <fx> <fy> <W> <H> <out.png> [--fit-height]` → out.png (W×H) 저장, stdout에 `borders l,b,r,t` 출력
- `python tools/draw_shapes.py <out_dir>` → `체력바.png`, `체력_피.png`, `hp.png`, `Dice.png`, `Rectangle 41.png` 생성
- `python tools/text_label.py <text> <W> <H> <out.png> [--on <pill.png>]` → 투명 배경 글자 이미지, `--on`이면 알약 위에 합성

- [x] **Step 1: 브랜치**

```bash
cd "D:/Dice Orbit" && git checkout -q feature/ui-skin-migration-20260925 && git checkout -q -b feature/ui-skin-dropin-20260925 && git branch --show-current && mkdir -p _workspace/2026-09-25-ui-reskin/dropin
```

- [x] **Step 2: render_slice.py**

```python
"""9-slice 렌더. 네이티브 모드 = 모서리 원본 크기 유지(Unity Sliced와 동일, 축별로 경계 합이 크면 축소).
--fit-height = 원본을 높이에 맞춰 균일 축소 후 가로만 9-slice (알약/버튼용, 외곽선 비례 유지)."""
import sys
from PIL import Image


def slice_render(src: Image.Image, l: int, b: int, r: int, t: int, W: int, H: int) -> Image.Image:
    sw, sh = src.size
    # Unity GetAdjustedBorders: 축별로 경계 합이 목표보다 크면 그 축만 비례 축소
    if l + r > W:
        k = W / (l + r); l, r = int(l * k), int(r * k)
    if t + b > H:
        k = H / (t + b); t, b = int(t * k), int(b * k)
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    xs_src = [0, l, sw - r, sw]; ys_src = [0, t, sh - b, sh]   # PIL 좌표: 위가 0 → t가 위쪽
    xs_dst = [0, l, W - r, W];   ys_dst = [0, t, H - b, H]
    for i in range(3):
        for j in range(3):
            box = (xs_src[i], ys_src[j], xs_src[i + 1], ys_src[j + 1])
            dw, dh = xs_dst[i + 1] - xs_dst[i], ys_dst[j + 1] - ys_dst[j]
            if box[2] <= box[0] or box[3] <= box[1] or dw <= 0 or dh <= 0:
                continue
            piece = src.crop(box).resize((dw, dh), Image.LANCZOS)
            out.paste(piece, (xs_dst[i], ys_dst[j]), piece)
    return out


def main() -> None:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    fit_height = "--fit-height" in sys.argv
    if len(args) != 6:
        raise SystemExit("사용법: render_slice.py <src> <fx> <fy> <W> <H> <out> [--fit-height]")
    src_path, fx, fy, W, H, out = args[0], float(args[1]), float(args[2]), int(args[3]), int(args[4]), args[5]
    src = Image.open(src_path).convert("RGBA")
    if fit_height:
        k = H / src.height
        src = src.resize((max(1, round(src.width * k)), H), Image.LANCZOS)
    sw, sh = src.size
    l = r = int(sw * fx); t = b = int(sh * fy)
    img = slice_render(src, l, b, r, t, W, H)
    img.save(out)
    # 저장된 그림에서의 실제 경계(축소 반영)
    if l + r > W: k = W / (l + r); l, r = int(l * k), int(r * k)
    if t + b > H: k = H / (t + b); t, b = int(t * k), int(b * k)
    print(f"{out}: {img.size} borders {l},{b},{r},{t}")


if __name__ == "__main__":
    main()
```

- [x] **Step 3: draw_shapes.py**

```python
"""HP바·주사위 면·하단 바를 스킨 팔레트로 직접 그린다 (4배 슈퍼샘플 → LANCZOS 축소)."""
import os, sys
from PIL import Image, ImageDraw

PAPER, PAPER_DEEP, INK, DANGER = (250, 243, 224, 255), (232, 219, 195, 255), (75, 66, 92, 255), (197, 58, 58, 255)
SS = 4


def canvas(w, h): return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
def down(im, w, h): return im.resize((w, h), Image.LANCZOS)


def pill(w, h, fill, outline=None, ow=0, inset=0):
    im = canvas(w, h); d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = inset * SS, inset * SS, w * SS - 1 - inset * SS, h * SS - 1 - inset * SS
    d.rounded_rectangle([x0, y0, x1, y1], radius=(y1 - y0) // 2, fill=fill, outline=outline, width=ow * SS if outline else 0)
    return down(im, w, h)


def main() -> None:
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    # HP 트랙 153×18: 크림 알약 + 잉크 2px
    pill(153, 18, PAPER, INK, 2).save(os.path.join(out, "체력바.png"))
    # HP 채움 149×10 / 255×23: 적색 알약 (+ 큰 쪽은 상단 하이라이트)
    pill(149, 10, DANGER).save(os.path.join(out, "체력_피.png"))
    hp = pill(255, 23, DANGER)
    hl = canvas(255, 23); ImageDraw.Draw(hl).rounded_rectangle([6 * SS, 3 * SS, 249 * SS, 9 * SS], radius=3 * SS, fill=(255, 255, 255, 70))
    hp = Image.alpha_composite(hp, down(hl, 255, 23)); hp.save(os.path.join(out, "hp.png"))
    # 주사위 면 266×266: 크림 둥근 사각 + 잉크 12px + 안쪽 PaperDeep 링 6px
    im = canvas(266, 266); d = ImageDraw.Draw(im)
    d.rounded_rectangle([6 * SS, 6 * SS, 259 * SS, 259 * SS], radius=40 * SS, fill=PAPER, outline=INK, width=12 * SS)
    d.rounded_rectangle([30 * SS, 30 * SS, 235 * SS, 235 * SS], radius=26 * SS, outline=PAPER_DEEP, width=6 * SS)
    down(im, 266, 266).save(os.path.join(out, "Dice.png"))
    # 하단 바 1920×188: 크림 면 + 상단 잉크 8px + 그 아래 PaperDeep 4px
    im = canvas(1920, 188); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 1920 * SS, 188 * SS], fill=PAPER)
    d.rectangle([0, 0, 1920 * SS, 8 * SS], fill=INK)
    d.rectangle([0, 8 * SS, 1920 * SS, 12 * SS], fill=PAPER_DEEP)
    down(im, 1920, 188).save(os.path.join(out, "Rectangle 41.png"))
    for n in ["체력바.png", "체력_피.png", "hp.png", "Dice.png", "Rectangle 41.png"]:
        print(n, Image.open(os.path.join(out, n)).size)


if __name__ == "__main__":
    main()
```

- [x] **Step 4: text_label.py**

```python
"""투명 배경 잉크 글자 이미지. --on <pill.png> 이면 알약 위 중앙에 합성해 같은 크기로 저장."""
import sys
from PIL import Image, ImageDraw, ImageFont

FONT = "D:/Dice Orbit/Assets/Fonts/ONE Mobile POP OTF.otf"
INK = (75, 66, 92, 255)


def render(text: str, W: int, H: int, on: str | None) -> Image.Image:
    base = Image.open(on).convert("RGBA").resize((W, H), Image.LANCZOS) if on else Image.new("RGBA", (W, H), (0, 0, 0, 0))
    size = int(H * (0.52 if on else 0.78))
    font = ImageFont.truetype(FONT, size)
    d = ImageDraw.Draw(base)
    bbox = d.textbbox((0, 0), text, font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    while tw > W * 0.86 and size > 8:   # 폭 넘치면 줄인다
        size -= 2; font = ImageFont.truetype(FONT, size)
        bbox = d.textbbox((0, 0), text, font=font); tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    x = (W - tw) / 2 - bbox[0]
    y = (H - th) / 2 - bbox[1] - (H * 0.04 if on else 0)   # 알약은 아래 두께선 때문에 살짝 위로
    d.text((x, y), text, font=font, fill=INK)
    return base


def main() -> None:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    on = sys.argv[sys.argv.index("--on") + 1] if "--on" in sys.argv else None
    if on: args = [a for a in args if a != on]
    if len(args) != 4:
        raise SystemExit("사용법: text_label.py <text> <W> <H> <out> [--on <pill.png>]")
    text, W, H, out = args[0], int(args[1]), int(args[2]), args[3]
    img = render(text, W, H, on); img.save(out); print(f"{out}: {img.size}")


if __name__ == "__main__":
    main()
```

- [x] **Step 5: 자체 검증**

```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && python tools/render_slice.py "../../Assets/Sprites/UI Skin/panel.png" 0.30 0.30 1000 175 dropin/_t_panel.png && python tools/render_slice.py "../../Assets/Sprites/UI Skin/button_primary_normal.png" 0.40 0.45 268 102 dropin/_t_pill.png --fit-height && python tools/draw_shapes.py dropin/_t_shapes && python tools/text_label.py "선택" 268 102 dropin/_t_select.png --on dropin/_t_pill.png && python tools/text_label.py "Start Game" 394 67 dropin/_t_start.png && python -c "
from PIL import Image
for p in ['dropin/_t_panel.png','dropin/_t_pill.png','dropin/_t_select.png','dropin/_t_start.png']:
    im=Image.open(p).convert('RGBA'); print(p, im.size, 'alpha_max', im.getchannel('A').getextrema()[1])"
```
Expected: 크기 일치(1000×175, 268×102, 268×102, 394×67), `alpha_max 255`, `borders` 출력, 도형 5개 크기 출력. `Read`로 `_t_select.png`·`_t_start.png` 열어 글자가 잉크색으로 중앙에 찍혔는지 확인 후 `rm dropin/_t_*` `rm -r dropin/_t_shapes`.

---

### Task 2: 코어 렌더 드롭인 12장 + PIL 도형 5장 + 글자 6장

**Files:**
- Create: `_workspace/2026-09-25-ui-reskin/dropin/*.png` (원본 파일명 그대로, 23장)
- Create: `_workspace/2026-09-25-ui-reskin/dropin/borders.txt` (render_slice 출력 모음)

- [x] **Step 1: 패널·슬롯 (네이티브 9-slice)**

```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && S="../../Assets/Sprites/UI Skin" && : > dropin/borders.txt && \
python tools/render_slice.py "$S/panel.png" 0.30 0.30 1000 175 "dropin/주사위 패널.png" | tee -a dropin/borders.txt && \
python tools/render_slice.py "$S/panel.png" 0.30 0.30 410 1040 "dropin/오른쪽 패널.png" | tee -a dropin/borders.txt && \
python tools/render_slice.py "$S/card.png" 0.30 0.30 1007 482 "dropin/패널 1차.png" | tee -a dropin/borders.txt && \
python tools/render_slice.py "$S/slot.png" 0.35 0.35 140 140 "dropin/파티 로스터 패널.png" | tee -a dropin/borders.txt
```

- [x] **Step 2: 버튼 배경 (높이맞춤)**

```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && S="../../Assets/Sprites/UI Skin" && \
python tools/render_slice.py "$S/button_secondary_normal.png" 0.40 0.45 632 149 "dropin/Rectangle 16.png" --fit-height | tee -a dropin/borders.txt && \
python tools/render_slice.py "$S/button_primary_normal.png" 0.40 0.45 731 213 "dropin/dice button.png" --fit-height | tee -a dropin/borders.txt && \
python tools/render_slice.py "$S/button_primary_normal.png" 0.40 0.45 325 152 "dropin/new act 2.png" --fit-height | tee -a dropin/borders.txt && \
python tools/render_slice.py "$S/button_secondary_normal.png" 0.40 0.45 379 149 "dropin/new act3.png" --fit-height | tee -a dropin/borders.txt && \
python tools/render_slice.py "$S/button_primary_normal.png" 0.40 0.45 268 102 "dropin/_pill_primary_268.png" --fit-height && \
python tools/render_slice.py "$S/button_secondary_normal.png" 0.40 0.45 268 102 "dropin/_pill_secondary_268.png" --fit-height
```

- [x] **Step 3: 글자 합성 (선택/취소 + 메인메뉴 4)**

```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && \
python tools/text_label.py "선택" 268 102 "dropin/선택.png" --on dropin/_pill_primary_268.png && \
python tools/text_label.py "취소" 268 102 "dropin/취소.png" --on dropin/_pill_secondary_268.png && \
python tools/text_label.py "Start Game" 394 67 "dropin/Start Game.png" && \
python tools/text_label.py "Continue" 309 67 "dropin/Continue.png" && \
python tools/text_label.py "Settings" 286 80 "dropin/Settings.png" && \
python tools/text_label.py "Quit" 154 81 "dropin/Quit.png" && rm dropin/_pill_*.png
```

- [x] **Step 4: PIL 도형**

```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && python tools/draw_shapes.py dropin
```
Expected: 5장 크기 출력 (153×18, 149×10, 255×23, 266×266, 1920×188).

- [x] **Step 5: 컨택트 시트 → Read로 검토**

```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && python tools/contact_sheet.py dropin review/dropin_sheet.png && ls dropin | wc -l
```
Expected: `22장` (상점 배경 제외), 시트에서 알약 글자 중앙·HP바 색·주사위 면·바 확인.

---

### Task 3: 상점 배경 (힉스필드 1장) → 승인

**Files:**
- Create: `candidates/shop_bg.png` → `dropin/상점 임시배경.png` (1536×1024)
- Modify: `Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md`

- [x] **Step 1: 제출**

```
generate_image_batch(requests=[{"index":0,"params":{
  "model":"gpt_image_2_5","quality":"high","resolution":"2k","background":"opaque","aspect_ratio":"3:2",
  "medias":[{"value":"<REF_STYLE>","role":"image_references"},{"value":"<REF_TITLE>","role":"image_references"},{"value":"<REF_NODE>","role":"image_references"}],
  "prompt":"Background illustration for a shop screen in a cute pastel cartoon roguelike: the inside of a witch's laboratory at night used as a small shop. Wooden shelves lined with potion bottles, flasks, jars and rolled scrolls along the left and right, a wooden counter across the lower third, warm candle light, a window with the night sky and stars at the top center. Thick dark ink outlines, flat pastel colors (cream, lavender, sky blue, pink accents), the same line weight and palette as the reference images. Keep the center of the image uncluttered so a shopkeeper character can stand there. No text, no characters, no UI."}}])
```
job_id를 로그에 기록 (2.75, 누적 33.75). `unlim_choice`가 오면 사용자에게 묻고 재호출.

- [x] **Step 2: 대기·다운로드·리사이즈**

```
jobs_wait(jobs=[{"index":0,"job_id":"<job>"}], timeout_seconds=15)
```
```bash
cd "D:/Dice Orbit/_workspace/2026-09-25-ui-reskin" && curl -sL -o candidates/shop_bg.png "<url>" && python -c "
from PIL import Image
im = Image.open('candidates/shop_bg.png').convert('RGB'); print(im.size)
im.resize((1536, 1024), Image.LANCZOS).save('dropin/상점 임시배경.png'); print('resized 1536x1024')"
```

- [x] **Step 3: 승인 요청**

`SendUserFile`로 `review/dropin_sheet.png` + `dropin/상점 임시배경.png`를 보여주고 `AskUserQuestion`: "전부 승인 / 일부 재작업(메모)". 재작업이 렌더·도형·글자면 크레딧 없이 도구 파라미터만 고쳐 재실행, 배경이면 재생성(2.75).

- [x] **Step 4: 로그 커밋**

```bash
cd "D:/Dice Orbit" && git add Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md && git commit -q -m "docs(ui-skin): 4단계 상점 배경 생성 승인 — 생성 로그 갱신

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: Assets에 덮어쓰기 + 임포터 경계 재설정 + 검증

**Files:**
- Modify (덮어쓰기): `Assets/Sprites/UI 이미지/{주사위 패널,오른쪽 패널,패널 1차,Rectangle 16,dice button,new act 2,new act3,파티 로스터 패널,선택,취소,Start Game,Continue,Settings,Quit}.png`, `Assets/Sprites/Info UI 이미지/{체력바,체력_피,hp}.png`, `Assets/Sprites/{Dice,Rectangle 41}.png`, `Assets/Sprites/상점/상점 임시배경.png`
- Modify (`.meta`, Unity 경유): 위 파일들의 spriteBorder·mipmap·filter

- [x] **Step 1: 복사 (백업은 git이 가진다)**

```bash
cd "D:/Dice Orbit" && D=_workspace/2026-09-25-ui-reskin/dropin && \
for n in "주사위 패널" "오른쪽 패널" "패널 1차" "Rectangle 16" "dice button" "new act 2" "new act3" "파티 로스터 패널" "선택" "취소" "Start Game" "Continue" "Settings" "Quit"; do cp "$D/$n.png" "Assets/Sprites/UI 이미지/$n.png"; done && \
for n in "체력바" "체력_피" "hp"; do cp "$D/$n.png" "Assets/Sprites/Info UI 이미지/$n.png"; done && \
cp "$D/Dice.png" Assets/Sprites/Dice.png && cp "$D/Rectangle 41.png" "Assets/Sprites/Rectangle 41.png" && cp "$D/상점 임시배경.png" "Assets/Sprites/상점/상점 임시배경.png" && \
git -c core.quotepath=off status --short | grep -c "^ M" 
```
Expected: `23`.

- [x] **Step 2: 임포터 설정 RunCommand** (경계값은 Task 2의 `borders.txt`를 그대로 옮긴다)

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    // (경로, l, b, r, t) — 0이면 Simple 용도(경계 없음). 값은 _workspace/.../dropin/borders.txt
    private static readonly (string path, int l, int b, int r, int t)[] Items =
    {
        ("Assets/Sprites/UI 이미지/주사위 패널.png", 156, 78, 156, 78),
        ("Assets/Sprites/UI 이미지/오른쪽 패널.png", 156, 78, 156, 78),
        ("Assets/Sprites/UI 이미지/패널 1차.png", 156, 87, 156, 87),
        ("Assets/Sprites/UI 이미지/파티 로스터 패널.png", 90, 92, 90, 92),
        ("Assets/Sprites/UI 이미지/Rectangle 16.png", 0, 0, 0, 0),   // borders.txt 값으로 교체
        ("Assets/Sprites/UI 이미지/dice button.png", 0, 0, 0, 0),    // 〃
        ("Assets/Sprites/UI 이미지/new act 2.png", 0, 0, 0, 0),      // 〃
        ("Assets/Sprites/UI 이미지/new act3.png", 0, 0, 0, 0),       // 〃
        ("Assets/Sprites/UI 이미지/선택.png", 0, 0, 0, 0),
        ("Assets/Sprites/UI 이미지/취소.png", 0, 0, 0, 0),
        ("Assets/Sprites/UI 이미지/Start Game.png", 0, 0, 0, 0),
        ("Assets/Sprites/UI 이미지/Continue.png", 0, 0, 0, 0),
        ("Assets/Sprites/UI 이미지/Settings.png", 0, 0, 0, 0),
        ("Assets/Sprites/UI 이미지/Quit.png", 0, 0, 0, 0),
        ("Assets/Sprites/Info UI 이미지/체력바.png", 0, 0, 0, 0),
        ("Assets/Sprites/Info UI 이미지/체력_피.png", 0, 0, 0, 0),
        ("Assets/Sprites/Info UI 이미지/hp.png", 0, 0, 0, 0),
        ("Assets/Sprites/Dice.png", 0, 0, 0, 0),
        ("Assets/Sprites/Rectangle 41.png", 0, 0, 0, 0),
        ("Assets/Sprites/상점/상점 임시배경.png", 0, 0, 0, 0),
    };

    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh();
        foreach (var (path, l, b, r, t) in Items)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { result.LogError("임포터 없음: " + path); continue; }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = (l + r + t + b) > 0 ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.spriteBorder = new Vector4(l, b, r, t);
            importer.SaveAndReimport();
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            result.Log("{0}: {1}x{2} border={3}", path, w, h, importer.spriteBorder);
        }
        AssetDatabase.SaveAssets();
        result.Log("드롭인 임포터 설정 완료: {0}", Items.Length);
    }
}
```
Expected: 20줄 + 완료 로그. `Unity_GetConsoleLogs(Error)` 0건.

- [x] **Step 3: 자가 테스트 + 플레이 캡처**

RunCommand `UiSkinSelfTests.RunAll()` → PASS. `Unity_ManageEditor(Play)` → BattleScene 시작 화면(모집 화면: 병 3개 + 선택/취소·HP바·로스터)을 `ScreenCapture.CaptureScreenshot("D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/review/phase4_recruit.png")` → 캐릭터 하나 선택 상태를 만들기 어렵다면 병 화면만 캡처 → `Stop`. `Read`로 확인.

- [x] **Step 4: 커밋 (LFS 포인터 확인)**

```bash
cd "D:/Dice Orbit" && git add "Assets/Sprites" && git -c core.quotepath=off status --short | head -30 && git commit -q -m "feat(ui-skin): 씬 드롭인 20장 — 전투 HUD·메인메뉴·모집 버튼·HP바·주사위 면·상점 배경을 크림 스킨으로 (파일명 유지, 재배선 없음)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: 문서·메모리 갱신 + 보고

- [x] **Step 1: 스펙 상태·README·생성 로그 누적 비용**

스펙 상단 상태 줄 → `> 상태: **1~4단계 완료 — 5단계(사용자 플레이 확인·WebGL 재빌드) 대기**`. README 표: 스펙 행 상태 `진행 중 (1~4단계 완료)`, 4단계 계획 행 추가. 생성 로그 「누적 비용」에 4단계 행 추가 (`balance` 호출값).

- [x] **Step 2: 커밋 + 보고**

```bash
cd "D:/Dice Orbit" && sed -i 's/^- \[ \] \*\*Step/- [x] **Step/' Docs/superpowers/plans/2026-09-25-ui-reskin-phase4-scene-dropin.md && git add Docs && git commit -q -m "docs(ui-skin): 리스킨 4단계 완료 — 스펙 상태·README·생성 로그 갱신

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```
보고: 브랜치, 커밋, 잔액, 사용자 확인 목록(메인메뉴는 MainMenu 씬에서, 전투 HUD는 전투 진입 후), 제외 항목 3종.

---

## 실행 기록 (2026-09-25)

- 드롭인은 20장(계획의 22는 오산): UI 이미지 14 + HP바 3 + Dice + Rectangle 41 + 상점 배경. 힉스필드는 상점 배경 1장(2.75, 누적 33.75, 잔액 46.25).
- `파티 로스터 패널` 경계는 슬롯 경계 합(180)이 140을 넘어 Unity 규칙대로 70,70,70,70으로 축소됨.
- **추가 개선(사용자 위임)**: 드롭인만으로는 hover/pressed가 없어 `UiSkinImage`를 버튼 8개에 부착 — MainMenu `GameStart`=ButtonPrimary, `continue/setting/quit`=ButtonSecondary; BattleScene `Roll Dice`·`MoveButton`=ButtonPrimary, `End Turn Button`·`CancelButton`=ButtonSecondary (TMP 라벨은 Ink). 두 씬 모두 dirty 아님을 확인하고 저장. 이 8개 버튼은 이제 스킨 스프라이트를 직접 쓰므로 `Rectangle 16.png`·`dice button.png`·`new act 2/3.png` 드롭인은 그 오브젝트에서는 보이지 않는다(다른 참조·안전망으로 유지).
- 검증: 콘솔 에러 0, 자가 테스트 PASS, MainMenu Play 캡처(`review/phase4_mainmenu.png`), 모집 화면 Play 캡처(`review/phase4_battle_hud.png`).
- 사용자 확인 남음: 전투 진입 후 주사위 패널·하단 바·HP바·주사위 면, 상점 배경, 정보 패널(오른쪽 패널 Simple 스트레치), 툴팁 패널(패널 1차 Sliced).
