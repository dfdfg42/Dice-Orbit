# 이벤트 노드 이벤트 9종 — 설계 스펙

- 날짜: 2026-07-30
- 브랜치: feature/event-outcomes-refactor-20260729
- 상태: 승인됨 (구두 승인 — "좋아 그대로 가자")

## 1. 범위

기획표의 이벤트 10종 중 9종 구현. **포션 강화기는 보류** (보상 '상급 포션'이 미정의 — 상급 포션 기획 확정 후 별도 작업).

| 분류 | 이벤트 | 방문 내 반복 |
|---|---|---|
| 주사위 | 주사위 교정기 | 최대 3회 |
| 주사위 | 인챈트 | 무제한 |
| 캐릭터 | 회복기 | 무제한 |
| 캐릭터 | ONE OR ALL | 1회 |
| 포션 | 고장난 자판기 | 1회 |
| 타일 | 조화의 정령 | 최대 3회 |
| 타일 | 제련소 | 최대 3회 |
| 유물 | 보물상자 | 1회 |
| 유물 | 고대 요정 | 최대 3회 |

## 2. 확정 결정 (사용자 Q&A)

1. **"선택지 반복" = 한 방문 안 반복** (StS식). 선택을 해소해도 이벤트 화면이 유지되고, 선택지별 남은 횟수만큼 다시 고를 수 있다. "넘어간다"로 종료.
2. **타일 설치는 런 내내 유지** — 이벤트 시점엔 예약만, 매 전투 시작 시 무작위 타일에 배치.
3. **같은 이벤트는 런에서 재등장하지 않음** — 본 이벤트는 풀에서 제외, 풀 소진 시 전체 리셋.
4. 접근 방식: **기존 EventDefinition 에셋 + EventOutcome 다형성 확장** (이벤트별 커스텀 클래스 없음).

## 3. 아키텍처 변경

### 3.1 EventChoice.RepeatLimit
`EventChoice`에 `RepeatLimit` int 필드 추가 (0 = 무제한, N = 최대 N회). 결과가 하나도 없는 선택지("넘어간다")는 이벤트 종료 버튼으로 동작.

EventUI 흐름 변경:
- 선택 해소 후 결과 텍스트를 갱신하되 화면 유지, 선택지 재표시.
- 선택지 라벨에 남은 횟수 병기 (`[2회 남음]`), 소진 시 비활성(회색).
- "넘어간다" 클릭 또는 모든 선택지 소진 시 [확인] → `OnEventComplete`.

### 3.2 대상 선택 — TargetedEventOutcome
```
public enum EventSelectionKind { Die, Character }

public abstract class TargetedEventOutcome : EventOutcome
{
    public abstract EventSelectionKind Kind { get; }
    public abstract string Apply(EventTargetContext ctx);   // 무인자 Apply()는 sealed — 호출 금지
}

public class EventTargetContext
{
    public int SelectedDieIndex;          // DiceDeckManager.Deck 인덱스
    public Character SelectedCharacter;
}
```
- EventUI는 선택지 클릭 시 그 선택지의 Outcome들이 요구하는 Kind를 수집해 **종류당 1회** 선택 패널을 순서대로 표시 (Die → Character).
- 같은 선택지의 여러 Outcome이 같은 Kind를 요구하면 선택을 공유한다 (인챈트: 면 0으로 + 인챈트 부여 = 같은 주사위).
- 선택 패널: 주사위 = 이름 + 면 6개 나열 버튼, 캐릭터 = 이름 + HP 버튼. 취소 버튼 = 선택지 고르기 전으로 복귀 (반복 횟수 미소모).

### 3.3 주사위 면 오버라이드 — DieInstance.FaceOverride
- `DieInstance`에 `int[] FaceOverride` 추가 (null = 원본).
- `Faces` → `FaceOverride ?? BaseDie.Faces`.
- `RollFace()` → **인스턴스 Faces에서 굴리도록 교정** (현재 `BaseDie.RollFace()` 직행 — 이대로면 면 변형이 굴림에 반영 안 됨). 주사위 면을 읽는 모든 표시/굴림 경로가 인스턴스 `Faces`를 경유하는지 구현 시 전수 확인.
- `Replace()`는 기존처럼 오버라이드/인챈트 초기화.

DiceDeckManager 면 조작 API:
| API | 동작 |
|---|---|
| `RandomizeFaces(index, count)` | 무작위 면 count개를 각각 1~6 무작위 값으로 |
| `AddToRandomFaces(index, count, delta)` | 무작위 면 count개에 delta (하한 0) |
| `ZeroRandomFace(index)` | 무작위 면 1개를 0으로 |

면 값 0 = 꽝 (굴리면 0). 조작 대상 면은 서로 다른 면에서 무작위 추출 (count > 6이면 6개 전부).

