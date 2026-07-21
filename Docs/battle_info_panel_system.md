# 전투 정보 시스템 (정보 패널 · 타일 패널 · 툴팁)

> 2026-07-05~06 구현. 구 헤비 호버 툴팁(ui_hover_tooltip_system_design.md)을 대체.
> 스펙: `superpowers/specs/2026-07-05-battle-info-panel-design.md`

## 구성 요소 한눈에

```
┌───────────────────────────────┬──────────────┐
│            [타일 패널]─────────┤  정보 패널    │ ← 오른쪽 세로 사이드바 (크림 점수지)
│   (패널 왼쪽 경계에 도킹)       │  이름/HP     │
│                               │  ● 액티브    │
│      (궤도 필드)               │  ● 패시브    │
│   [커서 요약 툴팁]              │  ● 모디파이어 │
│    (유닛 호버 시 커서 옆)       │  ● 상태이상   │
└───────────────────────────────┴──────────────┘
```

| 컴포넌트 | 파일 | 역할 |
|---|---|---|
| 정보 패널 | `UI/InfoPanel/BattleInfoPanelUI.cs` | 우측 사이드바 — 조회 대상의 액티브/패시브/모디파이어/상태이상 |
| 선택 컨트롤러 | `UI/InfoPanel/InfoPanelSelectionController.cs` | 호버/클릭 핀 대상 결정 (유닛·타일, 우클릭 해제) |
| 데이터 빌더 | `UI/InfoPanel/UnitInfoBuilder.cs` | Character/Monster/TileData → 표시 데이터 (모디파이어 반영값) |
| 데이터 모델 | `UI/InfoPanel/BattleInfoData.cs` | UnitInfoData/SkillInfoData/PassiveInfoData/TileInfoData... |
| 행 헬퍼 | `UI/InfoPanel/InfoPanelRows.cs` | 라이트(점수지) 팔레트 상수 + 행 생성. `OnLight()` 색 보정 |
| 타일 패널 | `UI/InfoPanel/TileInfoPanelUI.cs` | 패널 왼쪽 경계 도킹 — 타일 그림(안에 속성 아이콘) + 속성 카드 스택 |
| 커서 요약 툴팁 | `UI/HoverTooltipUI.cs` | 경량 ShowPinned/HidePinned만 — 이름+HP(+몬스터 다음 행동) |
| 키워드 링크 호버 | `UI/InfoPanel/KeywordLinkHover.cs` | 패널 텍스트 속 `<link="kw:...">` 호버 → 커서 옆 정의 툴팁 |
| 상단 HUD | `UI/RunHudUI.cs` | 골드/유물/포션 (런 구조 문서 참고) |

## 데이터 흐름

```
Unit (Character/Monster) : IBattleInfoProvider.GetBattleInfo()
        │  (TileData는 GetTileInfo())
        ▼
UnitInfoBuilder — 여기서 "유효값"을 만든다:
  · 스킬 타겟/설명 = 슬롯 게터 경유 (도화지 패턴 → 모디파이어 반영값)
  · 몬스터 다음 행동 = SimulateCalculation (예상 피해)
  · 모디파이어 목록 = 이름 그룹핑 ×N
        ▼
BattleInfoPanelUI.Render — 0.5s 주기 + 대상 변경 시.
  섹션 위치는 앵커 고정 (숨겨도 안 밀림), 내용 행만 재생성
```

## 표시 규칙

- **호버** = 임시 조회, **좌클릭** = 핀 (유닛/타일 상호 배타), **우클릭** = 핀 해제
- 타게팅 중에는 핀/호버 갱신 중단 (조준 방해 금지)
- 전체화면 UI(보상/모집/상점/이벤트) 동안 `BattleInfoPanelUI.SetVisible(false)` — 타일 패널·월드 인디케이터도 함께 숨김
- 패널 정렬: 사이드바 = 배경 레이어(sortingOrder -5), 커서 툴팁 30000

## 월드 인디케이터 연동 (조회 시 자동)

- 캐릭터 조회 → 패시브 범위 ㄱ자 브래킷 (`PassiveRangeIndicator`, `IPassiveRangeProvider`)
- 몬스터 조회 → 공격 타일 리프트 (`IntentTileLiftEffect`)
- 해제 시 자동 제거. (형태 의미는 battle_visual_language.md)

## 키워드 시스템

- 구 키워드 "섹션"은 철거 — 정의는 **물어볼 때만**:
  `TooltipKeywordFormatter.InsertKeywordLinks()`가 DB 키워드를 `<link>`+밑줄로 래핑
  (긴 키워드 우선 치환 = 이중 래핑 방지, 라이트 배경 색 보정 옵션)
- `KeywordLinkHover`가 등록된 TMP들을 히트테스트 → 호버 시 `HoverTooltipUI.ShowPinned`
- 등록 경로: `InfoPanelRows.AddText(..., linkKeywords: true)` / GlossaryCard 데스크 텍스트

## 스타일 규약

- 정보 패널 = **크림 점수지** (밝은 종이 + 검정 잉크 헤더 + 골드 핍 `●`) —
  보상/상점의 다크 "보드게임의 밤"과 한 세트 (골드 핍이 공통 시그니처)
- DB 색(다크 배경용)은 밝은 배경에서 `InfoPanelRows.OnLight()`로 어둡게 보정
- 레이아웃은 에디터 소유 (editor_owned_ui_pattern.md)
