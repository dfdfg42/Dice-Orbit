# UI 레이아웃·간격 정리 (2026-09-25)

> 상태: **완료 (2026-09-25)** — 사용자 위임("더 좋은 UI나 개선 방향 있으면 그대로 실천") 하에 설계·실행을 한 세션에서 진행. A~L 전부 적용, 재캡처(`tour2_*`, `tour3_*`)로 확인. 같은 날 §3 정보 패널(M~R) 추가 — 캐릭터 조회 캡처 기반. 추가 보정: 상점 카드는 레이아웃 그룹이 자식 크기를 제어하지 않아 `sizeDelta` 직접 지정 + 자동 축소 + 말줄임; 선반 앵커를 y 0.52~0.84 / 0.16~0.48로 정규화하고 제목 칩을 선반 안쪽 상단에; 주사위 버튼 폭 220·fs24.
> 근거: Play 모드 화면 투어 캡처 6장 (`_workspace/2026-09-25-ui-reskin/review/tour_0*.png`, 노드맵·상점·이벤트·전투·보상·결과) + BattleScene 정적 파싱(앵커·크기·글자 크기).
> 관련: 리스킨 스펙 `2026-09-25-ui-reskin-uiskin-higgsfield-design.md`(1~4단계 완료 위에 진행), `editor_owned_ui_pattern.md`.

## 0. 원칙

- 레이아웃만 만진다. 스킨(색·스프라이트)은 UiSkin 그대로.
- 씬 배치 UI는 RunCommand로 RectTransform·TMP만 조정(씬 dirty 가드), 코드 UI는 코드에서 padding/spacing/fontSize 조정.
- 화면 단위 커밋. 수정 후 같은 투어 코루틴으로 재캡처해 전후 비교.
- 정보 위계: 화면당 제목 1개(칩 위 잉크), 본문 24~28, 보조 18~20. 버튼 라벨은 잉크색, 한글.

## 1. 발견 → 수정

| # | 화면 | 문제 (캡처 근거) | 수정 |
|---|---|---|---|
| A | 공통 | 스크림(어두운 반투명) alpha 0.85가 얕아 맵·이벤트에서 뒤의 궤도·캐릭터가 비친다 | `UiSkin.Scrim` alpha 0.85→0.94 (코드 기본값 + 에셋) |
| B | 런 HUD | 상단 골드/포션 칩 배경이 별 장식 패널(주사위 패널 드롭인)이라 모서리에 별이 튄다 | `_RunHudCanvas/TopBar`에 UiSkinImage(Chip) |
| C | 전투 HUD | "Turn: 0"이 맨 왼쪽 위에 맨글자로 떠 있고, 맵·상점·이벤트에도 남는다(영문) | 텍스트 "턴 N"으로, `_RunHudCanvas`의 `TurnChip`(Chip 120×52, HUD 오른쪽)으로 옮기고, `CombatManager`가 전투 시작/종료에 표시/숨김 |
| D | 노드맵 | "Act 1" 제목이 맨 위 보스 노드와 겹친다; 제목이 배경 없이 떠 있다 | `NodeMapUI.verticalMargin` 130→220; 제목을 `TitleChip`(Chip 360×56) 안 잉크 글자로 |
| E | 전투 HUD | 주사위 굴리기/턴 종료 버튼이 1px 간격으로 붙어 있고 라벨이 영문 플레이스홀더("Roll Button") | Roll (-24,82) 200×56, End (-24,14) 200×56 (12px 간격, pivot 1,0 유지); 라벨 "주사위 굴리기"/"턴 종료", fs 26 |
| F | 캐릭터 액션 | 이동(273×112)·취소(293×131) 버튼 크기가 제각각, 취소가 주사위 패널 영역과 겹친다 | 둘 다 260×90; 이동 (-184,-130), 취소 (-184,-235); 라벨 오프셋 0, fs 28 |
| G | 정보 패널 | 우측 패널이 Simple 스트레치라 세로로 늘어나며 모서리·별이 눌린다 | `InfoPanelCanvas/Panel`에 UiSkinImage(Panel) — Sliced |
| H | 이벤트 | 제목·본문이 패널 왼쪽 가장자리에 붙는다(여백 10px); 패널이 Simple 스트레치 | `RightColumn`에 UiSkinImage(Panel); 제목·본문·선택지 컬럼 좌우 여백 30→50, 제목 y -30→-44, 선택지 높이 100→84 |
| I | 상점 | 제목 "상 점"이 런 HUD 칩 뒤에 가려짐; 골드 알약이 HUD 골드와 중복; 선반 제목이 흰 글자로 벽에 묻힘; 상품 카드 5장이 폭 부족으로 ~95px로 찌그러지고 설명 글자가 넘친다; 하단 버튼 좌우 비대칭 | 제목 → `TitleChip`(Chip 220×56, (60,-100)) 잉크 글자; GoldPill 비활성; 선반 제목 → 칩(150×40) 잉크 fs24; 선반 anchorMax.x 0.6→0.68; 카드 168×200, minWidth 160, 제목 fs20/설명 70%/가격 85%; Swap x -660→-640(300폭), Leave 700→640(300폭) |
| J | 보상 | 계속 버튼이 패널 아래 테두리에 걸친다 | 계속 y -265→-245, 강화 패널 취소 -250→-232 |
| K | 결과창 | 제목·문구·버튼이 스크림 위에 맨몸으로 떠 있다 | 코드: Panel(760×440) 뒤판 추가, 제목 Accent, 문구 Ink, 버튼 y -150 |
| L | 에디터 플레이 | BattleScene에서 바로 Play하면 combatUI 참조가 비어 전투 UI가 맵·상점에 남는다(sceneLoaded 미발화) | `GameFlowManager.Start`에서 `CacheSceneReferences()` 1회 호출 |