### 3.4 인챈트 — DieEffect 2종 신설
- `GainArmorOnUse`: 사용 시 사용자 `TempArmor += 10` (방어 포션과 같은 직접 경로).
- `EmpowerOnUse`: 사용 시 사용자에게 파워(10%, 1턴) 부여 — 공격 +10%를 파이프라인에 태우는 가장 단순한 근사. (승인됨)
- 이미 인챈트가 붙은 주사위에 재부여 = 교체 (`AttachedEffect` 덮어쓰기).

### 3.5 타일 속성 5종 신설
`TileAttributeType`에 추가 (enum 끝에 — 직렬화 순서 보존): `Sharp, Dull, Sturdy, Harmony, Disharmony`

| 타입 | 이름 | 효과 | 훅 |
|---|---|---|---|
| Sharp | 예리함 | 이 타일에서 공격 시 피해 +10% | OnReact(OnCalculateOutput, 시전자가 이 타일 위) |
| Dull | 약화 | 이 타일에서 공격 시 피해 -10% | 상동 (기획 문구의 "턴 종료 시"는 오기로 보고 공격 시로 통일) |
| Sturdy | 단단함 | 이 타일 통과(Traverse/Arrive) 및 이 타일에서 턴 종료 시 방어도 +10 | OnTraverse + OnArrive + OnEndTurn |
| Harmony | 조화 | 이 타일에서 턴 종료 시 최대체력 5% 회복 | OnEndTurn → HealContext 파이프라인 |
| Disharmony | 부조화 | 이 타일에서 턴 종료 시 최대체력 5% 피해 | OnEndTurn → AttackContext 파이프라인 (사망 가능) |

- 조화의 정령 "강화 타일"은 예리함과 효과가 동일 → **Sharp 재사용** (별도 타입 없음).
- 타일 훅이 Character 전용이므로 통과/턴 종료 효과는 아군에게만 적용 (몬스터 무시) — 현 구조 그대로.
- 설치 지속: 영구(-1). 매 전투 재배치되므로 전투 내 제거(중화 포션 등)는 그 전투에만 유효.
- 중화 포션의 디버프 목록에 Dull/Disharmony 추가.

### 3.6 타일 설치 예약 — EventTileInstalls
- PotionManager식 DontDestroyOnLoad 매니저. 내부: `List<TileAttributeType>` (설치 1건 = 타입 1개).
- 이벤트 결과가 `Enqueue(type)` — 즉시 요약 문자열 반환 ("예리함 타일 설치 예약").
- 전투 시작(CombatManager.OnCombatStart) 시 목록 전부를 무작위 타일에 배치. 한 전투에서 같은 타일 중복 회피 (설치 수 > 타일 수면 남는 것부터 중복 허용).
- 세이브: 타입 리스트 직렬화 (기존 세이브 구조에 필드 추가 — 구현 시 GameFlowManager 세이브 확인).

### 3.7 이벤트 재등장 제외
- 런 레벨 seen 목록 (`HashSet<string>`, key = EventDefinition 에셋 이름).
- EventUI.Show가 풀 − seen에서 랜덤, 뽑는 즉시 seen에 기록. 후보가 없으면 seen 리셋 후 전체 풀에서.
- 세이브에 seen 목록 포함 (3.6과 같은 위치에서 처리).

## 4. 이벤트 9종 상세

수치는 기본값 — 에셋에서 조정 가능.

### 4.1 주사위 교정기 (반복 3회)
> "오래된 기계가 있습니다. 기계를 가동하자 굉음을 내면서 빛을 내보내고 있습니다."
1. **가동한다** [Die 선택] — 선택 주사위의 무작위 면 2개를 무작위 값(1~6)으로.
2. **미세 조정** [Die 선택] — 선택 주사위의 무작위 면 3개에 -1 (하한 0).
3. 넘어간다.

### 4.2 인챈트 (반복 무제한)
> "주사위에 능력을 부여해주는 것 같습니다."
1. **수호 인챈트** [Die 선택] — 무작위 면 1개 → 0, 그 주사위에 `GainArmorOnUse`(방어도 10) 부여.
2. **공세 인챈트** [Die 선택] — 무작위 면 1개 → 0, 그 주사위에 `EmpowerOnUse`(피해 +10%, 1턴 파워) 부여.
3. 넘어간다.

### 4.3 회복기 (반복 무제한)
> "상처를 낫게하는 회복기입니다. 공짜는 아닌듯 합니다."
1. **가동한다** [Die 선택 → Character 선택] — 무작위 면 1개 → 0, 선택 캐릭터 MaxHP 10% 회복 (전투 밖 — HealParty 관례대로 직접 HP, 클램프).
2. 넘어간다.

### 4.4 ONE OR ALL (반복 1회)
> "ONE FOR ALL? ALL FOR ONE? 이게 뭘까요?"
1. **ONE FOR ALL** [Character 선택] — 선택 제외 각 파티원 MaxHP 10% 감소(내림, 최소 1 감소), 감소 총합만큼 선택 캐릭터 MaxHP 증가.
2. **ALL FOR ONE** [Character 선택] — 선택 캐릭터 MaxHP 30% 감소(내림), 감소분을 나머지 파티원에게 균등 분배 (나머지 몫은 앞 순서부터 +1).
- MaxHP 증가 시 CurrentHP도 같은 양만큼 증가. 감소 시 CurrentHP는 새 MaxHP로 클램프 (최소 1).
- 파티가 1명이면 두 선택지 모두 비활성.

