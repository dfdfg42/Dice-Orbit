# 전투 정보 패널 (Battle Info Panel) 설계

작성일: 2026-07-05 | 브랜치: `feature/battle-ui-info-panel-20260705`

---

## 1. 배경 및 동기

- 현재 캐릭터/몬스터/타일 정보는 **호버해야만** 마우스 추적 툴팁(`HoverTooltipUI` + `GlossaryContainerUI`)으로 보인다. 전투 중 상태를 한눈에 계속 볼 수 없다.
- 핵심 동기: **"항상 보이게"** + 기존 툴팁 시스템 평가·개선.
- 기존 툴팁 데이터 구조 평가 결과: Provider 패턴·`StatusDisplayData`·`GetDynamicDescription()`은 우수하나, `MainText` 리치텍스트 blob과 `KeywordDisplayData` 오용(패시브), 액티브/타일/원시수치 부재로 패널 요구를 감당 못함 → **표현 계층·데이터 모델은 신규, 우수한 부품만 계승**.

## 2. 확정 요구사항

| 항목 | 결정 |
|---|---|
| 패널 위치/크기 | 화면 오른쪽 ~1/3, 스크린스페이스 캔버스 |
| 궤도 필드 | 씬에서 카메라 조정으로 왼쪽 2/3 중앙 배치 (코드 변경 없음) |
| 패널 대상 | 선택/호버한 대상 **1명** 상세 (캐릭터/몬스터) |
| 표시 내용 | 이름/레벨/HP·Armor 게이지, 패시브, 액티브, 상태이상, **밟고 있는 타일 카드**(이미지+속성 아이콘+속성별 설명) |
| 선택 방식 | 호버 즉시 갱신 + 클릭 고정(핀). 핀 중 다른 대상 호버 시 임시 표시 후 복귀. 빈 상태는 안내 문구 |
| 기존 툴팁 | **축소 공존**: `ShowPinned(string)` 경량 경로만 잔류 (예상 피해, 선택 카운터, 스킬버튼 호버). 나머지 철거 |
| 갈아엎기 수위 | 어중간한 병행 없음 — blob 경로·글로서리 컨테이너 완전 삭제 |

## 3. 레이아웃 / 카메라

- 오른쪽 1/3 불투명(약간 투명 가능) 패널. 세로 스크롤 허용.
- 카메라는 씬 에디터에서 위치/각도 조정 — 전용 카메라 스크립트 없음이 확인됨. 레이캐스트(`ScreenPointToRay`)는 카메라 기준이라 자동 적응.
- 패널 위에 포인터가 있으면 3D 레이캐스트 차단 (`EventSystem.IsPointerOverGameObject`).

## 4. 데이터 모델 (신규)

파일: `Assets/Scripts/UI/InfoPanel/BattleInfoData.cs` (신규 폴더 `UI/InfoPanel/`)

```csharp
public readonly struct UnitInfoData
{
    public string Name;  public int Level;
    public int CurrentHp, MaxHp, Armor;                 // 원시 숫자 → 게이지 렌더링용
    public string FlavorText;                            // 서식 없는 프로필 원문
    public IReadOnlyList<SkillInfoData>   Actives;       // 이름/주사위조건/동적설명/레벨
    public IReadOnlyList<PassiveInfoData> Passives;      // 이름/레벨/동적효과/원문 — 필드 분리
    public IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData> Statuses; // 기존 구조체 재사용
    public TileInfoData CurrentTile;                     // 밟고 있는 타일
}

public readonly struct SkillInfoData   { string Name; string DiceCondition; string DynamicDescription; int Level; }
public readonly struct PassiveInfoData { string Name; int Level; string DynamicEffect; string FlavorText; }

public readonly struct TileInfoData
{
    public int TileIndex;  public TileType Type;
    public IReadOnlyList<TileAttributeInfo> Attributes;  // 원시값만
}
public readonly struct TileAttributeInfo { TileAttributeType Type; int Value; int Duration; }

public interface IBattleInfoProvider { UnitInfoData GetBattleInfo(); }   // Character, Monster 구현
```

**원칙:**
- **서식 제로**: Provider는 숫자와 원문만. 색/리치텍스트 태그/"Lv." 접두어/아이콘 해석은 전부 패널 렌더 계층에서.
- **빌더 단일화**: 현재 Character/Monster에 복붙된 상태이상·패시브 빌더를 `UnitInfoBuilder`(static) 한 곳으로 통합. `GetDynamicDescription()` 호출 포함.
- 타일은 유닛과 계층이 다르므로 `IBattleInfoProvider`에 억지로 묶지 않고 `TileData.GetTileInfo()` 별도 메서드.

## 5. 패널 컴포넌트 구조

```
BattleInfoPanelUI (오케스트레이터, 싱글턴, 신규 폴더 UI/InfoPanel/)
 ├─ InfoPanelSelectionController — 대상 결정 (호버/핀/타게팅 모드 규칙)
 ├─ HeaderSection    — 이름/레벨/HP·Armor 게이지/초상화
 ├─ SkillSection     — 액티브 목록 (주사위 조건 뱃지 + 동적 설명)
 ├─ PassiveSection   — 패시브 카드 (동적 효과 수치)
 ├─ StatusSection    — 상태이상 카드 (스택/지속턴)
 ├─ TileCardSection  — 타일 카드 이미지 + 속성 아이콘 오버레이 + 속성별 상세 행
 └─ KeywordSection   — 설명 텍스트에서 추출한 키워드 카드 (기존 ExtractMatches 계승)
```

### 5.1 TileCardSection 상세

