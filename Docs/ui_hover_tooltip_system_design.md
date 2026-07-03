# UI Hover/Tooltip System 설계 및 구현 기록

작성일: 2026-03-29 | 최종 업데이트: 2026-05-14  
범위: 캐릭터/몬스터/타일 Hover 정보, 키워드/패시브/상태이상 패널, ScriptableObject 기반 키워드 DB

---

## 1) 목표와 배경

기존 Hover UX는 대상 정보를 단일 텍스트 블록으로 출력하는 구조였고, 다음 문제가 있었습니다.

- 정보가 길어질수록 가독성 저하
- 타일/몬스터/캐릭터별 표시 방식이 혼재
- 키워드 설명이 본문에 섞여 유지보수 어려움

이에 따라 **Slay the Spire 스타일에 가까운 분리형 패널 UX**로 개편했습니다.

---

## 2) 설계 원칙

1. **단일 Hover 진입 경로**
   - `HoverTooltipUI`가 Raycast로 `IHoverTooltipProvider`를 조회해 표시를 오케스트레이션
2. **정보 패널 분리 (`GlossaryContainerUI` 통합)**
   - 본문 정보(Main Tooltip)
   - 키워드 설명 카드 (Keyword cards)
   - 패시브 설명 카드 (Passive cards)
   - 상태이상 설명 카드 (Status cards)
   - 세 종류의 카드가 `GlossaryContainerUI` 하나에 통합 관리됨
3. **데이터 주도 확장**
   - 키워드 설명/색상은 `TooltipKeywordDatabase`(ScriptableObject)에서 로드
4. **구조화된 데이터 전달**
   - 상태이상은 텍스트 파싱이 아닌 `HoverTooltipData.Statuses`로 직접 전달
   - 패시브도 `HoverTooltipData.Passives`로 직접 전달

---

## 3) 컴포넌트 구조

### 3.1 `HoverTooltipUI`
파일: `Assets/Scripts/UI/HoverTooltipUI.cs`

역할:
- Hover 대상 감지 (`IHoverTooltipProvider`)
- 본문 툴팁 표시/위치 추적
- `GlossaryContainerUI`에 키워드·패시브·상태이상 카드 전달
- 키워드 링크 상세 팝업 고정/해제 인터랙션

핵심 흐름:
1. Provider에서 `GetHoverTooltipData()` 수신 → `HoverTooltipData` 구조체 반환
2. `TooltipKeywordFormatter.FormatMainTooltipText(data.MainText)` — TrimEnd 정규화
3. 본문 텍스트를 메인 패널에 설정
4. `TooltipKeywordFormatter.ExtractMatches(text)` — 본문에서 DB 키워드 추출
5. `glossaryContainer.Show(matchedKeywords, data.Statuses, data.Passives, ...)` 호출
6. 키워드 링크 hover/클릭 → `ShowKeywordDetail()` / 고정 토글

주요 Inspector 참조:
- `panelRect` — 메인 툴팁 패널 RectTransform
- `tooltipText` — 본문 TextMeshProUGUI
- `glossaryContainer` — `GlossaryContainerUI` (키워드+패시브+상태이상 카드 컨테이너)
- `keywordDetailRect` / `keywordDetailText` — 키워드 상세 팝업 패널

### 3.2 `GlossaryContainerUI`
> ⚠️ 구 문서는 `KeywordGlossaryPanelUI`·`StatusGlossaryPanelUI` 두 개로 분리되어 있었으나,
> 현재는 `GlossaryContainerUI` 하나로 통합됨.

역할:
- 키워드 카드·패시브 카드·상태이상 카드를 한 컨테이너에서 관리
- 메인 패널 위치 기준으로 배치 재조정 (`Reposition`)

`Show(matchedKeywords, statuses, passives, panelPos, panelSize, scale)` 시그니처:
- `matchedKeywords` — `List<KeywordDisplayData>` (본문에서 추출)
- `statuses` — `IReadOnlyList<StatusDisplayData>` (Provider가 직접 전달)
- `passives` — `IReadOnlyList<KeywordDisplayData>` (Provider가 직접 전달)

### 3.3 `TooltipKeywordDatabase`
파일: `Assets/Scripts/UI/TooltipKeywordDatabase.cs`

역할:
- 키워드 정의를 ScriptableObject로 관리
- 필드: `key`, `description`, `color`, `icon`
- 런타임 로드 경로: `Resources/UI/TooltipKeywordDatabase.asset`

---

## 4) 데이터 포맷 / 전달 방식

### 4.1 `HoverTooltipData` 구조체
파일: `Assets/Scripts/UI/IHoverTooltipProvider.cs`

```csharp
public readonly struct HoverTooltipData
{
    public readonly string MainText;
    public readonly IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData> Statuses;
    public readonly IReadOnlyList<TooltipKeywordFormatter.KeywordDisplayData> Passives;
}
```

- `MainText` — 메인 패널에 표시할 본문 텍스트
- `Statuses` — 상태이상 카드 데이터 목록 (Provider가 직접 빌드해서 전달)
- `Passives` — 패시브 카드 데이터 목록 (Provider가 직접 빌드해서 전달)

> ⚠️ 구 문서의 "본문 텍스트에서 `--- Status ---` 섹션을 파싱" 방식은 **현재 사용하지 않음**.
> `ExtractStatuses()` 메서드는 삭제되었고, 상태이상은 Provider 쪽에서 구조화된 데이터로 넘겨야 합니다.

