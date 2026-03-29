# UI Hover/Tooltip System 설계 및 구현 기록

작성일: 2026-03-29  
범위: 캐릭터/몬스터/타일 Hover 정보, 키워드 패널, 상태이상 패널, ScriptableObject 기반 키워드 DB

---

## 1) 목표와 배경

기존 Hover UX는 대상 정보를 단일 텍스트 블록으로 출력하는 구조였고, 다음 문제가 있었습니다.

- 정보가 길어질수록 가독성 저하
- 타일/몬스터/캐릭터별 표시 방식이 혼재
- 키워드 설명이 본문에 섞여 유지보수 어려움
- 상태이상(예: `Honey`/`꿀`) 설명이 컨텍스트별로 일관되지 않음

이에 따라 **Slay the Spire 스타일에 가까운 분리형 패널 UX**로 개편했습니다.

---

## 2) 설계 원칙

1. **단일 Hover 진입 경로**
   - `HoverTooltipUI`가 Raycast로 `IHoverTooltipProvider`를 조회해 표시를 오케스트레이션
2. **정보 패널 분리**
   - 본문 정보(Main Tooltip)
   - 키워드 설명(Keyword Glossary Panel)
   - 상태이상 설명(Status Glossary Panel)
3. **데이터 주도 확장**
   - 키워드 설명/색상은 `TooltipKeywordDatabase`(ScriptableObject)에서 로드
4. **호환성 유지**
   - 기존 API(`AppendKeywordSection`)는 유지하되 본문 확장은 비활성화

---

## 3) 컴포넌트 구조

### 3.1 `HoverTooltipUI`
파일: `Assets/Scripts/UI/HoverTooltipUI.cs`

역할:
- Hover 대상 감지(`IHoverTooltipProvider`)
- 본문 툴팁 표시/위치 추적
- 키워드 패널, 상태 패널 표시 트리거
- (기존 유지) 링크 기반 키워드 상세 패널 고정/해제 인터랙션

핵심 흐름:
1. Provider에서 `GetHoverTooltipText()` 수신
2. `TooltipKeywordFormatter.FormatMainTooltipText(...)`
3. `ExtractStatuses(...)`로 상태이상 라인 분리
4. 본문은 상태 라인 제거된 텍스트만 표시
5. `ExtractMatches(...)`로 키워드 목록 추출
6. `KeywordGlossaryPanelUI.Show(...)` 호출
7. `StatusGlossaryPanelUI.Show(...)` 호출

### 3.2 `KeywordGlossaryPanelUI`
파일: `Assets/Scripts/UI/KeywordGlossaryPanelUI.cs`

역할:
- 본문에 등장한 키워드를 별도 패널에 렌더
- 키워드명(색상 적용) + 설명 목록 표시
- 본문 패널의 우측(화면 경계 고려) 배치

### 3.3 `StatusGlossaryPanelUI`
파일: `Assets/Scripts/UI/StatusGlossaryPanelUI.cs`

역할:
- 상태이상 목록(스택/지속턴/설명) 전용 패널 렌더
- 본문 패널 하단에 별도 배치
- 상태가 없으면 자동 숨김

### 3.4 `TooltipKeywordDatabase`
파일: `Assets/Scripts/UI/TooltipKeywordDatabase.cs`

역할:
- 키워드 정의를 ScriptableObject로 관리
- 필드: `key`, `description`, `color`, `icon`
- 런타임 로드 경로: `Resources/UI/TooltipKeywordDatabase.asset`

참고 문서:
- `Assets/TooltipKeywordDatabase_Setup.md`

---

## 4) 데이터 포맷/파싱 규칙

### 4.1 상태이상 섹션 파싱
`TooltipKeywordFormatter.ExtractStatuses(...)`는 본문 텍스트에서 아래 패턴을 파싱:

- 섹션 시작:
  - `--- Status ---` 또는 `--- 상태 ---`
- 항목 라인 예시:
  - `• Honey  x2  (3T)`
  - `• Focus  x5  (∞T)`

파싱 결과는 `StatusDisplayData`로 전달:
- `Name`
- `StackText`
- `DurationText`
- `Description`
- `Color`

### 4.2 상태명 Alias 정규화
상태명은 표시 일관성을 위해 alias 매핑 사용:

- `Honey -> 꿀`
- `SlushSnow -> 진창눈`
- `Focus -> 집중`
- `Poison -> 중독`
- `Weak -> 약화`
- `Vulnerable -> 취약`
- `Stun -> 기절`
- `Silence -> 침묵`

---

## 5) 캐릭터/몬스터/타일 제공 텍스트 정책

### 5.1 Character
파일: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Character.cs`

- `IHoverTooltipProvider` 구현
- 본문: 이름, 레벨, HP/Armor, 이동 보정
- 패시브: 이름 + 레벨 + 상세 설명
- 상태이상: `--- Status ---` 섹션으로 출력

### 5.2 Monster
파일: `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/Monster.cs`

- 본문: 이름, HP/Armor/ATK, Intent 관련 정보, 패시브
- 상태이상: `--- Status ---` 섹션으로 출력
- direct `OnMouseEnter/Exit` 출력 경로 제거 및 Hover 시스템 중심 통일

### 5.3 Tile
파일: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileData.cs`

- 본문: 타일 번호/타입/속성 상세
- Hover 표시 경로를 `HoverTooltipUI` 단일화

---

## 6) UX 동작 요약

Hover 대상(캐릭터/몬스터/타일)이 있을 때:

1. 메인 패널: 대상 핵심 정보
2. 키워드 패널: 키워드 설명 모음
3. 상태 패널: 상태이상 설명 모음

추가 인터랙션(기존 유지):
- 키워드 링크 hover 시 상세 패널
- 좌클릭 고정 / 우클릭 해제

---

## 7) Honey(꿀) 디버프 표시 보장 사항

요구사항: “엄마곰/아기곰의 꿀 디버프 설명이 캐릭터/몬스터에서 일관되게 보이기”

반영 내용:
- 키워드 DB/fallback에 `꿀`, `Honey` 설명 모두 등록
- 상태 alias 매핑으로 `Honey` 입력도 UI 표시는 `꿀`로 정규화
- 상태 패널에서 스택/지속턴/설명 함께 출력

결과:
- 대상이 `Honey` 상태를 보유하면 별도 상태 패널에 의미가 표시됨

---

## 8) 유지보수 포인트

1. 키워드 추가/수정
   - `TooltipKeywordDatabase.asset`의 `entries` 수정
2. 상태명 alias 확장
   - `TooltipKeywordFormatter.StatusNameAliases` 업데이트
3. 패널 스타일 조정
   - `KeywordGlossaryPanelUI`, `StatusGlossaryPanelUI`에서 폰트/색/패딩 조정

---

## 9) 향후 개선 제안

- 상태/키워드 아이콘 렌더 (`icon` 필드 활용)
- 패널 애니메이션(페이드 인/아웃)
- 모바일/게임패드 입력 대응
- 상태/키워드 데이터 완전 ScriptableObject 이관 (alias 포함)
- 다국어(Localization) 테이블 연동

---

## 10) 관련 파일 목록

- `Assets/Scripts/UI/HoverTooltipUI.cs`
- `Assets/Scripts/UI/KeywordGlossaryPanelUI.cs`
- `Assets/Scripts/UI/StatusGlossaryPanelUI.cs`
- `Assets/Scripts/UI/TooltipKeywordDatabase.cs`
- `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Character.cs`
- `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/Monster.cs`
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileData.cs`
- `Assets/TooltipKeywordDatabase_Setup.md`