~~보류(캡처로 확인 못 함): 정보 패널 헤더(이름 fs40/HP fs30/설명 fs24가 93px 안에 적층) — 캐릭터 호버 캡처 후 판단.~~ → §3에서 처리.

## 3. 정보 패널 (오른쪽 사이드바 + 타일 패널) — 2026-09-25 추가

> 근거: Play 투어에서 `ShowUnitExternal`로 전사(모디파이어 2종 장착)·마법사·파란 슬라임을 차례로 표시해 캡처 (`review/info_0*.png` 전 → `review/info2_0*.png` 후). 사용자 지적 "캐릭터 눌렀을 때 오른쪽 정보 여백·배치가 이상하다".

| # | 문제 (캡처 근거) | 수정 |
|---|---|---|
| M | 섹션이 앵커 고정 슬롯(액티브 136px·패시브 190px)이라 내용이 넘치면 RectMask2D에 잘리고, 적으면 빈 칸이 남는다. 몬스터는 패시브가 없는데 "패시브" 칩만 덩그러니 뜬다 | `Panel/Body`(VerticalLayoutGroup, spacing 18, RectMask2D)에 헤더→구분선→액티브→패시브→모디파이어를 세로 흐름으로 쌓음. 섹션은 내용 높이만큼, 비면 통째로 숨김(`SetSectionVisibility(unit, actives, passives, modifiers)`). `modifiersDropY`·`ApplyModifiersDrop`·`statusesContainer`·`paperColor` 제거 |
| N | 텍스트 폭이 407px로 패널(433px) 테두리에 13px까지 붙는다 | Body 인셋 좌 30 / 우 28 / 상 42(모서리 별 장식 회피) / 하 30 → 본문 폭 375. 항목 컨테이너 좌 들여쓰기 6 |
| O | 항목 제목·설명·메타가 전부 24 볼드라 위계가 없다; 이름 40 / HP 30 / 상태 24 이탤릭이 148px 헤더에 적층 | 이름 칩 30 볼드(KOTRA HOPE) → HP 26 볼드 → 상태 줄 21 보통(InkMuted). 섹션 칩 24 볼드. 항목 = `InfoPanelRows.AddEntry` 세로 그룹(제목 23 볼드 + `<size=19>` 흐린 메타, 설명 21 보통 잉크), 항목 사이 14 / 제목↔설명 2 |
| P | 칩 폭을 `ChipBackground`(LateUpdate에서 offsetMax 조정)가 텍스트 선호 폭으로 맞추던 방식 — 흐름 레이아웃과 충돌 | 칩 = HorizontalLayoutGroup(패딩 이름 18/18/5/7, 섹션 16/16/4/6) + 자식 TMP. 배경 스킨 Image는 `ignoreLayout` 자식 `Bg`. `ChipBackground.cs` 삭제 |
| Q | 헤더와 섹션 사이 경계가 없다 | 헤더 아래 `Divider`(UiSkinImage Divider, 높이 16) |
| R | 타일 패널: 맨 일반 타일도 그림만 있는 패널이 뜬다; 속성 카드가 글자 2줄인데 ~150px로 부풀어 있다; 글자 19/15로 정보 패널과 안 맞는다 | `TileInfoPanelUI.HasContent`(레벨업 타일이거나 속성 ≥1)일 때만 표시. 카드 배경 Image를 `ignoreLayout` 자식으로(아래 교훈). 제목 21 볼드 / 설명 18 잉크, 패딩 14/14/10/12 |

