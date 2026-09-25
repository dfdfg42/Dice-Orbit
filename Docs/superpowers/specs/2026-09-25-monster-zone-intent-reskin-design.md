# 몬스터 영역 표시 리스킨 — 구역 플레이트 + 의도 말풍선 (2026-09-25)

> 상태: **설계 승인, 구현 대기** — 브레인스토밍 답변(구역 "낙서 같다" → 잉크 플레이트 / 말풍선은 아이콘만 크게 / 공격 타일은 색만 진하게)으로 확정.
> 근거 캡처: `_workspace/2026-09-25-ui-reskin/review/intent_zoom_sheet.png` (전투 중 구역 브래킷·말풍선·공격 타일), `mock_tile_stamp.png`(기각된 도장 안).
> 관련: `battle_visual_language.md`(의미→형태 표, 갱신 대상), `2026-09-25-ui-reskin-uiskin-higgsfield-design.md`(스킨 시스템·힉스필드 요령), `2026-08-21-auto-combat-redesign-design.md` §2(구역 바닥색 = 주인 색 원안).

## 0. 문제

| 요소 | 지금 | 문제 |
|---|---|---|
| 구역 표시 `Visuals/ZoneFloorRenderer.cs` | 구역당 LineRenderer 3개 — 중앙 V자 + 바깥 모서리 ㄱ자 2개, 주인 몬스터 색 | 낙서처럼 보이고 구역이 어디까지인지 안 읽힌다 (사용자 지적) |
| 의도 말풍선 `UI/MonsterUI.cs` (TestMonster.prefab MonsterCanvas/IntentIcon) | 흰 사각 말풍선(`attack_icon.png`) 55×46, 스킬 아이콘 작음, 라벨 fs 8 | 아이콘·글자가 안 읽히고 크림 종이·잉크선 스타일과 안 맞는다 |
| 공격 예정 타일 `Visuals/MonsterTileColorOverlayManager.cs` | 주인 색 밴드, 알파 0.55 | 살구색이 연해 위협이 약하다 |

## 1. 결정

- **구역 = 잉크 플레이트.** 힉스필드로 사분면 조각(중앙 근처 → 타일 바깥 전체) 플레이트 1장을 생성해 바닥에 눕히고, 주인 색으로 틴트한다. 러그 일러스트가 가려져도 된다(사용자 확인). V자·ㄱ자 LineRenderer는 철거.
- **말풍선 = 크림 말풍선 + 아이콘만.** 타입 라벨(공격/특수)은 없앤다. 아이콘을 키운다.
- **공격 타일 = 새 에셋 없이 색 밴드 알파 0.55 → 0.8.** 잉크 도장 안은 기각.
- 시각 언어 원칙 유지: 주인 색은 플레이트·발밑 마커·밴드·조준선에 공통(누가), 형태가 의미(어디·무엇).

## 2. 구역 플레이트

### 2.1 에셋 `Assets/Sprites/UI Skin/zone_plate.png`

- **형태**: 사분면 조각. 궤도 중심이 원점, 안쪽 반지름 2.5, 바깥 반지름 16.0(월드 유닛), 각도 2.5°~87.5°(양끝 2.5° 들임 — `CombatZoneManager`의 구역 폭 90°에 이웃과 안 겹치게), 모서리 반경 0.6 둥글림.
- **캔버스**: 2048×2048, **1유닛 = 128px**, 원점 = 캔버스 좌하단, 조각은 +x(오른쪽)·+z(위) 사분면. 스프라이트 pivot (0,0), PPU 128 → 월드 크기 16×16.
- **스타일**: 크림 종이(흰색에 가깝게 — 틴트용), 가장자리 안쪽 굵은 잉크 외곽선(~0.35유닛 = 45px), 안쪽에 옅은 점·해칭 결. 모서리 별 장식 없음(4번 반복되면 시끄럽다). 투명 배경.
- **생성**: gpt_image_2_5. 참조 = 승인된 스타일 타일(REF_STYLE `5d029aa6-…`) + PIL로 그린 정확한 실루엣(흰 조각/투명). 초안 low·1k 2장(0.25×2) → 승인 → high·2k·투명(2.75).
- **후처리**: 실루엣 마스크로 알파 클램프(실루엣 밖 0) → 종이 영역 밝기 정규화(평균 L≈0.94, 잉크선은 유지) → PNG. 임포트: Sprite, PPU 128, pivot (0,0), Full Rect, Mipmap+Trilinear.
- **틴트**: `SpriteRenderer.color` = 주인 색을 채도 0.35·명도 1.0으로 완화, 알파 0.85. 중립 구역 = (0.78, 0.78, 0.84, 0.5). 잉크선은 곱해도 어둡게 남는다.

### 2.2 렌더러 `Visuals/ZonePlateRenderer.cs` (`ZoneFloorRenderer` 대체)

