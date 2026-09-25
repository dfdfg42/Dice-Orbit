# UI 레이아웃·간격 정리 (2026-09-25)

> 상태: **완료 (2026-09-25)** — 사용자 위임("더 좋은 UI나 개선 방향 있으면 그대로 실천") 하에 설계·실행을 한 세션에서 진행. A~L 전부 적용, 재캡처(`tour2_*`, `tour3_*`)로 확인. 추가 보정: 상점 카드는 레이아웃 그룹이 자식 크기를 제어하지 않아 `sizeDelta` 직접 지정 + 자동 축소 + 말줄임; 선반 앵커를 y 0.52~0.84 / 0.16~0.48로 정규화하고 제목 칩을 선반 안쪽 상단에; 주사위 버튼 폭 220·fs24.
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

보류(캡처로 확인 못 함): 정보 패널 헤더(이름 fs40/HP fs30/설명 fs24가 93px 안에 적층) — 캐릭터 호버 캡처 후 판단.

## 2. 순서

1. 코드: A(UiSkin), C(CombatManager), K(GameResultUI), I-카드(ShopUI), L(GameFlowManager) → 컴파일·자가 테스트.
2. 씬(RunCommand, dirty 가드): B·C·D·E·F·G·H·I·J → 저장.
3. 재캡처 투어 → 전후 비교 → 커밋(코드 1, 씬 1, 문서 1).
