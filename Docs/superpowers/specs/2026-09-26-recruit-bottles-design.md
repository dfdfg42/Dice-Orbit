# 모집 유리병 선택지 화면 정리 (2026-09-26)

> 상태: **설계 승인, 구현 중** — 사용자 위임. 앞선 `2026-09-26-recruit-detail-reskin-design.md`(상세 화면·배경)의 후속.
> 근거 캡처: `review/recruit_03_cards.png`(새 배경 위 병 3개 — 호버 전 알파 0.3으로 묻히고 이름·HP 없음, 병이 카운터 아래까지 내려옴).

## 1. 결정

| 항목 | 지금 | 변경 |
|---|---|---|
| 병 가시성 (`UI/CharacterCard.cs`) | CanvasGroup 0.3 / 그림 알파 0.5 하드코딩 | `restingGroupAlpha` 0.85·`hoverGroupAlpha` 1.0 필드로, `normalAlpha` 0.9, `hoverScale` 1.06. 흐림(0.25)은 유지 |
| 병 크기·위치 (`Prefabs/CharacterCard.prefab`, `RecuritUI/CardContainer`) | SelectButton 565×891 at (8,100), Portrait 300×300 ×1.33, 컨테이너 (−399,−116) | SelectButton **384×606 at (0,40)**, Portrait ×0.9 at (0,0), 컨테이너 **(−400, 23)** → 병 바닥이 카운터 상판(화면 y≈780)에 닿고 560·960·1360에 선다 |
| 정보 | 없음 | 카드 프리팹에 `NameChip`(HLG 패딩 16/16/4/6 + Bg ignoreLayout Chip + `NameText` 28 볼드 KOTRA) at (0,−320), `StatsText` "HP 100" 20 InkMuted at (0,−372). 기존 `nameText`/`statsText` 슬롯 배선 |
| 제목 | 없음 | `RecuritUI/TitleChip`(HLG 패딩 24/24/6/8 + Bg Chip + `Title` 32 볼드) at (0, 470). `CharacterSelectionUI.titleText` 슬롯, `UpdateTitle()` = "동료를 고르세요 · n/m" (1명일 땐 숫자 없음) |
| 병 그림 | `empty_bottle 2.png` (얇은 선·그라데이션) | 힉스필드 생성 `Assets/Sprites/캐릭터 선택화면/bottle_ink.png` — 투명 유리, 굵은 잉크선, 옅은 하늘색, 하이라이트 2줄. 프리팹 SelectButton Image 교체. 초안 low ×2(0.5) → high 2k(2.75) |

- 호버 시 캐릭터 외곽선 스프라이트 교체·확대는 그대로.
- `SelectedBottleAnchor` (−730,0) 그대로 — 병이 작아져 상세 시트 왼쪽에 여유 있게 선다.

## 2. 검증

Play: 모집 화면 2초 후 캡처(병 3개 + 이름 칩 + 제목) → 첫 병 선택 → 상세 캡처(작아진 병이 왼쪽). 콘솔 에러 0.
