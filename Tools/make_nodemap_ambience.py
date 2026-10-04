"""노드맵 배경(nodemap_bg.png)의 물체를 살짝 움직이기 위한 '깃털 조각'과 배치표를 만든다 (2026-10-04).
usage: python Tools/make_nodemap_ambience.py            # 조각·광원·반짝이 스프라이트 + ambience_layout.json
       python Tools/make_nodemap_ambience.py --preview   # 에셋은 건드리지 않고, 가장 크게 움직인 자세의 합성 그림만 _workspace에

배경은 통짜 그림 한 장이라 촛불이나 깃펜만 따로 움직일 수 없다. 그래서 물체를 주변 나무결과 함께 떠내고
가장자리를 투명하게 풀어 준 조각을 원래 자리에 겹쳐 놓은 뒤 그 조각만 움직인다.
  - 조각의 불투명한 가운데(물체 + margin)가 원본 물체를 덮는다 → 움직임이 margin보다 작아야 원본이 비치지 않는다.
  - 풀린 가장자리(feather)는 나무결끼리 섞여 이음매가 보이지 않는다.
  - 그림 밖으로 나가는 부분은 가장자리 픽셀을 늘려 채운다 (화면 밖이라 보이지 않지만, 조각이 안쪽으로 당겨질 때 덮을 것이 필요하다).

물체를 더하려면 PIECES에 한 줄을 더하고 이 도구를 돌린 뒤 Unity에서 [DiceOrbit → Rebuild Node Map Ambience].
움직임 수치(motion)는 씬을 처음 만들 때의 기본값이다 — 만든 뒤에는 씬(AmbientMotion·AmbientTwinkle)이 수치를 소유한다.
배경 그림을 바꾸면 좌표가 전부 달라지므로 PIECES를 새 그림에 맞춰 다시 잡아야 한다.
"""
import json
import math
import os
import re
import sys
import uuid

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

BG = "Assets/Sprites/배경/nodemap_bg.png"
OUT = "Assets/Sprites/배경/NodeMapAmbience/"
LAYOUT = OUT + "ambience_layout.json"
PREVIEW = "_workspace/2026-09-25-ui-reskin/review/"
PAD = 64   # 그림 밖으로 늘려 두는 폭


def wave(amplitude, period, jitter=0.0):
    """amplitude: 회전은 도, 배율은 비율(0.04 = ±4%), 알파는 0~1. jitter 0 = 고른 사인, 1 = 불규칙."""
    return dict(amplitude=amplitude, period=period, jitter=jitter)


# 좌표는 전부 원본 그림(2688x1520) 픽셀, y는 위에서 아래로. 적은 순서 = 그리는 순서.
PIECES = [
    # ── 촛불: 따뜻한 빛이 일렁이고 불꽃이 흔들린다 ──
    dict(name="candle_glow", kind="glow", center=(190, 150), radius=185, color=(1.0, 0.90, 0.62, 0.16),
         motion=dict(alpha=wave(0.07, 0.9, 1.0), scaleX=wave(0.05, 1.4, 1.0), scaleY=wave(0.05, 1.4, 1.0))),
    dict(name="candle_flame", kind="patch", margin=8, feather=10, pivot=(188, 199),
         polygon=[(188, 78), (212, 112), (230, 148), (229, 178), (213, 199), (188, 207),
                  (163, 199), (147, 178), (146, 148), (164, 112)],
         motion=dict(rotation=wave(3.0, 0.9, 1.0), scaleX=wave(0.035, 0.7, 1.0), scaleY=wave(0.05, 0.6, 1.0))),

    # ── 나침반: 바늘(별)이 느리게 떤다 ──
    dict(name="compass_needle", kind="patch", margin=4, feather=6, pivot=(513, 362),
         ellipse=(513, 362, 80, 80),
         motion=dict(rotation=wave(5.0, 5.5, 0.6))),

    # ── 깃펜: 잉크병 입구를 축으로 까딱인다 ──
    dict(name="quill", kind="patch", margin=12, feather=12, pivot=(2372, 287),
         polygon=[(2362, 292), (2370, 235), (2384, 170), (2404, 120), (2440, 80), (2500, 40), (2570, 12),
                  (2620, 8), (2638, 35), (2626, 75), (2602, 106), (2578, 150), (2546, 186), (2500, 226),
                  (2460, 256), (2420, 266), (2396, 272), (2386, 292)],
         motion=dict(rotation=wave(1.3, 4.2, 0.15))),

    # ── 찻잔: 잔 위로 올라온 김이 하늘거린다 (잔에 걸친 아랫부분은 그대로 둔다) ──
    dict(name="tea_steam", kind="patch", margin=8, feather=12, pivot=(2560, 512),
         polygon=[(2492, 504), (2496, 455), (2540, 386), (2576, 370), (2610, 378), (2632, 410),
                  (2640, 470), (2626, 504)],
         motion=dict(rotation=wave(2.2, 3.4, 0.3), scaleY=wave(0.04, 2.6, 0.2))),

    # ── 고양이: 숨 쉰다 (바닥을 축으로 세로로만) ──
    dict(name="cat", kind="patch", margin=8, feather=16, pivot=(2440, 1476),
         rect=(2176, 1075, 2700, 1500),
         motion=dict(scaleY=wave(0.008, 3.8, 0.0))),

    # ── 반짝이: 주사위·금속·유리에 가끔 ──
    dict(name="sparkle_d6_left", kind="sparkle", center=(300, 536), size=56),
    dict(name="sparkle_d12", kind="sparkle", center=(90, 640), size=50),
    dict(name="sparkle_compass", kind="sparkle", center=(432, 300), size=54),
    dict(name="sparkle_ink", kind="sparkle", center=(2276, 344), size=50),
    dict(name="sparkle_cup", kind="sparkle", center=(2360, 556), size=52),
    dict(name="sparkle_d6_pink", kind="sparkle", center=(2326, 900), size=50),
    dict(name="sparkle_d6_blue", kind="sparkle", center=(2600, 926), size=50),
]

