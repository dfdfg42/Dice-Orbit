# Artifact 시스템 재구축 설계 (구 코드 기반 + 런 기능 흡수)

> 작성: 2026-07-21. 브랜치 `feature/run-structure-node-map-20260707`.
> 결정: 현 Relic 시스템(enum 규칙 효과 + 인라인 CombatEffect)을 철거하고,
> 구 Artifact 시스템(RuntimeArtifact 추상 클래스, 유물 1개 = 클래스 1개)을 골격으로 재구축한다.
> 현 시스템이 담당하던 런 기능(풀/드랍/상점 진열/세이브/HUD)은 새 골격에 흡수한다.

## 1. 동기

- **유물 1개 = 클래스 1개** — enum+수치 데이터로는 표현력이 부족하다. 클래스 기반이면
  훅/로직 자유도가 스킬·패시브와 같은 수준이 된다.
- **런타임 인스턴스 래핑** — 현 구조는 SO 에셋에 붙은 인라인 `CombatEffect`를 그대로
  리액터로 쓰므로, 상태를 가지는 유물(카운터 등)을 만들면 상태가 에셋에 오염된다.
  구 구조(획득 시 인스턴스 생성)는 이 문제가 없다.
- **매니저 표면 단순화** — 구 ArtifactManager의 Add/Remove API를 승계한다.

네이밍은 **Artifact로 통일** (ArtifactManager / ArtifactData / RuntimeArtifact).

## 2. 아키텍처 (승인된 A안: 프로토타입 인라인 + Clone)

에셋↔클래스 연결은 스킬 시스템과 동일한 프로젝트 idiom을 쓴다:
`ArtifactData`(SO)에 `[SerializeReference, SubclassPicker]`로 효과 클래스를 인라인 편집하고,
획득 시 `CreateInstance`(MemberwiseClone)로 런타임 인스턴스를 만든다.

```csharp
// ArtifactData.cs — SO: 표시 데이터 + 로직 프로토타입
[CreateAssetMenu(fileName = "New ArtifactData", menuName = "DiceOrbit/ArtifactData")]
public class ArtifactData : ScriptableObject
{
    public string artifactName = "유물 이름";
    [TextArea(2, 4)] public string artifactTooltip = "유물 설명";
    public Sprite artifactIcon;
    [Min(1)] public int shopPrice = 120;
    [SerializeReference, SubclassPicker] public RuntimeArtifact effect; // 클래스 선택 + 파라미터 튜닝
}

// RuntimeArtifact.cs — 추상 베이스
[System.Serializable]
public abstract class RuntimeArtifact : ICombatReactor
{
    [System.NonSerialized] public ArtifactData data;   // 획득 시 주입
    public virtual int Priority => 11;                 // 패시브(50~100) 뒤, 모디파이어(10~30) 대역

    // 규칙형 질의 효과 — 필요한 것만 오버라이드 (기본 0)
    public virtual float ShopDiscountPercent  => 0f;   // 상점 가격 -N%
    public virtual float RestHealBonusPercent => 0f;   // 휴식 회복 +N%p
    public virtual int   BattleGoldBonus      => 0;    // 전투 보상 골드 +N
    public virtual float ReviveHpBonusPercent => 0f;   // 점감 부활 HP +N%p
    public virtual int   BattleStartHeal      => 0;    // 전투 시작 시 파티 회복 +N

    public RuntimeArtifact CreateInstance(ArtifactData source)
    {
        var clone = (RuntimeArtifact)MemberwiseClone();
        clone.data = source;
        return clone;
    }
}
```

전투 반응은 `ICombatReactor`의 DIM 훅(OnAttack/OnHeal/OnMove/OnTurnEvent)을
서브클래스가 필요한 것만 구현한다 (`combat_reactor_dispatch.md` 참조).

## 3. 파일 구성

### 신규 — 시스템 (`Assets/Scripts/Core/Run/Artifact/`)
| 파일 | 역할 |
|---|---|
| `ArtifactData.cs` | 위 SO |
| `RuntimeArtifact.cs` | 위 추상 베이스 |
| `ArtifactManager.cs` | 싱글톤: 보유 목록 + 풀/드랍/상점/세이브 창구 (§4) |

### 신규 — 콘텐츠 (`Assets/Scripts/Data/Artifacts/<이름>/<이름>.cs`, 유물 1개 = 폴더 1개)
| 클래스 | 유물 | 효과 |
|---|---|---|
| `PowerfullPunch` | 강력한 주먹 (디버그) | 캐릭터 공격 출력을 `fixedOutput`(기본 1000)으로 고정 |
| `RegularStamp` | 단골 도장 | `ShopDiscountPercent` (기본 20) |
| `CozyBedroll` | 포근한 침낭 | `RestHealBonusPercent` (기본 20) |
| `GoldenDice` | 황금 주사위 | `BattleGoldBonus` (기본 25) |
| `PhoenixFeather` | 불사조 깃털 | `ReviveHpBonusPercent` (기본 15) |
| `LifeAmulet` | 생명의 부적 | `BattleStartHeal` (기본 5) |

`.asset` 파일 생성은 에디터 작업(Unity MCP 또는 수동). 에셋이 없어도 §4의 폴백으로 동작한다.

### 삭제
`Core/Run/RelicManager.cs`, `RelicDefinition.cs`, `RelicCombatEffect.cs`
(`PowerfulPunchEffect`는 `PowerfullPunch` 클래스로 이관 후 삭제)

## 4. ArtifactManager 표면

