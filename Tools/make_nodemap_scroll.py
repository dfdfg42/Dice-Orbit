"""노드맵 가운데 지도 두루마리를 새 그림으로 바꾼다 (2026-10-04).
usage: python Tools/make_nodemap_scroll.py <두루마리 그림.png>             # 배경에서 옛 두루마리를 지우고, 새 그림을 스프라이트 + 배치표로 내보낸다
       python Tools/make_nodemap_scroll.py <두루마리 그림.png> --preview   # 에셋은 건드리지 않고 합성 미리보기만 (_workspace)

예전에는 두루마리가 배경(nodemap_bg.png)에 같이 그려져 있어 모양만 따로 고칠 수 없었다. 이제는 둘로 나눈다:
  - 배경 = 탁자와 소품만. 옛 두루마리 자리는 오른쪽의 빈 나무결을 거울처럼 이어 붙여 메운다
    (새 두루마리가 그 자리를 덮으므로 가장자리 조금만 실제로 보인다).
  - 두루마리 = 따로 떠 있는 스프라이트. 탁자 위 다른 소품처럼 아래·오른쪽으로 떨어지는 그림자를 붙여서 낸다.
  - 말린 부분(위·아래 롤)은 한 번 더 따로 낸다 — 지도 내용 위에 덮어 그려 노드가 롤 밑으로 말려 들어가 보이게 한다.
    그림에 롤이 없으면(평평한 종이) 내지 않는다.

배치표(scroll_layout.json)의 좌표는 배경 원본 픽셀(2688x1520, y는 위에서 아래)이고, 씬은
[DiceOrbit → Rebuild Node Map Scroll]이 그 비율을 앵커로 써서 만든다.
배경을 이미 지운 뒤 다시 돌려도 된다 (지울 두루마리가 없으면 그 단계는 건너뛴다).
"""
import json
import os
import re
import sys
import uuid

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

BG = "Assets/Sprites/배경/nodemap_bg.png"
OUT = "Assets/Sprites/배경/NodeMapScroll/"
LAYOUT = OUT + "scroll_layout.json"
PREVIEW = "_workspace/2026-09-25-ui-reskin/review/"

# 새 두루마리가 놓일 자리 (배경 원본 픽셀). 가운데 정렬, 위아래 여백을 남기고 높이에 맞춘다.
CENTER_X = 1344
TOP, BOTTOM = 24, 1496

# 옛 두루마리 찾기·지우기
SEARCH_X = (640, 2040)          # 이 열 범위 안의 '푸른 덩어리'가 두루마리다 (나무는 붉다)
WOOD_BAND = (2015, 2150)        # 물체가 하나도 없는 나무결 열 — 이걸 거울처럼 이어 붙여 메운다
ERASE_GROW, ERASE_FEATHER = 22, 10   # 그림자까지 덮도록 넓히고, 가장자리는 풀어 준다

SHADOW_OFFSET = (10, 16)
SHADOW_BLUR = 5
SHADOW_COLOR = (92, 48, 78)
SHADOW_ALPHA = 0.30

ROLL_EXTRA = 0.02               # 몸통 폭보다 이만큼(그림 폭 대비) 넓은 행을 롤로 본다
ROLL_OVERLAP = 10               # 롤 조각을 종이 쪽으로 이만큼 더 포함 (롤 아래 외곽선까지)


# ── 배경에서 옛 두루마리 지우기 ─────────────────────────────────

def find_scroll(bg):
    """배경 가운데의 두루마리 실루엣 (없으면 None)."""
    r, b = bg[..., 0].astype(np.int32), bg[..., 2].astype(np.int32)
    bluish = b > r - 12
    bluish[:, :SEARCH_X[0]] = False
    bluish[:, SEARCH_X[1]:] = False
    labels, count = ndimage.label(bluish)
    if count == 0:
        return None
    sizes = ndimage.sum(bluish, labels, range(1, count + 1))
    if sizes.max() < 200_000:   # 소품의 푸른 조각이 아니라 큰 종이여야 한다
        return None
    return ndimage.binary_fill_holes(labels == 1 + int(np.argmax(sizes)))


