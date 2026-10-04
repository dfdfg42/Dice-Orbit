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
| 정보 패널 | `UI/InfoPanel/BattleInfoPanelUI.cs` | 우측 사이드바 — 조회 대상의 액티브/패시브/모디파이어 (상태이상은 헤더 한 줄) |
| 선택 컨트롤러 | `UI/InfoPanel/InfoPanelSelectionController.cs` | 호버/클릭 핀 대상 결정 (유닛·타일, 우클릭 해제) |
| 데이터 빌더 | `UI/InfoPanel/UnitInfoBuilder.cs` | Character/Monster/TileData → 표시 데이터 (모디파이어 반영값) |
| 데이터 모델 | `UI/InfoPanel/BattleInfoData.cs` | UnitInfoData/SkillInfoData/PassiveInfoData/TileInfoData... |
| 행 헬퍼 | `UI/InfoPanel/InfoPanelRows.cs` | 항목 그룹(`AddEntry`)·행 생성, 항목 타이포 상수(23/21/19), `OnLight()` 색 보정. 색은 UiSkin |
| 타일 패널 | `UI/InfoPanel/TileInfoPanelUI.cs` | 패널 왼쪽 경계 도킹 — **평평한 패널 한 장**: 맨 위 타일 미리보기(타일 그림 + 그 위 속성 아이콘), 그 아래 속성마다 [아이콘 + 이름 (값·지속)] + 설명. `HasContent`(레벨업 타일 또는 속성 ≥1)일 때만 표시. 2026-10-04 단순화: 그림을 감싸던 액자와 속성마다 따로 뜨던 카드를 없앴다 (패널이 겹겹이 붙어 내용이 묻혔다). 미리보기 그림 자체는 남긴다 — 사용자 결정 |
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
  섹션은 세로 흐름(Body VLG) — 내용 높이만큼 자라고, 비면 통째로 숨겨 아래가 당겨 올라옴. 내용 행만 재생성
```

## 패널 레이아웃 구조 (씬 `battle Info UI/InfoPanelCanvas/Panel`, 2026-09-25 정리)

```
Panel (UiSkinImage Panel, 433×931 우측 도킹)            ← highlightRect(튜토리얼)
 ├ Body  (VerticalLayoutGroup spacing 18, RectMask2D, 인셋 좌30/우28/상42/하30)
 │   ├ Header (VLG 6)
 │   │   ├ NameRow (HLG, expand 안 함) ─ NameChip (HLG 패딩 18/18/5/7) ─ Bg(ignoreLayout, Chip) + NameText 30 볼드
 │   │   ├ HpText 26 볼드            "HP 100/100   방어도 N"
 │   │   └ FlavorText 21 보통 InkMuted  상태이상 요약 (키워드 링크 호버)
 │   ├ Divider (UiSkinImage Divider, 높이 16)
 │   ├ ActivesSection (VLG 8) ─ TitleRow ─ TitleChip(HLG 16/16/4/6) ─ Bg + Title 24 볼드 ("액티브" / 몬스터 "다음 행동")
 │   │                        └ Content (VLG spacing 14, 좌 6)  ← 코드가 Entry(제목 23 볼드 + 메타 19 + 설명 21)를 채움
 │   ├ PassivesSection  (동일)
 │   └ ModifiersSection (동일)  ← 장착 모디파이어 있을 때만
 └ EmptyState (대상 없음 안내)
```

- 슬롯 배선: nameText / hpText / flavorText / activesTitle / activesContainer / passivesContainer / modifiersContainer.
- **칩·카드의 배경 Image는 반드시 `ignoreLayout` 자식(`Bg`)에 둔다.** 레이아웃 그룹과 같은 오브젝트에 두면 Image의 선호 크기(스프라이트 원본)가 최댓값으로 채택돼 칩이 312×106으로 부푼다.
- 씬 구조를 바꾸는 코드 수정은 RunCommand/에디터에서 Body 아래를 재생성하고 슬롯을 재배선한다 (구 `ChipBackground`는 2026-09-25 삭제).

## 표시 규칙

- **호버** = 임시 조회, **좌클릭** = 핀 (유닛/타일 상호 배타), **우클릭** = 핀 해제
- 타게팅 중에는 핀/호버 갱신 중단 (조준 방해 금지)
- 전체화면 UI(보상/모집/상점/이벤트) 동안 `BattleInfoPanelUI.SetVisible(false)` — 타일 패널·월드 인디케이터도 함께 숨김
- 패널 정렬: 사이드바 = 배경 레이어(sortingOrder -5), 커서 툴팁 30000

## 월드 인디케이터 연동 (조회 시 자동)

- 캐릭터 조회 → 패시브 구역 테두리 (`PassiveZoneIndicator`, `IPassiveZoneProvider`). 구역 패시브는 패널의 패시브 제목 옆에 지금 효과 한 줄이 붙는다 (`PassiveInfoData.LiveEffect` — 걸려 있으면 굵은 잉크, 꺼져 있으면 흐린 회색)
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
- 타이포 위계 (2026-09-25): 이름 칩 30 > HP 26 > 섹션 칩 24 > 항목 제목 23 볼드(색: 잉크/Passive/Modifier) > 설명 21 보통 잉크 > 메타 19 InkMuted. 타일 패널 카드는 한 단계 작게 (21/18)
- 레이아웃은 에디터 소유 (editor_owned_ui_pattern.md)
