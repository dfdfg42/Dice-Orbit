"""패널 스프라이트의 머리띠(창 제목 표시줄처럼 보이는 위쪽 띠와 그 아래 구분선)를 없애 평평한 패널로 만든다 (2026-10-04).
usage: python Tools/flatten_panel_headers.py            # 목록 전부, 제자리에서 덮어쓴다
       python Tools/flatten_panel_headers.py --preview   # 덮어쓰지 않고 _workspace에 미리보기만

외곽선·모서리 둥글기·아래 그림자·종이 결·장식(클립·핀·탭)은 그대로 두고 띠만 종이로 바꾼다.
이미 평평한 스프라이트에 다시 돌려도 결과가 같다 (띠가 없으면 종이를 종이로 덮을 뿐).
battle_plain_panel.png(SkinPart.PlainPanel)는 같은 방법으로 툴팁 프레임에서 먼저 떠낸 것이다.

방법 — 스프라이트마다 위쪽을 세 구역으로 나눈다:
  1) 모서리 구역 (맨 위 ~ 안쪽 종이가 제 폭에 닿는 행): 픽셀 단위로 '띠 색'만 종이색으로 바꾼다.
     외곽선과 띠 사이의 안티에일리어싱 픽셀은 외곽선↔종이로 다시 섞는다.
  2) 띠 구역 (모서리 구역 아래 ~ band_end): 본문 행을 통째로 복사한다 — 띠·구분선·물결선이 한꺼번에 사라지고
     양옆 외곽선은 본문과 똑같이 이어진다.
  3) 보존 상자 (클립·핀·탭 같은 장식): 통째 복사에서 빼고 1)처럼 '띠 색'만 바꾼다.
"""
import colorsys
import os
import sys

import numpy as np
from PIL import Image

SKIN = "Assets/Sprites/UI Skin/"
PREVIEW = "_workspace/2026-09-25-ui-reskin/review/flatten_preview/"

# band_end: 이 행부터 본문 종이 (구분선·물결선의 가장 아래 + 여유). keep: 장식 보존 상자 (x0, y0, x1, y1).
SPRITES = {
    "battle_tooltip_frame":  dict(band_end=68),
    "battle_dice_tooltip":   dict(band_end=80),
    "battle_info_frame":     dict(band_end=162, keep=[(250, 0, 474, 104)]),     # 위 가운데 탭
    "event_frame":           dict(band_end=150),
    "recruit_sheet":         dict(band_end=206, keep=[(50, 0, 215, 184)]),      # 클립
    "reward_frame":          dict(band_end=162),                                 # 물결 구분선
    "shop_step_frame":       dict(band_end=92),
    "tutorial_prompt_frame": dict(band_end=100, keep=[(392, 0, 508, 84)]),      # 핀
}

OUTLINE = np.array([48.0, 43.0, 82.0], dtype=np.float32)   # 잉크 외곽선 대표색
OUTLINE_LUM = 62.0
INNER_LUM = 150.0        # 이 이상이면 띠·종이 같은 밝은 안쪽
TAN_LUM = 118.0          # 황토색 계열은 이 밝기부터 띠로 본다 (띠 아래의 어두운 그림자선 포함)
MIX_TOLERANCE = 24.0     # 외곽선↔띠 혼합선에서 이만큼 벗어나면 다른 색(장식)으로 본다
ROW_CYCLE = 40           # 본문에서 떠 오는 행 범위 — 왕복(핑퐁)으로 돌려 이음매가 줄무늬로 보이지 않게 한다
LUM = np.array([0.299, 0.587, 0.114], dtype=np.float32)


def is_tan(rgb):
    """띠·구분선·종이가 속한 '바랜 황토색' 계열인가 (금색 핀·은색 클립·남색 외곽선은 아니다)."""
    r, g, b = [c / 255.0 for c in rgb]
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    return 28.0 / 360.0 <= h <= 56.0 / 360.0 and 0.08 <= s <= 0.46 and v >= 0.55