def erase_scroll(bg):
    """두루마리 자리를 나무결로 메운 배경. 지울 것이 없으면 None."""
    silhouette = find_scroll(bg)
    if silhouette is None:
        return None

    h, w = bg.shape[:2]
    band = bg[:, WOOD_BAND[0]:WOOD_BAND[1]].astype(np.float32)
    period = band.shape[1]
    mirrored = np.concatenate([band, band[:, ::-1]], axis=1)          # 왕복 — 이음매에서 결이 끊기지 않는다
    columns = (np.arange(w) - WOOD_BAND[0]) % (2 * period)
    wood = mirrored[:, columns]

    distance = ndimage.distance_transform_edt(~silhouette)
    t = np.clip((distance - ERASE_GROW) / ERASE_FEATHER, 0.0, 1.0)
    cover = (1.0 - t * t * (3.0 - 2.0 * t))[..., None]
    return (bg * (1.0 - cover) + wood * cover + 0.5).astype(np.uint8)


# ── 새 두루마리 ─────────────────────────────────────────────────

def load_art(path):
    art = Image.open(path).convert("RGBA")
    alpha = np.asarray(art.getchannel("A")) > 8
    ys, xs = np.nonzero(alpha)
    return art.crop((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))


def place(art):
    """배경 원본 좌표에서의 자리 (x0, y0, x1, y1) — 높이에 맞추고 가운데 정렬."""
    height = BOTTOM - TOP
    width = round(art.width * height / art.height)
    x0 = CENTER_X - width // 2
    return x0, TOP, x0 + width, BOTTOM


def with_shadow(art):
    """탁자 위 그림자를 깐 그림과, 그림자 때문에 늘어난 여백 (left, top, right, bottom)."""
    pad = SHADOW_BLUR * 3
    grow = (pad, pad, SHADOW_OFFSET[0] + pad, SHADOW_OFFSET[1] + pad)
    canvas = Image.new("RGBA", (art.width + grow[0] + grow[2], art.height + grow[1] + grow[3]), (0, 0, 0, 0))

    alpha = art.getchannel("A").point(lambda v: int(v * SHADOW_ALPHA))
    shadow = Image.new("RGBA", art.size, SHADOW_COLOR + (0,))
    shadow.putalpha(alpha)
    canvas.alpha_composite(shadow, (grow[0] + SHADOW_OFFSET[0], grow[1] + SHADOW_OFFSET[1]))
    canvas = canvas.filter(ImageFilter.GaussianBlur(SHADOW_BLUR))
    canvas.alpha_composite(art, (grow[0], grow[1]))
    return canvas, grow


def find_rolls(art):
    """말린 부분의 행 범위 [(y0, y1) 위, (y0, y1) 아래]. 롤(봉·손잡이)은 종이 몸통보다 옆으로 튀어나와 있다. 없으면 None."""
    alpha = np.asarray(art.getchannel("A")) > 128
    widths = alpha.sum(axis=1)
    quarter = art.height // 4
    body = np.median(widths[quarter: art.height - quarter])
    wide = widths > body + art.width * ROLL_EXTRA

    top_rows = np.nonzero(wide[:quarter])[0]
    bottom_rows = np.nonzero(wide[art.height - quarter:])[0]
    if len(top_rows) == 0 or len(bottom_rows) == 0:
        return None
    return (0, int(top_rows.max()) + 1), (art.height - quarter + int(bottom_rows.min()), art.height)


