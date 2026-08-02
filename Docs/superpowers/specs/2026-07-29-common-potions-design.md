# 일반 포션 8종 설계 (+ 신규 상태이상 3종, 타일 조준)

> 작성: 2026-07-29. 브랜치 `feature/event-outcomes-refactor-20260729`.
> 기획 표 기반. **활력 포션(버프 효율)은 보류** — 정의 확정 후 후속.
> 포션 구조 = 2026-07-28 개편본 (Potion 추상 SO, 포션 1종 = 클래스 1 + 에셋 1).

## 1. 신규 상태이상 3종

EffectType **끝에 추가** (직렬화 순서 보존): `Weak`, `Poison`, `Power`.
`StatusEffect` 서브클래스 3개 (`Combat/Effects/`), `CreateEffect` 팩토리 switch 등록.
훅은 반드시 `override` (§combat_reactor_dispatch.md 4.1).

| 상태 | Value 의미 | 구현 |
|---|---|---|
| 쇠약(Weak) | 감소율 %(25) | `OnAttack` OnCalculateOutput, Source==Owner → `OutputValue ×(1-V/100)` |
| 파워(Power) | 증가율 %(25) | 동일 구조, `×(1+V/100)` |
| 독(Poison) | 최대HP 비율 %(5) | `OnTurnEvent` 본인 TurnStart(OnPreAction 게이트) → `AttackContext(null, Owner, "독", MaxHP×V/100)` Process (중첩 Process는 파이프라인 로컬 상태라 안전 — LifeAmulet 실증) |

- 계산 개입이라 예상 피해 미리보기 자동 반영. 아이콘 줄/오버레이/호버 설명에 자동 표시.
- 표시명: `StatusNameAliases` 갱신 — Weak→**쇠약**(기존 약화), Poison→**독**(기존 중독), Power→**파워** 추가.
  설명은 키워드 DB에 3종 등록.
- 중첩 규칙: 기존 관례(AddStack 값 누적 + 지속 갱신) 그대로.

## 2. 타일 조준 지원 (중화 포션용)

- `PotionTargetType.Tile` 추가.
- `Potion.UseOnTile(TileData tile)` 가상 메서드 (기본 false) + `PotionManager.TryUseOnTile(index, tile)`.
- `PotionTargetSelector`에 타일 조준 모드 — 스킬 타일 타겟팅 로직 참고, Tile 타입 포션이면 타일 히트만 허용.

## 3. 포션 8종 (`Assets/Scripts/Data/Potions/<이름>/`)

| 클래스 | 타겟 | CombatOnly | 효과 |
|---|---|---|---|
| WeakPotion 쇠약 포션 | Enemy | ✓ | `AddEffect(Weak, 25, 2)` |
| ExplosionPotion 폭발 포션 | Enemy | ✓ | `AttackContext(null, t, "폭발 포션", 20)` Process |
| PoisonPotion 독 포션 | Enemy | ✓ | `AddEffect(Poison, 5, 2)` |
| PowerPotion 파워 포션 | Ally | ✓ | `AddEffect(Power, 25, 2)` |
| HealPotion 회복 포션 (교정) | Ally | ✗ | **HealContext 경유로 교정** (현행 HP 직접 대입 = 파이프라인 위반), 회복량 20 |
| DefensePotion 방어 포션 | Ally | ✓ | `Stats.TempArmor += 30` (기존 일시 방어도 — 턴 초기화·선흡수) |
| NeutralizePotion 중화 포션 | **Tile** | ✓ | 지정 타일의 디버프 속성 제거: RandMine·Honey·SnowPrison·Bone |
| RandomPotion 랜덤 포션 | None | ✗ | 직접 사용 불가(Use=false). 전투 시작 시 변신 (§4) |

## 4. 랜덤 포션 변신

`PotionManager`가 `CombatManager.OnCombatStart` 구독 → 슬롯 중 `RandomPotion`을
풀의 무작위 **일반 포션(랜덤 포션 제외)**으로 참조 교체 + `OnChanged`.
세이브는 이름 매칭이라 변신 후 이름이 저장됨 (자연 호환).

## 5. 배선

- 에셋 8개 생성(에디터 스크립트) → 씬 `PotionManager.potionPool` 등록
  → 상점 진열/전투 드랍/변신 풀 자동 반영. 아이콘 아트는 후속 (시약·치유 아이콘만 존재).
- 키워드 DB에 쇠약/독/파워 설명 등록 → 상태 카드·아이콘 호버·정보 패널 일괄 반영.

## 6. 검증

컴파일 게이트 + 플레이: ① 각 포션 사용·소모 ② 독 틱(턴 시작 피해, 파이프라인 알림)
③ 쇠약/파워가 예상 피해 미리보기에 반영 ④ 방어 포션 → 피해 선흡수·턴 종료 초기화
⑤ 중화 타일 조준·디버프 속성 제거 ⑥ 랜덤 포션 전투 시작 변신 ⑦ 상점 진열·세이브 복원 이름 매칭.
