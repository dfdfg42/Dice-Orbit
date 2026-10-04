"""머리띠(창 제목 표시줄처럼 보이는 위쪽 띠) 없는 평평한 패널 스프라이트를 만든다 (2026-10-04).
usage: python Tools/make_plain_panel.py

원본 battle_tooltip_frame.png(키워드 툴팁 프레임)에서 위쪽 띠와 그 아래 경계선만 종이색으로 덮는다.
외곽선·모서리 둥글기·아래 그림자·종이 결은 원본 그대로라 다른 패널과 한 벌로 보인다.

방법: 위쪽 구역의 픽셀을 밝기로 나눈다.
  - 띠·경계선·밝은 테(밝기 ≥ INNER) → 같은 열의 아래쪽 종이 픽셀로 바꾼다 (종이 결 유지)
  - 외곽선과 띠 사이의 안티에일리어싱 픽셀 → 외곽선 색과 종이색을 같은 비율로 다시 섞는다
  - 외곽선·투명 → 그대로
"""
import numpy as np
from PIL import Image

SRC = "Assets/Sprites/UI Skin/battle_tooltip_frame.png"
DST = "Assets/Sprites/UI Skin/battle_plain_panel.png"

BAND_BOTTOM = 68        # 띠 + 경계선이 끝나는 행 (원본 64행부터 종이)
SAMPLE_OFFSET = 70      # 종이를 떠 오는 행 간격 — 같은 열의 이만큼 아래
OUTLINE_LUM = 60.0      # 외곽선 밝기 (≈ 50)
INNER_LUM = 165.0       # 이 이상이면 띠/선/종이 안쪽


def main():
    src = np.asarray(Image.open(SRC).convert("RGBA")).astype(np.float32)
    out = src.copy()

    rgb = src[..., :3]
    lum = rgb @ np.array([0.299, 0.587, 0.114], dtype=np.float32)

    for y in range(BAND_BOTTOM):
        paper = src[y + SAMPLE_OFFSET, :, :3]                       # 같은 열의 아래쪽 종이
        row_lum = lum[y]
        opaque = src[y, :, 3] > 8

        inner = opaque & (row_lum >= INNER_LUM)
        out[y, inner, :3] = paper[inner]

        # 외곽선↔띠 경계 픽셀: 외곽선 색은 그 픽셀에서 띠 성분을 뺀 값으로 추정하기 어렵다 → 원본 외곽선 대표색을 쓴다
        edge = opaque & (row_lum > OUTLINE_LUM) & (row_lum < INNER_LUM)
        if edge.any():
            t = (row_lum[edge] - OUTLINE_LUM) / (INNER_LUM - OUTLINE_LUM)   # 0 = 외곽선, 1 = 안쪽
            outline = np.array([48.0, 43.0, 82.0], dtype=np.float32)
            out[y, edge, :3] = outline + (paper[edge] - outline) * t[:, None]

    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA").save(DST)
    print("wrote", DST, out.shape[1], "x", out.shape[0])


if __name__ == "__main__":
    main()
