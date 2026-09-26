# UI 리스킨 생성 로그

> 스펙 §5 재현성 규칙. 모든 힉스필드 호출을 기록한다. 비용은 호출 시점의 응답 기준.

## 참조 이미지 (media_id)

| 이름 | 원본 | media_id |
|---|---|---|
| REF_NODE | Assets/Sprites/노드/node_battle.png | `555522a5-a336-4bad-bfba-a103ae1d7999` |
| REF_TITLE | Assets/Sprites/메인화면/main menu/titleScreenBG.png | `7650ffb6-0a1d-4957-bfdf-2349d520d66c` |
| REF_DICEBTN | Assets/Sprites/UI 이미지/dice button.png | `138acdab-f50c-4077-9a67-7883cb7b2d60` |
| REF_STYLE | 승인된 스타일 타일 (변형 1, job `1f7570c7`) | `5d029aa6-d2ec-4fd7-9a41-ef6e157d2ffe` |

## 생성 기록

| # | 부품 | 모델 / quality / resolution / bg / aspect | 참조 | job_id | 결과 | 비용 |
|---|---|---|---|---|---|---|
| 1 | 스타일 타일 변형 1 | gpt_image_2_5 / low / 1k / opaque / 4:3 | NODE, TITLE, DICEBTN | `1f7570c7-9773-4fa1-9536-dc3c6f578506` | **승인** → `final/style_tile.png` = REF_STYLE | 0.25 |
| 2 | 스타일 타일 변형 2 | 동일 | 동일 | `ba328938-3237-4f8a-a20f-4820e6f7c359` | 탈락 (X 아이콘 분홍, 슬롯 작음) | 0.25 |
| 3 | 스타일 타일 변형 3 | 동일 | 동일 | `394ac562-d3dc-4753-b9b7-8efc324961e7` | 탈락 (선 가늘음) | 0.25 |
| 4 | chip | gpt_image_2_5 / high / 2k / transparent / 21:9 | NODE, STYLE | `33104488-db59-4264-bf28-3b28ce83c882` | 승인 → final/chip.png 392×118 | 2.75 |
| 5 | tooltip | 동일 / 4:3 | 동일 | `8b85d987-5f60-43c8-bfd5-d0e9a685e958` | 승인 → final/tooltip.png 520×210 | 2.75 |
| 6 | slot | 동일 / 1:1 | 동일 | `ec18c2fb-1147-4ad1-9383-1cfb1c023030` | 승인 → final/slot.png 259×264 | 2.75 |
| 7 | icon_potion_empty | 동일 / 1:1 | 동일 | `f33bc8ac-b146-4ae7-9439-9fe7e950165f` | 승인 → final/icon_potion_empty.png 176×264 | 2.75 |
| — | panel, card, divider, icon_close | 동일 | 동일 | 제출 실패 | "Out of credits on basic (monthly) plan" (1차 배치, 2026-09-25 20:28) | 0 |
| — | icon_coin | 동일 | 동일 | 제출 실패 | "429 rate_limit_reached" (1차 배치) | 0 |
| 8 | panel | 동일 / 1:1 | 동일 | `3e880aa6-f23d-42e0-ba16-e9ac7712f7d7` | 승인 → final/panel.png 520×260 (2차 배치) | 2.75 |
| 9 | card | 동일 / 4:3 | 동일 | `98be5519-8b05-427a-85fa-bd1b36cfa362` | 승인 → final/card.png 520×293 (2차 배치) | 2.75 |
| 10 | divider | 동일 / 21:9 | 동일 | `144a7d58-0f1b-42c6-9bff-2d7ca5ccf4c5` | 승인 → final/divider.png 520×30 (2차 배치) | 2.75 |
| 11 | icon_coin | 동일 / 1:1 | 동일 | `d936b520-6c30-408f-be39-65e247bff552` | 승인 → final/icon_coin.png 261×264 (2차 배치) | 2.75 |
| — | icon_close | 동일 | 동일 | 제출 실패 | "429 rate_limit_reached" (2차 배치) | 0 |
| 12 | icon_close | 동일 / 1:1 | 동일 | `975f323f-6aa0-4365-9eda-8a45c8198059` | 승인 → final/icon_close.png 264×259 (3차 단독) | 2.75 |

> 트리밍 교훈: 결과물 캔버스 곳곳에 알파 1~3짜리 잔여 픽셀이 있어 bbox가 부풀려짐 → `trim_png.py`에 알파 임계값 8 도입 (이하 = 완전 투명).

| 13 | button_primary 스트립 (normal/hover/pressed/disabled) | gpt_image_2_5 / high / 2k / transparent / 21:9 | NODE, STYLE | `c6347f47-a754-4f1f-bb91-bb7666dc8a1d` | 승인 → 투명 간격 분할 4장 (392×138~147) | 2.75 |
| 14 | button_secondary 스트립 (normal/hover/pressed/disabled) | 동일 | 동일 | `95cce7db-0bfa-46d8-aa62-a7f25786bee3` | 승인 → 투명 간격 분할 4장 (392×139~154) | 2.75 |