### 4.5 고장난 자판기 (반복 1회)
> "포션 자판기 입니다. 고장나보이는데요. 버튼을 눌러볼까요?"
1. **버튼을 누른다** — 무작위 포션 2개 (`GrantRandomDrop` ×2 — 슬롯 남는 만큼만, 가득이면 그만큼 무효 안내).
2. 넘어간다.

### 4.6 조화의 정령 (반복 3회)
> "정령이 말을 걸어 옵니다. 조화는 유지되어야 한다고 합니다."
1. **힘의 조화** — Sharp 1개 + Dull 1개 설치 예약.
2. **생명의 조화** — Harmony 1개 + Disharmony 1개 설치 예약.
3. 넘어간다.

### 4.7 제련소 (반복 3회)
> "무엇이든 제련하는 장소입니다. 어떤 걸 좋게 만들까요?"
1. **예리함** — Sharp 1개 설치 예약.
2. **단단함** — Sturdy 1개 설치 예약.
3. 넘어간다. (기획표에 없으나 반복 이벤트 종료 수단으로 필수)

### 4.8 보물상자 (반복 1회)
> "야호"
1. **보물상자를 연다** — 무작위 유물 (기존 `GainRandomRelic`).
2. 넘어간다.

### 4.9 고대 요정 (반복 3회)
> "고대 요정이 말을 걸어 옵니다. 유물을 달라고 하네요. 대신 보답하겠다고 합니다."
1. **준다** — 보유 유물 중 무작위 1개 상실 + 전 파티원 MaxHP 10% 증가(내림, 최소 1). 보유 유물이 없으면 비활성.
2. 넘어간다.

## 5. 신규 EventOutcome 클래스 목록

| 클래스 | 선택 | 동작 |
|---|---|---|
| `RandomizeDieFaces` | Die | 면 count(2)개 무작위화 |
| `AddToDieFaces` | Die | 면 count(3)개에 delta(-1) |
| `ZeroDieFaceAndEnchant` | Die | 면 1개 → 0 + DieEffect 부여 (effect 필드 [SerializeReference], null이면 면만 0) |
| `HealSelectedCharacter` | Character | MaxHP percent(10)% 회복 |
| `OneForAll` | Character | §4.4-1 |
| `AllForOne` | Character | §4.4-2 |
| `GainRandomPotions` | — | count(2)개 (기존 GainRandomPotion의 복수판) |
| `QueueTileInstall` | — | EventTileInstalls.Enqueue(type) — type 인스펙터 지정 |
| `LoseRandomRelicGainPartyMaxHp` | — | 유물 상실 + 전원 MaxHP +10% (유물 없으면 선택지 비활성 조건 제공) |

회복기의 "면 1개 → 0"은 `ZeroDieFaceAndEnchant`(effect=null) 재사용.

선택지 비활성 조건: `EventOutcome`에 `virtual bool CanApply()` (기본 true) 추가 — EventUI가 선택지의 모든 Outcome CanApply AND로 활성 판정 (고대 요정 = 유물 보유, ONE OR ALL = 파티 2인 이상).

## 6. 에셋/배선

- 이벤트 에셋 9개: `Assets/Scripts/Data/Events/` (폴더 신설, 이벤트당 1에셋).
- 씬 EventUI.eventPool에 9종 등록. 런타임 폴백 3종(도박/마차)은 유지하되 풀이 채워지므로 미사용.
- EventTileInstalls 매니저 씬 배치.
- 타일 5종 이름/설명은 TileAttribute.GetDisplayName/GetDescription 확장 + 필요시 키워드 DB 등록.

## 7. 검증 체크리스트 (사용자 플레이)

- [ ] 교정기: 면 변형이 주사위 호버/굴림 값에 반영, 3회 후 소진
- [ ] 인챈트: 면 0 확인, 사용 시 방어도/파워 발동, 재부여 시 교체
- [ ] 회복기: 주사위→캐릭터 2단 선택, 반복 사용
- [ ] ONE OR ALL: 양방향 MaxHP 이동, 1회 후 종료, 1인 파티 비활성
- [ ] 자판기: 포션 2개 (슬롯 상황별)
- [ ] 정령/제련소: 다음 전투부터 매 전투 타일 등장, 효과 발동 (공격±10%/방어도/회복/피해)
- [ ] 보물상자/고대 요정: 유물 증감 + MaxHP 증가, 유물 0개 비활성
- [ ] 재등장 제외: 한 런에서 같은 이벤트 안 나옴, 풀 소진 시 리셋
- [ ] 중화 포션이 Dull/Disharmony 제거
