# 모집(캐릭터 선택) 상세 화면 + 배경 리스킨 (2026-09-26)

> 상태: **구현 완료 (2026-09-26)** — 계획 `2026-09-26-recruit-detail-reskin.md`. 실행 중 보정: (1) `SelectedBottleAnchor`가 CardContainer 자식이라 `GenerateRandomChoices`가 파괴하고 HLayout이 위치를 덮어써 고른 병이 제자리에 머물렀음 → 캔버스 직속으로 옮기고 카드 목표는 `cardContainer.InverseTransformPoint(anchor.position)`; (2) 배경은 사용자 요청으로 "기존 배경 구도 참조" 초안(4번)으로 재생성. 비용: 초안 1.0 + 고해상 2회(3번 폐기·4번 채택) 5.5 = 6.5.
> 사용자 위임("유리병 이후 선택 화면이랑 배경을 네가 생각해서 바꿔봐"). 유리병 3개 선택지 화면 자체는 범위 밖.
> 근거 캡처: `_workspace/2026-09-25-ui-reskin/review/sel_01_cards.png`(병 3개), `sel_02_detail.png`(상세), `recruit_assets_sheet.png`(기존 에셋 43장).
> 관련: `2026-09-25-ui-reskin-uiskin-higgsfield-design.md`(스킨), `battle_info_panel_system.md`(상세 패널이 따르는 타이포·칩 문법), `editor_owned_ui_pattern.md` 체크리스트 7(레이아웃 그룹 + Image 함정).

## 0. 문제

| 요소 | 지금 (`RecuritUI` 캔버스, `UI/CharacterSelectionUI.cs`, `UI/CharacterCard.cs`) | 문제 |
|---|---|---|
| 배경 `BackGround` | `SelectScreen2.png` 파란 연구실 일러스트 | 어둡고 차가워 크림 스킨·상점 배경(따뜻한 마녀 연구실)과 분위기가 끊긴다 |
| 상세 `DetailRoot/LeftDescriptionPanel` | 화면 폭 전체 어두운 띠(`Group 5 (1).png` + 검정 0.745 그늘) 위 흰 글자, 마젠타 「패시브/액티브」 라벨 이미지, 왼쪽 아래 선택·취소 | 스킨과 안 맞고 위계가 없다. 고른 유리병은 띠 뒤에 가려 안 보인다 |
| 캔버스 | `CanvasScaler` ConstantPixelSize(800×600 참조) | 1080p 외 해상도에서 배치가 깨진다 |

## 1. 결정

- **배경 = 힉스필드 생성** "밤의 마녀 연구실 선반": 상점 배경과 같은 결(따뜻한 촛불빛, 크림·라벤더 파스텔, 굵은 잉크선). 가운데 3분의 1은 유리병 3개가 올라설 빈 나무 선반, 글자·캐릭터·UI 없음. 새 파일 `Assets/Sprites/캐릭터 선택화면/recruit_bg.png`(2k 16:9), 옛 파일은 보존.
- **상세 = 크림 종이 캐릭터 시트**: 어두운 띠·그늘·라벨 이미지 철거. 전투 정보 패널의 문법(이름 칩 → HP → 구분선 → 섹션 칩 + 본문)을 그대로 쓴다. 고른 유리병은 왼쪽에 보이게 둔다.
- **캔버스** = ScaleWithScreenSize 1920×1080, match 0.5 (1080p에서는 픽셀 동일).
- 유리병 선택지 화면(병 알파·정보 없음)은 이번 범위 밖.

## 2. 레이아웃 (캔버스 1920×1080, 중심 원점)

```
RecuritUI (Canvas, ScaleWithScreenSize 1920×1080)
 ├ BackGround (stretch, recruit_bg.png)
 ├ CardContainer (기존)
 │   └ SelectedBottleAnchor  anchoredPosition (−331, 116)  ← 고른 병이 화면 x≈230, y 중앙에 서도록
 └ DetailRoot  (0, 0)
     ├ Panel  (40, −20) 980×620, UiSkinImage Panel — 코드가 세로 언폴드(scale y 0→1)
     │   └ Body (VLG spacing 18, 인셋 좌40/우40/상40/하110)      ← 아래 110은 버튼 자리
     │       ├ Header (VLG 6)
     │       │   ├ NameRow (HLG) ─ NameChip (HLG 패딩 20/20/6/8, Bg ignoreLayout Chip) ─ NameText 36 볼드 KOTRA HOPE
     │       │   └ StatText 28 볼드 Danger        "HP 100"
     │       ├ Divider (UiSkinImage Divider, 높이 16)
     │       ├ ActiveSection (VLG 8) ─ TitleRow ─ TitleChip(패딩 16/16/4/6) ─ Title 26 볼드 "액티브"
     │       │                        └ ActiveText 22 보통 잉크 (wrap)
     │       └ PassiveSection (동일) ─ "패시브" / PassiveText 22
     │   ├ CancelButton  (150, −255) 220×64, UiSkinImage ButtonSecondary, 라벨 "취소" 26 볼드 잉크
     │   └ SelectButton  (400, −255) 220×64, UiSkinImage ButtonPrimary,   라벨 "선택" 26 볼드 잉크
     └ LDIllust (720, 20) 560×748 (752×1003 비율 유지), 패널 오른쪽 가장자리에 살짝 겹침
```

- 버튼은 Panel의 자식이라 패널과 함께 펼쳐진다(기존 CancelButton/SelectButton은 DetailRoot 직속이었음).
- 칩·버튼 배경 Image는 레이아웃 그룹과 다른 오브젝트(`Bg`, ignoreLayout)에 둔다 — 체크리스트 7.
- 슬롯 재배선: `detailNameText`=NameText, `detailStatsText`=StatText, `detailActiveText`=ActiveText, `detailPassiveText`=PassiveText, `detailDescriptionText`=null(통합 설명 미사용), `cancelButton`, `ldConfirmButton`, `ldIllustrationImage`=LDIllust.

## 3. 코드 (`UI/CharacterSelectionUI.cs`)

- `_leftPanel` = `detailRoot.transform.Find("Panel")` (구 "LeftDescriptionPanel"). 언폴드 연출 유지.
- `EnsureTopShade`·`_topShade`·`topShadeStrength` 삭제(크림 패널에 검정 그늘은 무의미).
- `BuildPassiveSummary`: 이름을 `<color=#세이지>` 볼드로, 본문은 그대로 — 정보 패널의 패시브 색과 동일(`UiSkin.Passive`). `BuildActiveSummary`는 그대로.
- 폴백 금지: 슬롯이 비면 `LogError`(기존 null 가드는 유지하되 조용히 넘어가지 않게 Start에서 1회 검사).

## 4. 생성

- gpt_image_2_5, 참조 = REF_STYLE + 상점 배경(`Assets/Sprites/상점/상점 임시배경.png` 재업로드). 초안 low·1k·16:9 ×2(0.5) → 승인 → high·2k·16:9·opaque(2.75). 예상 3.25(잔액 39.75 → 36.5).
- 임포트: Sprite, Mipmap+Trilinear, 최대 2048.

## 5. 검증

Play 투어: 모집 화면 자동 표시 2초 후 캡처(배경) → 첫 카드 Select 클릭 → 1.2초 후 상세 캡처 → 전후 비교. 콘솔 에러 0, 스킨 점검 0.

## 6. 범위 밖

유리병 선택지(알파 0.3·정보 없음), 병 애니메이션, 캐릭터 창 일러스트 교체.