```csharp
public class ArtifactManager : MonoBehaviour
{
    public static ArtifactManager Instance { get; private set; }
    public static ArtifactManager EnsureInstance();          // 현 RelicManager 패턴 유지

    [SerializeField] List<ArtifactData> artifactPool;        // 획득 후보 (비우면 기본 6종 런타임 생성)
    [SerializeField] List<ArtifactData> startingArtifacts;   // 시작 유물 (테스트/디버그)

    IReadOnlyList<RuntimeArtifact> Artifacts;                // CombatPipeline이 리액터로 직접 수집
    event System.Action OnArtifactsChanged;                  // RunHudUI 칩 갱신

    // 구 API 승계
    void AddArtifact(RuntimeArtifact artifact);              // 인스턴스 직접 추가 (디버그/특수)
    bool RemoveArtifact<T>() where T : RuntimeArtifact;
    bool RemoveArtifact(RuntimeArtifact artifact);

    // 현 RelicManager 승계 (시그니처 유지, 타입만 교체)
    void Grant(ArtifactData data);                           // data.effect.CreateInstance(data) → 추가
    ArtifactData GrantRandom();                              // 엘리트 드랍/이벤트: 미보유 풀에서 랜덤
    List<ArtifactData> GetShopOfferings(int count);          // 상점 진열: 미보유 랜덤 count개
    ArtifactData FindInPool(string artifactName);            // 세이브 복원용
    bool Owns(ArtifactData data);                            // 중복 획득 방지 (data 참조 기준)

    // 규칙형 질의 — 보유 인스턴스 순회 합산. 프로퍼티명 현행 유지 → 소비처 수정 최소화
    float ShopDiscount01;   // Clamp01(Sum(ShopDiscountPercent) / 100)
    float RestHealBonus01;  // Sum(RestHealBonusPercent) / 100
    int   BattleGoldBonus;  // Sum(BattleGoldBonus)
    float ReviveHpBonus01;  // Sum(ReviveHpBonusPercent) / 100
    int   BattleStartHeal;  // Sum(BattleStartHeal)
}
```

- **획득 규칙**: `Grant`는 `Owns(data)`면 무시. `data.effect == null`이면 에러 로그 + 무시
  (셋업 실수 조기 발견). 성공 시 `OnArtifactsChanged` 발화.
- **폴백**: `artifactPool`이 비어 있으면 §3 콘텐츠 중 규칙형 5종(단골 도장~생명의 부적)의
  `ArtifactData`를 `ScriptableObject.CreateInstance`로 런타임 생성해 채운다 (에셋 셋업
  전에도 동작 — 현 RelicManager의 기본 5종 폴백 승계). PowerfullPunch는 디버그 전용이라
  폴백 풀에 넣지 않는다.
- **구 코드와의 차이**: `Start()`에서 디버그 유물 강제 지급(ResetArtifacts)은 하지 않는다 —
  `startingArtifacts` 인스펙터 목록으로 대체 (현 RelicManager 방식).

## 5. 소비처 마이그레이션 — 전부 기계적 치환

| 소비처 | 변경 |
|---|---|
| `GameFlowManager.cs` (272, 466) | `RelicManager` → `ArtifactManager` (RestHealBonus01 / EnsureInstance) |
| `WaveManager.cs` (55) | 〃 (BattleStartHeal) |
| `Character.cs` (450) | 〃 (ReviveHpBonus01) |
| `RewardUI.cs` (118, 136~) | 〃 (BattleGoldBonus / GetShopOfferings / Grant) |
| `EventDefinition.cs` (122) | 〃 (GrantRandom) |
| `ShopUI.cs` (194~) | 〃 + `RelicDefinition`→`ArtifactData`, 필드명 치환 (`RelicName`→`artifactName`, `Description`→`artifactTooltip`, `Icon`→`artifactIcon`, `ShopPrice`→`shopPrice`) |
| `RunHudUI.cs` (76, 143) | 〃 + 칩 데이터를 `RuntimeArtifact.data`에서 읽기. `data == null`인 인스턴스(`AddArtifact` 직접 추가분)는 클래스명으로 표시 |
| `RunSaveService.cs` (83~) | 저장 필드 `RelicNames` → `ArtifactNames`. 복원 = `FindInPool(name)` → `Grant` |
| `CombatPipeline.cs` (130~) | D단계 수집을 `ArtifactManager.Instance.Artifacts`로 (인스턴스 자체가 ICombatReactor) |

**세이브 호환**: 기존 세이브의 유물 보유만 소실된다 (런 진행/골드/포션은 유지).
프로토타입 단계이므로 수용. 마이그레이션 코드는 쓰지 않는다.

## 6. 규약 (유물 클래스 작성 규칙)

1. `[System.Serializable]` 필수 (SubclassPicker 인라인 편집용). 파라미터는 public 필드로
   노출해 에셋에서 튜닝.
2. `CreateInstance`는 **얕은 복사** — 프로토타입 필드는 값 타입/불변만. 참조형 상태가
   필요하면 획득 후 런타임에서만 초기화.
3. 상태를 가지는 유물(카운터 등)은 파이프라인 훅에서 `if (!context.IsSimulation)` 가드 필수
   (예상 피해 시뮬레이션 중 부수효과 금지 — 파이프라인 기존 규칙).
4. 규칙형 효과는 virtual 프로퍼티 오버라이드, 전투 반응은 DIM 훅 구현 — 한 유물이 둘 다
   가져도 된다.

## 7. 검증

- 컴파일: Unity MCP 콘솔로 에러 확인.
- 플레이 검증(사용자, 에디터): ① 엘리트 클리어 → 유물 드랍 + HUD 칩 표시
  ② 상점 진열/구매/할인(단골 도장) ③ 휴식 보너스(침낭) ④ 부활 HP 보너스(깃털)
  ⑤ 전투 시작 회복(부적) ⑥ 보상 골드 보너스(황금 주사위) ⑦ PowerfullPunch 공격 출력 고정
  ⑧ 세이브 → 이어하기로 유물 복원.