```
┌─ 밟고 있는 타일 ────────┐
│   ┌──────────┐          │
│   │ 타일 카드  │ 🍯 ⚗️   │  ← 카드 이미지 + 속성 아이콘 오버레이 (월드 버블과 동일 아이콘)
│   └──────────┘          │
│  🍯 꿀 x2 (3T)           │  ← 속성마다 한 줄: 아이콘+이름+스택/지속턴+설명
│  ⚗️ 시약 x1 (∞T)         │     숨김 인터랙션 없이 항상 표시 (타일당 속성 1~3개)
└──────────────────────────┘
```

- 카드 이미지: `TileType`(현재 Normal/LevelUp 2종)별 스프라이트를 패널 컴포넌트에 직렬화. 렌더텍스처 미사용.
- 속성 아이콘: `TileAttributeVisualDatabase`(타입→icon/tint/shortLabel) 재사용 — 월드 버블 UI와 아이콘 일치.
- **DB 확장**: `TileAttributeVisualDatabase.Entry`에 `description` 필드 추가 (속성 설명의 단일 출처). `TooltipKeywordDatabase`의 유사 항목(예: "꿀")과 역할 구분: 키워드 DB는 텍스트 내 키워드 링크용, 이 DB는 타일 속성 카드용.

### 5.2 갱신 정책

- 대상 변경 시 즉시 리빌드 + 표시 중 0.5초 간격 리프레시 (HP/상태이상 실시간 반영. 문자열 수 개 수준이라 비용 무시 가능).

## 6. 선택/상호작용 규칙

| 상황 | 패널 동작 |
|---|---|
| 평시 호버 (캐릭터/몬스터) | 즉시 해당 대상으로 갱신 |
| 평시 호버 (빈 타일 직접) | **타일 단독 뷰** — TileCardSection만 표시 (기존 타일 툴팁 역할 계승) |
| 평시 클릭 | 그 대상으로 **고정(핀)**. 재클릭 또는 우클릭으로 해제 |
| 핀 중 다른 대상 호버 | 임시 표시, 호버 벗어나면 핀 대상 복귀 |
| 아무것도 없음 | 안내 문구 (빈 상태) |
| **타게팅 모드 중** | **호버 추적만 작동, 클릭 고정 비활성** (클릭 = 스킬 대상 지정이므로 `SkillTargetSelector`가 소비). 적 호버 시 패널이 그 몬스터 정보로 갱신 |
| 타게팅 중 예상 피해 | 패널이 아닌 **커서 옆 경량 툴팁**(`ShowPinned`) 유지. "N/M 선택" 카운터도 동일 |
| 타게팅 종료(실행/취소) | 핀 대상 또는 빈 상태로 복귀 |

## 7. 철거 목록 🔥 (확실하게)

| 대상 | 처분 |
|---|---|
| `HoverTooltipData` 구조체, `IHoverTooltipProvider` 인터페이스 | **삭제** → `IBattleInfoProvider`/`UnitInfoData`로 대체 |
| `HoverTooltipUI` 레이캐스트 호버 모드, `Show(HoverTooltipData)`, 글로서리 오케스트레이션, 키워드 상세 팝업 | **삭제** — 클래스는 `ShowPinned(string)`/`HidePinned()` 초경량으로 축소 |
| `GlossaryContainerUI` (툴팁 옆 카드 배치) | **삭제** — 카드 배치는 패널 섹션 담당 |
| `Character.BuildCharacterTooltipText` / `Monster.BuildMonsterTooltipText` / `TileData.BuildTooltipText` (blob 빌더) | **삭제** → `UnitInfoBuilder`로 대체 |
| Character/Monster 중복 `BuildStatusTooltipData`/`BuildPassiveTooltipData` | **삭제 → 통합** |
| 철거 후 씬/프리팹의 글로서리 오브젝트 | 씬에서 제거 (missing script 잔재 없게) |

## 8. 존치/재사용 목록 ✅

| 대상 | 역할 |
|---|---|
| `TooltipKeywordFormatter` (`StatusDisplayData`, `BuildStatusDisplayData`, alias 테이블, `ExtractMatches`) | 패널이 그대로 소비 |
| `TooltipKeywordDatabase` | 키워드 카드 데이터 |
| `TileAttributeVisualDatabase` | 타일 속성 아이콘 (+description 확장) |
| `GlossaryCardUI` | 카드 1장 렌더러 — 패널 카드 프리팹으로 재사용 |
| `SkillPreviewHoverUI`, `SkillTargetSelector`의 `ShowPinned` 호출 | 무변경 (경량 경로만 사용) |
| 패시브 `GetDynamicDescription()` 다형성 | `UnitInfoBuilder`가 호출 — 풀 모디파이어 보너스 실시간 반영 |

## 9. 검증 계획

에디터 플레이로 확인:
1. 캐릭터/몬스터/타일 호버·클릭 시 패널 갱신 및 핀 동작
2. 타게팅 모드: 적 호버 → 패널 갱신, 클릭 → 대상 지정(핀 안 됨), 예상 피해 커서 툴팁 병행
3. 상태이상/HP 변화 실시간 반영 (0.5s 리프레시)
4. 타일 카드: 속성 있는 타일(꿀/시약 등) 위 캐릭터 선택 시 아이콘+설명 행 표시
5. 철거 후 컴파일 에러 0, 씬 missing script 0
6. 카메라 이동 후 타게팅 레이캐스트/미리보기 정상

## 10. 비범위 (후속 과제)

- 모바일/터치 대응 (호버 없는 입력 모델)
- HP 게이지에 예상 피해 미리보기(고스트 감소분) 표시
- 패널 애니메이션 (슬라이드/페이드)
- 다국어 테이블 연동
