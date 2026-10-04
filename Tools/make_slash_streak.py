"""처치 연출의 참격 줄기 스프라이트를 그린다 (2026-10-05).
usage: python Tools/make_slash_streak.py

가로로 긴 흰 줄기 — 양 끝이 뾰족하고 가운데가 도톰한 칼자국 + 옅은 후광. 색은 SpriteRenderer.color(HitFeelProfile.deathSlice.slashColor)가 입힌다.
DeathSliceEffect가 길이·굵기를 몬스터 크기에 맞춰 늘려 쓴다.
"""
import os
import re
import uuid

import numpy as np
from PIL import Image

OUT = "Assets/Sprites/VFX/slash_streak.png"
META_TEMPLATE = "Assets/Sprites/배경/NodeMapAmbience/nodemap_amb_sparkle.png.meta"   # 밉맵 + 삼선형
WIDTH, HEIGHT = 512, 128


def main():
    y, x = np.mgrid[0:HEIGHT, 0:WIDTH]
    u = (x + 0.5) / WIDTH                      # 0~1 줄기를 따라
    v = np.abs((y + 0.5) / HEIGHT - 0.5) * 2   # 0(가운데)~1(가장자리)

    profile = (4.0 * u * (1.0 - u)) ** 0.75    # 양 끝 0, 가운데 1
    core_half = 0.20 * profile                 # 날카로운 흰 속
    glow_half = 0.85 * profile                 # 옅은 후광

    core = np.clip((core_half - v) / 0.035 + 0.5, 0.0, 1.0)
    glow = 0.42 * np.exp(-((v / np.maximum(glow_half, 1e-4)) ** 2) * 2.2) * np.clip(profile * 3.0, 0.0, 1.0)
    alpha = np.clip(core + glow * (1.0 - core), 0.0, 1.0)

    rgba = np.dstack([np.full((HEIGHT, WIDTH, 3), 255, np.uint8), (alpha * 255.0 + 0.5).astype(np.uint8)])
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(OUT)

    # 이미 있으면 건드리지 않는다 — GUID가 바뀌면 프로필 참조가 끊긴다
    if not os.path.exists(OUT + ".meta"):
        meta = open(META_TEMPLATE, encoding="utf-8").read()
        meta = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, meta, count=1)
        with open(OUT + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(meta)
    print("->", OUT, f"{WIDTH}x{HEIGHT}")


if __name__ == "__main__":
    main()