| 15 | 상점 배경 (4단계, `상점/상점 임시배경.png` 드롭인) | gpt_image_2_5 / high / 2k / opaque / 3:2 | STYLE, TITLE, NODE | `c00c6fdf-3579-463c-a8e1-e39d81ac224d` | 채택 → 1536×1024 리사이즈, `상점 임시배경.png` 덮어씀 | 2.75 |

상점 배경 프롬프트(15): `Background illustration for a shop screen in a cute pastel cartoon roguelike: the inside of a witch's laboratory at night used as a small shop. Wooden shelves lined with potion bottles, flasks, jars and rolled scrolls along the left and right sides, a wooden counter across the lower third, warm candle light, a round window showing the night sky and stars at the top center. Thick dark ink outlines, flat pastel colors (cream, lavender, sky blue, pink accents), the same line weight and palette as the reference images. Keep the center of the image uncluttered so a shopkeeper character can stand there. No text, no characters, no UI elements.`

> 4단계 참고: 나머지 드롭인 19장은 힉스필드 없이 제작 — 코어 9-slice 렌더(`tools/render_slice.py`), PIL 도형(`tools/draw_shapes.py`), ONE Mobile POP 글자 합성(`tools/text_label.py`).

버튼 스트립 프롬프트(13·14 공통, 색만 다름): `Single 2D game UI element set, centered, no text, transparent background, soft pastel cartoon, flat vector, no gradients, no glow, no drop shadow. Same line weight and outline style as the reference images. Item: four states of the SAME pill button laid out in one horizontal row, left to right, equal size, with wide clear transparent gaps between them: (1) normal: pastel pink fill (#FFA6F2) [secondary: pastel sky blue fill (#A6DDFF)], thick ink outline (#4B425C), a one-tone darker bottom edge line; (2) hover: same shape but slightly brighter fill and a thicker outline with a thin white inner ring; (3) pressed: same shape shifted down slightly, bottom edge line gone, fill a touch darker; (4) disabled: desaturated gray paper fill (#D9D4CB), dashed ink outline, no bottom edge line. All four identical in width and height, evenly spaced. No text on the buttons. Take the pink [blue] button row exactly as drawn in the reference style sheet.`

> 1차 배치 교훈: 9건 동시 제출 시 "Out of credits" 오탐 5건 + 429 1건. 잔액은 실제 성공분만 차감됨(68.25). 이후 배치는 4~5건씩.

스타일 타일 프롬프트(1~3 공통): `Game UI style sheet for a cute pastel cartoon roguelike, neatly arranged on a single sheet with generous spacing on a plain flat light gray background. Items: (1) a large rounded rectangle panel with tiny star ornaments only at its top corners, (2) a smaller content card with a slightly tighter corner radius and no ornament, (3) a small pill-shaped label chip in a slightly deeper cream (#E8DBC3) with a thin outline, (4) a rectangular tooltip box without a tail, (5) an empty inset square slot with a subtle inner ring, (6) a thin horizontal divider line with a small star at its center, (7) a pill button filled with pastel pink (#FFA6F2) shown in four states side by side: normal, hover (slightly brighter, thicker outline), pressed (pushed down, bottom edge line gone), disabled (desaturated gray paper, dashed outline), (8) the same four states of a pill button filled with pastel sky blue (#A6DDFF), (9) three icons: a gold coin, an empty potion bottle outline, a bold close X. All items share: cream paper fill (#FAF3E0), thick dark ink outline (#4B425C), rounded corners, a one-tone darker bottom edge line like a slightly raised paper card, flat 2D vector look, no gradients, no glow, no drop shadows, no text or letters anywhere. Match the line weight and outline style of the reference battle icon; take the pastel palette from the reference night sky and the pink from the reference button.`

## 프롬프트 공통 접두 (스펙 §5)

`Single 2D game UI element, centered, no text, transparent background, cream paper fill (#FAF3E0), thick dark ink outline (#4B425C), rounded corners, a one-tone darker bottom edge line like a slightly raised paper card, soft pastel cartoon, flat vector, no gradients, no glow, no drop shadow. Same line weight and outline style as the reference images.`

## 누적 비용

| 시점 | 누적 크레딧 | balance 응답 |
|---|---|---|
| 시작 | 0 | 80 |
| 스타일 타일 3변형 후 | 0.75 | (미조회) |
| 코어 1·2차 배치 중간 (고화질 4장) | 11.75 | 68.25 |
| 2단계 생성 종료 (고화질 11장 = 코어 9 + 버튼 스트립 2) | 31 | 49 |
| 4단계 상점 배경 1장 후 (드롭인 나머지 19장은 로컬 제작) | 33.75 | 46.25 |