**교훈 — 레이아웃 그룹이 있는 오브젝트에 스킨 Image를 같이 두지 말 것.** `Image`는 ILayoutElement로 스프라이트 원본 크기를 선호 크기로 내놓고, 같은 우선순위(0)의 LayoutGroup 값과 **최댓값**이 채택되므로 칩이 312×106, 카드가 150px로 부푼다. 배경은 앵커 스트레치 + `LayoutElement.ignoreLayout` 자식으로 분리한다. (`editor_owned_ui_pattern.md` 체크리스트 7)

## 2. 순서

1. 코드: A(UiSkin), C(CombatManager), K(GameResultUI), I-카드(ShopUI), L(GameFlowManager) → 컴파일·자가 테스트.
2. 씬(RunCommand, dirty 가드): B·C·D·E·F·G·H·I·J → 저장.
3. 재캡처 투어 → 전후 비교 → 커밋(코드 1, 씬 1, 문서 1).

## 4. 인게임 말풍선·행동 라벨·타일 패널 — 2026-09-26 추가

> 근거: 사용자 지적 "몬스터 intent 말풍선 꼬리가 너무 짜부됐다, 공격 스킬명 패널이 너무 크다, 타일 정보 그림은 타일과 같은 이미지를 써야 한다, 타일도 카툰 느낌 3D로 뽑아볼까". 캡처 `review/fix_01_bubble_zoom.png`·`fix_03_tile_panel_zoom.png`·`fix_04_label_zoom.png`·`tile3d_01_battle.png`·`revert_01_battle.png`.

| # | 문제 (캡처 근거) | 결과 |
|---|---|---|
| S | 행동 라벨(`MonsterActionLabel`)이 스킬명 한 줄인데 툴팁 스프라이트 원본 크기로 부푼다 — 배경 Image가 레이아웃 그룹 오브젝트에 같이 있었다(§3 교훈과 같은 원인) | 배경을 `ignoreLayout` 자식 `Bg`로 분리(툴팁 파트, `pixelsPerUnitMultiplier` 2, 그림자), 패딩 10/12/4/4, 간격 6, 아이콘 26, 글자 22 NoWrap. **적용** |
| T | 타일 패널 그림이 스킨 슬롯 폴백이라 인게임 타일과 다르다 | `TileInfoPanelUI.SetTileVisual`이 타일 재질 텍스처(`new cardNormal`) 스프라이트를 Simple·비율 유지로 표시. 씬 `normalTileSprite`/`levelUpTileSprite` 배선, 비면 LogError(폴백 없음). **적용** |
| U | 의도 말풍선 꼬리가 9-slice 가운데 열 압축으로 납작하다 | 몸통·꼬리 스프라이트 분리(`Tail` 자식, Simple)를 시도해 Play 확인까지 했으나 사용자 결정 "말풍선은 원래 거 쓰자" → **되돌림**. 현재 = 단일 `intent_bubble.png` 9-slice(ppuMultiplier 2.2, y 116) |
| V | 타일을 카툰 3D로 | 힉스필드 컨셉 초안 2장(1번 채택) → Tripo image-to-3D(1.44M면 GLB) → 4k면 데시메이트·OBJ 임포트·프리팹 자식 `Visual`(3.56×2.38×4.82, 두께 0.55, 윗면 y 0.03)·루트 BoxCollider·URP 재질·플레이트 y −0.53까지 Play 확인. 정점 단위 최근접 UV 이식 때문에 삼각형 1,201/3,999개가 텍스처 섬을 가로질러 균열 무늬가 생겼고, 재베이크 전에 사용자 결정 "이건 하지 말자" → **전부 되돌림**(프리팹·재질·모델·씬 배선·plateY). 도구는 `_workspace/2026-09-25-ui-reskin/tools/{glb_to_obj,decimate_uv,rotate_obj_y90}.py`에 보존 |

**교훈** — image-to-3D 결과를 데시메이트할 때는 정점 UV 이식이 아니라 새 아틀라스(xatlas)로 텍셀 단위 재베이크가 필요하다. 그리고 타일은 사용자 취향상 2D 카드로 유지한다.
