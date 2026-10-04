"""주사위 종류별 면 그림 시트(4x4)를 잘라 스프라이트 + 배선표로 낸다 (2026-10-05).
usage: python Tools/cut_die_faces.py <시트.png>

시트의 칸 순서(왼쪽 위부터 가로로)가 ORDER의 주사위 에셋 이름과 짝이다. 칸은 알파의 연결 덩어리로 찾는다 —
간격이 조금 어긋나도 된다. 모든 면을 같은 크기의 정사각 캔버스 가운데에 놓아 낸다.

die_faces.json: 에셋 이름 → 스프라이트 경로 + 숫자 색. 숫자 색은 면 가운데(숫자가 얹히는 자리)의 밝기로 정한다 —
어두운 면에는 크림색, 밝은 면에는 잉크색. Unity에서 [DiceOrbit → Assign Die Faces]가 이 표대로 DieDefinitionSO에 꽂는다.
ALIASES: 그림을 따로 그리지 않은 에셋이 다른 주사위의 면을 같이 쓴다.
"""
import json
import os
import re
import sys
import uuid

import numpy as np
from PIL import Image
from scipy import ndimage

OUT = "Assets/Sprites/Dice/"
META_TEMPLATE = "Assets/Sprites/Dice.png.meta"   # 밉맵 + 삼선형, 단일 스프라이트

ORDER = [
    "Die", "AdvanceDie", "AlchemyDie", "ArcaneDie",
    "ChaosDie", "FocusDie", "GiantDie", "LifeDie",
    "PolarityDie", "PrecisionDie", "RampartDie", "RefineDie",
    "ShadowDie", "SmallStepsDie", "StarlightDie", "SynthesisDie",
]
ALIASES = {"Die 1": "Die"}   # 시험용 low 주사위 — 표준 면

INK = [0.294, 0.259, 0.361, 1.0]      # UiSkin.Ink
CREAM = [0.980, 0.953, 0.878, 1.0]    # UiSkin.Paper
DARK_BELOW = 135.0                    # 가운데 밝기가 이보다 낮으면 크림색 숫자
PAD = 4


def find_tiles(alpha):
    """칸의 상자 (x0, y0, x1, y1) — 위에서 아래, 왼쪽에서 오른쪽 순."""
    labels, count = ndimage.label(alpha > 40)
    boxes = []
    for index, box in enumerate(ndimage.find_objects(labels)):
        ys, xs = box
        if (ys.stop - ys.start) * (xs.stop - xs.start) < 3000:
            continue   # 자잘한 얼룩
        boxes.append((xs.start, ys.start, xs.stop, ys.stop))
    if len(boxes) != len(ORDER):
        raise ValueError(f"칸 {len(boxes)}개를 찾았다 — {len(ORDER)}개여야 한다")

    boxes.sort(key=lambda b: b[1])
    columns = int(round(len(ORDER) ** 0.5))
    rows = [sorted(boxes[i:i + columns], key=lambda b: b[0]) for i in range(0, len(boxes), columns)]
    return [box for row in rows for box in row]


def write_meta(path):
    """이미 있으면 건드리지 않는다 — GUID가 바뀌면 주사위 에셋의 참조가 끊긴다."""
    if os.path.exists(path + ".meta"):
        return
    meta = open(META_TEMPLATE, encoding="utf-8").read()
    meta = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, meta, count=1)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(meta)


def main(sheet_path):
    sheet = Image.open(sheet_path).convert("RGBA")
    boxes = find_tiles(np.asarray(sheet)[..., 3])
    side = max(max(x1 - x0, y1 - y0) for x0, y0, x1, y1 in boxes) + PAD * 2

    os.makedirs(OUT, exist_ok=True)
    table = {}
    for name, (x0, y0, x1, y1) in zip(ORDER, boxes):
        tile = sheet.crop((x0, y0, x1, y1))
        canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        canvas.alpha_composite(tile, ((side - tile.width) // 2, (side - tile.height) // 2))
        path = f"{OUT}die_{name}.png"
        canvas.save(path)
        write_meta(path)

        center = np.asarray(canvas.convert("RGB")).astype(np.float32)
        c0, c1 = side * 3 // 8, side * 5 // 8
        luminance = float((center[c0:c1, c0:c1] @ np.array([0.299, 0.587, 0.114], dtype=np.float32)).mean())
        table[name] = dict(asset=name, sprite=path, numberColor=CREAM if luminance < DARK_BELOW else INK)
        print(f"{name:14s} {tile.width}x{tile.height} 가운데 밝기 {luminance:5.1f} → 숫자 {'크림' if luminance < DARK_BELOW else '잉크'}")

    for alias, target in ALIASES.items():
        table[alias] = dict(table[target], asset=alias)

    with open(OUT + "die_faces.json", "w", encoding="utf-8", newline="\n") as f:
        json.dump(dict(faces=list(table.values())), f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"{len(ORDER)}면 {side}x{side} →", OUT)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