SPARKLE_DEFAULTS = dict(intervalMin=5.0, intervalMax=12.0, duration=0.75, spin=35.0)


# ── 조각 떠내기 ─────────────────────────────────────────────────

def shape_mask(piece, size):
    """물체 모양(패딩 좌표)의 이진 마스크."""
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    if "polygon" in piece:
        draw.polygon([(x + PAD, y + PAD) for x, y in piece["polygon"]], fill=255)
    elif "ellipse" in piece:
        cx, cy, rx, ry = piece["ellipse"]
        draw.ellipse([cx - rx + PAD, cy - ry + PAD, cx + rx + PAD, cy + ry + PAD], fill=255)
    elif "rect" in piece:
        x0, y0, x1, y1 = piece["rect"]
        draw.rectangle([x0 + PAD, y0 + PAD, x1 + PAD, y1 + PAD], fill=255)
    else:
        raise ValueError(f"{piece['name']}: polygon·ellipse·rect 중 하나가 있어야 한다")
    return np.asarray(mask) > 0


def cut_patch(padded, piece):
    """깃털 조각(RGBA)과 원본 좌표계의 상자 (x0, y0, x1, y1)."""
    h, w = padded.shape[:2]
    inside = shape_mask(piece, (w, h))
    distance = ndimage.distance_transform_edt(~inside)   # 모양 밖으로 얼마나 나갔나

    margin, feather = piece["margin"], piece["feather"]
    t = np.clip((distance - margin) / feather, 0.0, 1.0)
    alpha = 1.0 - t * t * (3.0 - 2.0 * t)   # smoothstep

    ys, xs = np.nonzero(alpha > 0.0)
    x0, x1 = max(xs.min() - 1, 0), min(xs.max() + 2, w)
    y0, y1 = max(ys.min() - 1, 0), min(ys.max() + 2, h)

    rgba = np.dstack([padded[y0:y1, x0:x1, :3], (alpha[y0:y1, x0:x1] * 255.0 + 0.5).astype(np.uint8)])
    return Image.fromarray(rgba, "RGBA"), (int(x0) - PAD, int(y0) - PAD, int(x1) - PAD, int(y1) - PAD)


# ── 그려서 만드는 스프라이트 ────────────────────────────────────

def make_glow(size=256):
    """부드러운 원형 빛 — 색은 Image.color가 입힌다."""
    c = (size - 1) / 2.0
    y, x = np.mgrid[0:size, 0:size]
    r = np.clip(np.hypot(x - c, y - c) / c, 0.0, 1.0)
    alpha = (1.0 - r) ** 2
    rgba = np.dstack([np.full((size, size, 3), 255, np.uint8), (alpha * 255.0 + 0.5).astype(np.uint8)])
    return Image.fromarray(rgba, "RGBA")


def make_sparkle(size=128):
    """네 갈래 반짝이 + 옅은 후광."""
    c = (size - 1) / 2.0
    y, x = np.mgrid[0:size, 0:size]
    nx, ny = np.abs(x - c) / c, np.abs(y - c) / c
    p = 0.5
    star = (nx ** p + ny ** p) ** (1.0 / p)                     # 1에서 네 갈래 별의 윤곽
    core = np.clip((0.92 - star) / 0.12, 0.0, 1.0)
    halo = 0.35 * np.exp(-((np.hypot(nx, ny) / 0.30) ** 2))
    alpha = np.clip(core + halo, 0.0, 1.0)
    rgba = np.dstack([np.full((size, size, 3), 255, np.uint8), (alpha * 255.0 + 0.5).astype(np.uint8)])
    return Image.fromarray(rgba, "RGBA")


# ── 저장 ────────────────────────────────────────────────────────

def import_scale(source_size):
    """배경이 임포트될 때 줄어드는 비율 — 조각도 같은 비율로 줄여 선명도가 배경과 같게 한다."""
    meta = open(BG + ".meta", encoding="utf-8").read()
    max_size = int(re.search(r"maxTextureSize: (\d+)", meta).group(1))
    return min(1.0, max_size / max(source_size))


