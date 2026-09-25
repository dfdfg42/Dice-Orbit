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

스타일 타일 프롬프트(1~3 공통): `Game UI style sheet for a cute pastel cartoon roguelike, neatly arranged on a single sheet with generous spacing on a plain flat light gray background. Items: (1) a large rounded rectangle panel with tiny star ornaments only at its top corners, (2) a smaller content card with a slightly tighter corner radius and no ornament, (3) a small pill-shaped label chip in a slightly deeper cream (#E8DBC3) with a thin outline, (4) a rectangular tooltip box without a tail, (5) an empty inset square slot with a subtle inner ring, (6) a thin horizontal divider line with a small star at its center, (7) a pill button filled with pastel pink (#FFA6F2) shown in four states side by side: normal, hover (slightly brighter, thicker outline), pressed (pushed down, bottom edge line gone), disabled (desaturated gray paper, dashed outline), (8) the same four states of a pill button filled with pastel sky blue (#A6DDFF), (9) three icons: a gold coin, an empty potion bottle outline, a bold close X. All items share: cream paper fill (#FAF3E0), thick dark ink outline (#4B425C), rounded corners, a one-tone darker bottom edge line like a slightly raised paper card, flat 2D vector look, no gradients, no glow, no drop shadows, no text or letters anywhere. Match the line weight and outline style of the reference battle icon; take the pastel palette from the reference night sky and the pink from the reference button.`

## 프롬프트 공통 접두 (스펙 §5)

`Single 2D game UI element, centered, no text, transparent background, cream paper fill (#FAF3E0), thick dark ink outline (#4B425C), rounded corners, a one-tone darker bottom edge line like a slightly raised paper card, soft pastel cartoon, flat vector, no gradients, no glow, no drop shadow. Same line weight and outline style as the reference images.`

## 누적 비용

| 시점 | 누적 크레딧 | balance 응답 |
|---|---|---|
| 시작 | 0 | 80 |