def paper_rect(art, rolls):
    """지도 내용이 놓일 종이 안쪽 (그림 픽셀) — 롤 사이, 양옆 외곽선 안쪽."""
    alpha = np.asarray(art.getchannel("A")) > 128
    y0, y1 = (rolls[0][1], rolls[1][0]) if rolls else (0, art.height)
    xs = np.nonzero(alpha[(y0 + y1) // 2])[0]
    inset = round(art.width * 0.02)   # 외곽선 두께
    if not rolls:
        y0, y1 = y0 + inset, y1 - inset
    return int(xs.min()) + inset, y0, int(xs.max()) + 1 - inset, y1


# ── 저장 ────────────────────────────────────────────────────────

def import_scale(source_size):
    meta = open(BG + ".meta", encoding="utf-8").read()
    max_size = int(re.search(r"maxTextureSize: (\d+)", meta).group(1))
    return min(1.0, max_size / max(source_size))


def write_meta(path):
    """배경의 임포트 설정(밉맵·삼선형)을 그대로. 이미 있으면 건드리지 않는다 — GUID가 바뀌면 씬 참조가 끊긴다."""
    if os.path.exists(path + ".meta"):
        return
    meta = open(BG + ".meta", encoding="utf-8").read()
    meta = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, meta, count=1)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(meta)


def save_sprite(image, filename, texel_scale):
    size = (max(1, round(image.width * texel_scale)), max(1, round(image.height * texel_scale)))
    image.resize(size, Image.LANCZOS).save(OUT + filename)
    write_meta(OUT + filename)
    return OUT + filename


def build(art_path, preview):
    source = Image.open(BG).convert("RGB")
    bg = np.asarray(source)
    erased = erase_scroll(bg)
    table = Image.fromarray(erased) if erased is not None else source
    print("옛 두루마리:", "지웠다" if erased is not None else "없음 (이미 지운 배경)")

    art = load_art(art_path)
    x0, y0, x1, y1 = place(art)
    scale = (x1 - x0) / art.width                      # 그림 픽셀 → 배경 원본 픽셀
    sheet, grow = with_shadow(art)
    rolls = find_rolls(art)
    px0, py0, px1, py1 = paper_rect(art, rolls)

    def to_bg(ax0, ay0, ax1, ay1):
        return dict(x=x0 + ax0 * scale, y=y0 + ay0 * scale, width=(ax1 - ax0) * scale, height=(ay1 - ay0) * scale)

    if preview:
        os.makedirs(PREVIEW, exist_ok=True)
        frame = table.convert("RGBA")
        size = (round(sheet.width * scale), round(sheet.height * scale))
        frame.alpha_composite(sheet.resize(size, Image.LANCZOS), (round(x0 - grow[0] * scale), round(y0 - grow[1] * scale)))
        name = PREVIEW + "scroll_preview_" + os.path.splitext(os.path.basename(art_path))[0] + ".png"
        frame.convert("RGB").resize((1920, round(1920 * source.height / source.width)), Image.LANCZOS).save(name)
        print("preview ->", name, "| rolls:", rolls, "| paper:", to_bg(px0, py0, px1, py1))
        return

    os.makedirs(OUT, exist_ok=True)
    if erased is not None:
        table.save(BG)

    texel = scale * import_scale(source.size)          # 배경과 같은 선명도
    pieces = [dict(name="MapScroll", sprite=save_sprite(sheet, "nodemap_scroll.png", texel),
                   **to_bg(-grow[0], -grow[1], art.width + grow[2], art.height + grow[3]))]
    if rolls:
        (t0, t1), (b0, b1) = rolls
        top = art.crop((0, t0, art.width, t1 + ROLL_OVERLAP))
        bottom = art.crop((0, b0 - ROLL_OVERLAP, art.width, b1))
        pieces.append(dict(name="RollTop", sprite=save_sprite(top, "nodemap_scroll_roll_top.png", texel),
                           **to_bg(0, t0, art.width, t1 + ROLL_OVERLAP)))
        pieces.append(dict(name="RollBottom", sprite=save_sprite(bottom, "nodemap_scroll_roll_bottom.png", texel),
                           **to_bg(0, b0 - ROLL_OVERLAP, art.width, b1)))

    layout = dict(sourceWidth=source.width, sourceHeight=source.height, hasRolls=bool(rolls),
                  paper=to_bg(px0, py0, px1, py1), pieces=pieces)
    with open(LAYOUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(layout, f, ensure_ascii=False, indent=2)
        f.write("\n")
    for piece in pieces:
        print(f"{piece['name']:10s} x={piece['x']:7.1f} y={piece['y']:7.1f} w={piece['width']:7.1f} h={piece['height']:7.1f} -> {piece['sprite']}")
    print("paper", layout["paper"], "\nlayout ->", LAYOUT)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if len(args) != 1:
        sys.exit(__doc__)
    build(args[0], "--preview" in sys.argv)