def write_meta(path):
    """배경의 임포트 설정(밉맵·삼선형)을 그대로 쓴다. 이미 있으면 건드리지 않는다 — GUID가 바뀌면 씬 참조가 끊긴다."""
    meta_path = path + ".meta"
    if os.path.exists(meta_path):
        return
    meta = open(BG + ".meta", encoding="utf-8").read()
    meta = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, meta, count=1)
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(meta)


def save_sprite(image, filename):
    image.save(OUT + filename)
    write_meta(OUT + filename)
    return OUT + filename


def build():
    source = Image.open(BG).convert("RGB")
    padded = np.pad(np.asarray(source), ((PAD, PAD), (PAD, PAD), (0, 0)), mode="edge")
    scale = import_scale(source.size)

    os.makedirs(OUT, exist_ok=True)
    glow_path = save_sprite(make_glow(), "nodemap_amb_glow.png")
    sparkle_path = save_sprite(make_sparkle(), "nodemap_amb_sparkle.png")

    entries = []
    for piece in PIECES:
        entry = dict(name=piece["name"], kind=piece["kind"])
        entry.update(piece.get("motion", {}))

        if piece["kind"] == "patch":
            patch, (x0, y0, x1, y1) = cut_patch(padded, piece)
            out_size = (max(1, round(patch.width * scale)), max(1, round(patch.height * scale)))
            entry["sprite"] = save_sprite(patch.resize(out_size, Image.LANCZOS), f"nodemap_amb_{piece['name']}.png")
            entry.update(x=x0, y=y0, width=x1 - x0, height=y1 - y0, pivotX=piece["pivot"][0], pivotY=piece["pivot"][1])
        elif piece["kind"] == "glow":
            cx, cy = piece["center"]
            r = piece["radius"]
            entry.update(sprite=glow_path, x=cx - r, y=cy - r, width=2 * r, height=2 * r, pivotX=cx, pivotY=cy,
                         color=list(piece["color"]))
        elif piece["kind"] == "sparkle":
            cx, cy = piece["center"]
            half = piece["size"] / 2.0
            entry.update(sprite=sparkle_path, x=cx - half, y=cy - half, width=piece["size"], height=piece["size"],
                         pivotX=cx, pivotY=cy)
            for key, value in SPARKLE_DEFAULTS.items():
                entry[key] = piece.get(key, value)
        else:
            raise ValueError(f"{piece['name']}: 모르는 kind '{piece['kind']}'")

        entries.append(entry)
        print(f"{entry['name']:18s} {entry['kind']:8s} x={entry['x']:7.1f} y={entry['y']:7.1f} "
              f"w={entry['width']:6.1f} h={entry['height']:6.1f} -> {entry['sprite']}")

    layout = dict(sourceWidth=source.width, sourceHeight=source.height, pieces=entries)
    with open(LAYOUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(layout, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("layout ->", LAYOUT)


# ── 미리보기 ────────────────────────────────────────────────────

def posed(patch, box, pivot, degrees, scale_x, scale_y, canvas_size):
    """조각을 pivot 기준으로 돌리고 늘려 전체 그림 좌표에 올린 RGBA."""
    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    canvas.paste(patch, (box[0], box[1]))
    px, py = pivot
    rad = math.radians(degrees)
    cos, sin = math.cos(rad), math.sin(rad)
    # 출력 → 입력 (역변환): p = pivot + S^-1 · R(-θ) · (p' - pivot)
    a, b = cos / scale_x, sin / scale_x
    d, e = -sin / scale_y, cos / scale_y
    c = px - a * px - b * py
    f = py - d * px - e * py
    return canvas.transform(canvas_size, Image.AFFINE, (a, b, c, d, e, f), resample=Image.BICUBIC)


def preview():
    source = Image.open(BG).convert("RGB")
    padded = np.pad(np.asarray(source), ((PAD, PAD), (PAD, PAD), (0, 0)), mode="edge")
    os.makedirs(PREVIEW, exist_ok=True)

    for sign, tag in ((1.0, "plus"), (-1.0, "minus")):
        frame = Image.fromarray(padded).convert("RGBA")
        for piece in PIECES:
            if piece["kind"] != "patch":
                continue
            patch, box = cut_patch(padded, piece)
            motion = piece.get("motion", {})
            amp = lambda key: motion[key]["amplitude"] * sign if key in motion else 0.0
            layer = posed(patch, (box[0] + PAD, box[1] + PAD), (piece["pivot"][0] + PAD, piece["pivot"][1] + PAD),
                          amp("rotation"), 1.0 + amp("scaleX"), 1.0 + amp("scaleY"), frame.size)
            frame.alpha_composite(layer)

        frame = frame.crop((PAD, PAD, PAD + source.width, PAD + source.height)).convert("RGB")
        frame.crop((0, 0, 700, source.height)).save(f"{PREVIEW}nm_amb_{tag}_left.png")
        frame.crop((source.width - 560, 0, source.width, source.height)).save(f"{PREVIEW}nm_amb_{tag}_right.png")
        print("preview ->", f"{PREVIEW}nm_amb_{tag}_left.png / _right.png")


if __name__ == "__main__":
    preview() if "--preview" in sys.argv else build()