### 4.2 상태이상 데이터 빌드 (`BuildStatusDisplayData`)
`TooltipKeywordFormatter.BuildStatusDisplayData(rawName, value, duration)` 사용:

- `rawName` — C# enum 이름 (예: `"Focus"`) → `StatusNameAliases`로 한국어 변환
- `value` — 스택 수 (0이면 스택 텍스트 미표시)
- `duration` — 남은 지속 턴 (-1이면 `∞T`)

### 4.3 상태명 Alias 정규화
`StatusNameAliases` 딕셔너리 (OrdinalIgnoreCase):

| enum 이름 | 표시 이름 |
|---|---|
| `Honey` | 꿀 |
| `SlushSnow` | 진창눈 |
| `Focus` | 집중 |
| `Poison` | 중독 |
| `Weak` | 약화 |
| `Vulnerable` | 취약 |
| `Stun` | 기절 |
| `Silence` | 침묵 |
| `Frozen` | 빙결 |
| `Frostbite` | 동상 |
| `BuffAttack` | 공격력 증가 |
| `BuffDefense` | 방어력 증가 |
| `DebuffAttack` | 공격력 감소 |
| `DebuffDefense` | 방어력 감소 |
| `Dot` | 지속 피해 |
| `Shield` | 보호막 |
| `Dodge` | 회피 |

---

## 5) `IHoverTooltipProvider` 구현 정책

```csharp
public interface IHoverTooltipProvider
{
    HoverTooltipData GetHoverTooltipData();
}
```

### 5.1 Character
파일: `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Character.cs`

본문에 포함할 정보:
- 이름, 레벨, HP/Armor, 이동 보정

`Passives`:
- 패시브 스킬 이름 + 레벨 + 설명을 `KeywordDisplayData`로 빌드해서 전달

`Statuses`:
- `BuildStatusDisplayData`로 현재 상태이상 목록을 빌드해서 전달

### 5.2 Monster
파일: `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/Monster.cs`

- 이름, HP/Armor/ATK, 의도 정보
- 상태이상은 `Statuses`로 직접 전달

### 5.3 Tile
파일: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileData.cs`

- 타일 번호/타입/속성 상세

---

## 6) UX 동작 요약

Hover 대상(캐릭터/몬스터/타일)이 있을 때:

1. **메인 패널**: 대상 핵심 정보 (마우스 따라 이동)
2. **GlossaryContainer** (메인 패널 옆/아래):
   - 키워드 카드: 본문에서 DB 키워드 자동 추출
   - 패시브 카드: Provider가 직접 전달 (`Passives`)
   - 상태이상 카드: Provider가 직접 전달 (`Statuses`)
3. **키워드 상세 팝업**: 키워드 링크 hover 시 표시
   - 좌클릭: 고정 (같은 키워드 재클릭 시 해제)
   - 우클릭: 즉시 해제

**Pinned 모드** (`ShowPinned`):
- UI 컴포넌트가 `ShowPinned(string)` 또는 `ShowPinned(HoverTooltipData)` 직접 호출
- Pinned 상태에서는 3D Raycast 탐색을 건너뜀
- 해제: `HidePinned()`

---

## 7) Honey(꿀) 디버프 표시 보장 사항

- 키워드 DB에 `꿀`, `Honey` 설명 모두 등록
- `StatusNameAliases`로 `Honey` 입력도 UI 표시는 `꿀`로 정규화
- 상태 카드에서 스택/지속턴/설명 함께 출력

---

## 8) 유지보수 포인트

1. **키워드 추가/수정**
   - `Resources/UI/TooltipKeywordDatabase.asset`의 `entries` 수정
2. **상태명 alias 확장**
   - `TooltipKeywordFormatter.StatusNameAliases` 딕셔너리에 항목 추가
3. **패시브 카드 표시 추가**
   - Provider의 `GetHoverTooltipData()`에서 `Passives` 리스트 채우기
4. **상태이상 카드 표시 추가**
   - `BuildStatusDisplayData(rawName, value, duration)` 호출 후 `Statuses`로 전달
5. **패널 스타일 조정**
   - `GlossaryContainerUI`에서 카드 레이아웃/폰트/색/패딩 조정

---

## 9) 향후 개선 제안

- 상태/키워드 아이콘 렌더 (`icon` 필드 활용)
- 패널 애니메이션 (페이드 인/아웃)
- 모바일/게임패드 입력 대응
- `StatusNameAliases` 완전 ScriptableObject 이관
- 다국어(Localization) 테이블 연동

---

## 10) 관련 파일 목록

- `Assets/Scripts/UI/HoverTooltipUI.cs`
- `Assets/Scripts/UI/IHoverTooltipProvider.cs` — `HoverTooltipData`, `IHoverTooltipProvider`
- `Assets/Scripts/UI/TooltipKeywordFormatter.cs` — 포매터, `StatusDisplayData`, `KeywordDisplayData`
- `Assets/Scripts/UI/TooltipKeywordDatabase.cs`
- `Assets/Scripts/UI/GlossaryContainerUI.cs` ← 구 KeywordGlossaryPanelUI + StatusGlossaryPanelUI 통합
- `Assets/Scripts/Core/Stage/BattleStage/Units/Character/Character.cs`
- `Assets/Scripts/Core/Stage/BattleStage/Units/Monster/Monster.cs`
- `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Tile/TileData.cs`