- 구역당 `SpriteRenderer` 1개 (`_ZonePlate{n}`), 스프라이트 = `UiSkin.Current.ZonePlate`.
- 위치 (0, −0.02, 0) — 타일(불투명 URP/Lit, y 0~0.03) 아래라 타일이 그 위에 올라앉는다. 회전 `Quaternion.Euler(90, −startDeg, 0)`: 눕힌 뒤 구역 시작각(`GetZoneAngularRangeDeg`, 구역 0 = −9°)만큼 반시계로 돌린다. 캡처로 구역 0 플레이트가 우상단 타일 5장 아래 오는지 확인.
- 정렬: 기존 `SyncSortingBehindUnits`와 같은 규칙(유닛 스프라이트 레이어, order −200).
- 등장·갱신: 기존 로직 재사용 — 주인 몬스터가 팝인으로 드러난 뒤(`IsMonsterRevealed`) 표시, 중립은 전원 드러난 뒤. 주인 변경·가시성 변화만 반영(InstanceID 캐시). 표시 전환 시 알파 0.25s 페이드.
- `ZoneFloorRenderer.cs` 삭제, 호출부(`EnsureInstance`)를 `ZonePlateRenderer`로 교체. 시각 언어 사전 표 갱신 + 기각 목록에 "브래킷 구역 표시(낙서처럼 읽힘, 2026-09-25)".

### 2.3 스킨 연결

- `UiSkin`에 `[Header("월드 데칼")] Sprite ZonePlate`, `[Header("몬스터")] Sprite IntentBubble` 추가. 점검(`UiSkinValidator`)·자가 테스트(빈 스킨 이슈 수 18 → 20) 갱신. 색은 기존 팔레트만 쓴다.

## 3. 의도 말풍선

### 3.1 에셋 `Assets/Sprites/UI Skin/intent_bubble.png`

- 크림 종이 말풍선, 굵은 잉크 외곽선, **아래 중앙 꼬리**, 9-slice. 생성 1024 → 트림. 9-slice 경계: 좌·우·상 ≈ 폭의 15%, 하 = 꼬리 높이 + 15% (꼬리는 늘어나지 않는 하단 캡에 포함).
- 생성: 초안 low 2장(0.5) → high·2k·투명(2.75). 참조 REF_STYLE.

### 3.2 프리팹 `Assets/Prefabs/TestMonster.prefab` MonsterCanvas

- `IntentIcon`(구 흰 말풍선) → `IntentBubble`(Image, Sliced `UiSkin.IntentBubble`, 92×84, HP바 위 중앙: x = HPBar.x(2.95), y = 78) 안에 `Icon`(Image 56×56, 꼬리 위 중앙, y +6). `IntentText`·자식 `Image`(빈 스프라이트) 삭제.
- `MonsterUI` 슬롯: `intentBubbleRoot = IntentBubble`, `intentBubbleBg = IntentBubble Image`, `intentIcon = Icon`, `intentText` 제거. 라벨 기능(`showIntentText`, `GetIntentLabel`, `intentText`)은 다른 사용처가 없으면 코드에서 제거(구조 > 레거시). `tintBubbleByIntent`는 false 유지(말풍선은 크림 고정), `neutralBubbleColor` 필드 제거 → 스킨 스프라이트 원색.
- 아이콘 없는 의도(`intent.Icon == null`): 아이콘 숨김 + `LogError` 1회(폴백 금지 — 스킬 아이콘 80장이 이미 있으므로 데이터 누락으로 취급).
- 팝 애니메이션(`PlayIntentPop`) 유지. 타일 위 소형 말풍선 `FloatingIntentUI`(프리팹·스크립트·`MonsterAttackIntentManager.floatingIntentUIPrefab`)는 이번 범위 밖 — 그대로 둔다.

## 4. 공격 예정 타일

- `MonsterTileColorOverlayManager.overlayAlpha` 기본값 0.55 → **0.8** (씬·프리팹에 직렬화된 값이 있으면 함께 교정). 그 외 변경 없음. 호버 시 붉은 외곽선(`MonsterThreatOutline`)은 그대로.

## 5. 검증

- Play 투어 코루틴: 모집 스킵 → 전투 진입 5초 후 캡처(구역 플레이트·말풍선·밴드) + 몬스터 `ShowUnitExternal` 캡처(호버 외곽선과의 공존) → `review/zone_0*.png` 전후 비교.
- 구역 0 플레이트 방향 확인(우상단 타일 아래). 유닛·타일·발밑 마커보다 뒤에 그려지는지 확인.
- 콘솔 에러 0, 「UI 스킨 점검」 0, `UiSkinSelfTests.RunAll()` PASS.

## 6. 비용

플레이트 3.25 + 말풍선 3.25 = **약 6.5 크레딧** (잔액 46.25 → 약 39.75). 배치 제출은 2건씩.

## 7. 범위 밖 (다음 라운드)

발밑 원 마커 리스킨, 몬스터 HP바·이름표 스타일, 타일 위 소형 말풍선(`FloatingIntentUI`) 정리, 모집(캐릭터 선택) 화면 리스킨.