## 몬스터 영역 표시 리스킨 (2026-09-25 밤) — 스펙 `2026-09-25-monster-zone-intent-reskin-design.md`

참조 추가: `REF_ZONE_SHAPE` = PIL 실루엣(`refs/zone_plate_ref.png`, `tools/zone_silhouette.py`) → media_id `53d05b97-6812-49ce-a171-3e38a5f159e2`. 스타일 참조는 REF_STYLE 재사용(만료 없이 동작).

| # | 항목 | 모델/품질 | 참조 | job id | 결과 | 비용 |
|---|---|---|---|---|---|---|
| 13 | 말풍선 초안 ×2 | gpt_image_2_5 / low / 1k / transparent / 1:1 | STYLE | `994de9b5`, `4b794205` | 2번 승인 | 0.5 |
| 14 | 플레이트 초안 ×2 | 동일 | ZONE_SHAPE, STYLE | `4113bf99`, `ec37e2d5` | 1번 승인 (실루엣 IoU 0.94) | 0.5 |
| 15 | 플레이트 최종 | high / 2k / transparent | ZONE_SHAPE, STYLE, 초안 job 4113bf99 | `8ace3395-642d-421c-9e0c-72b1b5f5343d` | `final/zone_plate.png` 2048² — `finalize_zone_plate.py`로 실루엣 마스킹(종이 평균 L 0.92 → 0.94) | 2.75 |
| 16 | 말풍선 최종 | 동일 | STYLE, 초안 job 4b794205 | `bbdf7466-abfc-44c3-ac15-fc03bce7d22b` | `final/intent_bubble.png` 560×352, 꼬리 41px, 9-slice L84 B88 R84 T47 | 2.75 |

소계 6.5 → 누적 40.25, `balance` 39.75 (basic).

교훈: 정확한 기하가 필요한 데칼은 PIL 실루엣을 참조로 넣고 결과를 그 실루엣으로 마스킹하면 모델 편차와 무관하게 게임 기하와 맞는다. 9-slice 경계가 렌더 크기보다 크면 `Image.pixelsPerUnitMultiplier`로 경계를 줄인다(말풍선 2.2).

## 모집 상세·배경 리스킨 (2026-09-26) — 스펙 `2026-09-26-recruit-detail-reskin-design.md`

참조 추가: `REF_SHOP_BG` = `Assets/Sprites/상점/상점 임시배경.png` → `ed754fc6-7194-476f-8e9f-c50bfecdeaea`, `REF_RECRUIT_OLD` = `SelectScreen2.png` → `14c76fc6-b7d4-4ee8-a3b5-5e75e4402833`.

| # | 항목 | 모델/품질 | 참조 | job id | 결과 | 비용 |
|---|---|---|---|---|---|---|
| 17 | 배경 초안 ×2 (선반 위주) | gpt_image_2_5 / low / 1k / 16:9 | SHOP_BG, STYLE | `c1125795`, `f3c8a71b` | 사용자: "기존 배경을 참조해 달라" → 재생성 | 0.5 |
| 18 | 배경 초안 ×2 (옛 구도 참조) | 동일 | RECRUIT_OLD, SHOP_BG, STYLE | `020c9fce`(3번), `e04a065e`(4번) | 4번 채택 | 0.5 |
| 19 | 배경 최종 (3번) | high / 2k / 16:9 / opaque | + job 020c9fce | `d592a9a1-e314-4fa1-9a47-f5dedd9d79af` | **폐기** (사용자가 4번으로 정정) | 2.75 |
| 20 | 배경 최종 (4번) | 동일 | + job e04a065e | `f5df3944-0a7a-41ea-aade-b4e745b9d314` | `Assets/Sprites/캐릭터 선택화면/recruit_bg.png` 2688×1520 → 임포트 2048 | 2.75 |

소계 6.5 → 누적 46.75. 교훈: 초안 선택 답변은 번호를 되물어 확정한 뒤 고해상을 뽑을 것(3번 2.75 낭비).

## 모집 유리병 (2026-09-26) — 스펙 `2026-09-26-recruit-bottles-design.md`

| # | 항목 | 모델/품질 | 참조 | job id | 결과 | 비용 |
|---|---|---|---|---|---|---|
| 21 | 유리병 초안 ×2 | gpt_image_2_5 / low / 1k / 2:3 / transparent | STYLE | `c4394f8f`(1번), `2413a13e`(2번) | 1번 채택 | 0.5 |
| 22 | 유리병 최종 | high / 2k / 2:3 / transparent | STYLE + job c4394f8f | `364429c5-2a2a-431a-b5de-54abc9cf5d85` | `Assets/Sprites/캐릭터 선택화면/bottle_ink.png` 687×1208 (알파 트림, 최대 변 1200) | 2.75 |

소계 3.25 → 누적 50.0, `balance` 30 (basic).