def recolor_pixel(pixel, paper, band):
    """한 픽셀에서 띠 성분만 종이로 바꾼다. 바꿀 게 없으면 None."""
    rgb = pixel[:3]
    lum = float(rgb @ LUM)

    if lum >= TAN_LUM and is_tan(rgb):
        return paper
    if lum >= INNER_LUM or lum <= OUTLINE_LUM:
        return None

    # 외곽선↔띠 사이의 중간 밝기 — 그 혼합선 위에 있을 때만 외곽선↔종이로 다시 섞는다
    axis = band - OUTLINE
    t = float(np.clip((rgb - OUTLINE) @ axis / (axis @ axis), 0.0, 1.0))
    if np.linalg.norm(rgb - (OUTLINE + axis * t)) > MIX_TOLERANCE:
        return None
    return OUTLINE + (paper - OUTLINE) * t


def find_corner_end(src, band_end):
    """안쪽 종이가 본문과 같은 폭에 닿는 첫 행 — 그 위는 둥근 모서리."""
    h, w = src.shape[:2]
    lum = src[..., :3] @ LUM
    body = band_end + 12
    light = np.nonzero((lum[body] >= INNER_LUM) & (src[body, :, 3] > 200))[0]
    left, right = int(light.min()), int(light.max())

    for y in range(band_end):
        if lum[y, left + 2] >= INNER_LUM and lum[y, right - 2] >= INNER_LUM:
            return y
    raise ValueError("모서리 끝을 찾지 못했다 — band_end가 너무 작다")


def in_boxes(x, y, boxes):
    for x0, y0, x1, y1 in boxes:
        if x0 <= x < x1 and y0 <= y < y1:
            return True
    return False


def flatten(name, band_end, keep=()):
    path = SKIN + name + ".png"
    src = np.asarray(Image.open(path).convert("RGBA")).astype(np.float32)
    out = src.copy()
    h, w = src.shape[:2]

    corner_end = find_corner_end(src, band_end)

    def body_row(y):
        phase = y % (2 * ROW_CYCLE)
        return band_end + 6 + (phase if phase < ROW_CYCLE else 2 * ROW_CYCLE - 1 - phase)

    # 띠의 대표색 — 모서리 구역 바로 아래, 가운데가 아닌 열에서 (가운데는 장식이 있을 수 있다)
    band = src[min(corner_end + 4, band_end - 1), int(w * 0.3), :3].copy()

    keep_mask = np.zeros((band_end, w), dtype=bool)
    for x0, y0, x1, y1 in keep:
        keep_mask[y0:min(y1, band_end), x0:x1] = True

    # 2) 띠 구역 — 본문 행 통째 복사 (보존 상자 제외)
    for y in range(corner_end, band_end):
        row = src[body_row(y)]
        cols = ~keep_mask[y]
        out[y, cols] = row[cols]

    # 1)·3) 모서리 구역과 보존 상자 — 띠 색만 종이로
    for y in range(band_end):
        if y >= corner_end and not keep_mask[y].any():
            continue
        paper_row = src[body_row(y), :, :3]
        xs = range(w) if y < corner_end else np.nonzero(keep_mask[y])[0]
        for x in xs:
            if src[y, x, 3] <= 8:
                continue
            # 보존 상자 밖의 띠 구역은 이미 통째로 복사했다
            if y >= corner_end and not keep_mask[y, x]:
                continue
            new = recolor_pixel(src[y, x], paper_row[x], band)
            if new is not None:
                out[y, x, :3] = new

    # 보존 상자의 세로 가장자리 밖은 본문 행이 그대로 들어가 있다 — 상자 안쪽 가장자리에서 띠 색이 남지 않았는지는 미리보기로 확인
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA"), corner_end


def main():
    preview = "--preview" in sys.argv
    if preview:
        os.makedirs(PREVIEW, exist_ok=True)

    for name, params in SPRITES.items():
        image, corner_end = flatten(name, params["band_end"], params.get("keep", ()))
        dst = (PREVIEW if preview else SKIN) + name + ".png"
        image.save(dst)
        print(f"{name:24s} corner_end={corner_end:3d} band_end={params['band_end']:3d} -> {dst}")


if __name__ == "__main__":
    main()
