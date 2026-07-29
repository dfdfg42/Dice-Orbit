# 상태이상 시각화 설계 — 체력바 아래 아이콘 줄 + 유닛 오버레이 스택

> 작성: 2026-07-29. 브랜치 `feature/event-outcomes-refactor-20260729`.
> 목표: 상태이상이 생기면 ① 유닛 체력바 아래에 아이콘(+스택 수)이 나열되고
> ② 캐릭터/몬스터 스프라이트 위에 상태 장식 스프라이트가 겹겹이 얹힌다.
> 아트 레퍼런스: `Assets/Sprites/상태이상/적용했을 때의 모습.png` (얼음 조각 + 꿀 방울 동시 적용).

## 1. 현황 (조사 결과)

- 체력바: `CharacterUI` / `MonsterUI` — 유닛별 머리 위 월드 스페이스 캔버스. **레이캐스트 전부
  꺼짐** (`ConfigureNonBlockingRaycasts` — 캐릭터 클릭 보호. 절대 켜지 말 것).
- `StatusEffectManager`(유닛별 인스턴스): **변경 이벤트 없음** — Add/Remove가 평범한 메서드.
- 아트: 오버레이 2종(꿀·얼음 `상태이상/`), 아이콘 2종(시약·치유 `아이콘/`) — 부분 준비.
- 기존 툴팁 부품: `HoverTooltipUI.ShowPinned`(커서 옆), `BuildStatusDisplayData`(이름·설명·색),
  `KeywordLinkHover`의 폴링 히트테스트 패턴 (레이캐스트 불필요).

## 2. 컴포넌트

### ① StatusEffectManager.OnChanged (선행)
`public event System.Action OnChanged;` — `AddEffect` / `RemoveEffect` 끝에서 발화
(만료 제거도 RemoveEffect 경유라 커버). UI 두 개(§③④)가 유닛 단위로 구독.

### ② StatusVisualLibrary (매핑 DB — 씬 컴포넌트 싱글톤)
타일 버블(`attributeVisuals`) 패턴 승계. 인스펙터 리스트:
`EffectType → { Sprite icon, Sprite overlay, Color tint }` + `TryGet(EffectType, out entry)`.
폴백: 아이콘 없음 → 상태색 원형 칩 + 이름 첫 글자 / 오버레이 없음 → 오버레이 생략.

### ③ StatusIconRow (체력바 아래 아이콘 줄 — 공용 컴포넌트)
- `CharacterUI`/`MonsterUI`가 HP바 아래에 부착·생성 (양쪽 공용 — 중복 구현 금지).
- 유닛 `StatusEffects.OnChanged` 구독 → 재구성: 상태별 아이콘 가로 나열,
  **우하단 스택 숫자** (value>0일 때 — 정보 패널 표기 언어와 동일: 숫자만, x 없음).
- 상태 0개면 줄 숨김. 모든 Graphic은 `raycastTarget=false` (클릭 보호 유지).
- **호버 설명**: 레이캐스트가 꺼진 캔버스라 IPointerEnter 불가 →
  `KeywordLinkHover` 방식의 폴링 히트테스트(`RectangleContainsScreenPoint`, 카메라=mainCamera)
  → 히트 시 `HoverTooltipUI.ShowPinned($"[이름]\n설명")` — 텍스트는 `BuildStatusDisplayData` 재사용.

### ④ StatusOverlayStack (유닛 위 장식 겹침)
- 유닛 스프라이트의 자식으로 부착 (CharacterUI/MonsterUI 초기화 시 같이 부착).
- `OnChanged` 구독 → 오버레이 아트가 있는 상태마다 SpriteRenderer 1장 생성:
  유닛 SpriteRenderer **bounds에 맞춰 스케일**, `sortingLayer` 동일 + `sortingOrder = 유닛+1, +2…`
  (획득순 겹침 — 레퍼런스처럼 꿀+얼음 동시 표시). 상태 해제 시 해당 장만 제거.
- 유닛 애니메이션(idle/피격 프레임)과 무관하게 정적으로 얹힘.

## 3. 데이터 흐름

```
StatusEffectManager.AddEffect/RemoveEffect
  → OnChanged 발화
      ├─ StatusIconRow.Rebuild()      (아이콘+스택 숫자, 호버 폴링)
      └─ StatusOverlayStack.Rebuild() (오버레이 장 추가/제거)
비주얼 조회는 둘 다 StatusVisualLibrary.TryGet 경유. 설명 텍스트는 BuildStatusDisplayData.
```

## 4. 검증

- 컴파일 게이트 (Unity MCP 콘솔 0 에러).
- 플레이: ① 꿀곰 전투에서 꿀 상태 → 아이콘+꿀 오버레이 표시 ② 집중 스택 → 아이콘 숫자 증가
  ③ 두 상태 동시 → 오버레이 겹침 (레퍼런스 비교) ④ 만료/해제 시 아이콘·오버레이 제거
  ⑤ 아이콘 호버 → 설명 툴팁 ⑥ 캐릭터 클릭/타게팅이 여전히 정상 (레이캐스트 보호 확인).
