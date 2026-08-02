# 주사위 덱 Phase 1 (덱 토대) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development(권장) 또는 superpowers:executing-plans 로 태스크 단위 구현. 스텝은 체크박스(`- [ ]`)로 추적.

**Goal:** 플레이어 소유 주사위 덱(커스텀 6면 + 사용 시 효과)을 도입하고, 매 턴 덱을 굴리며, 주사위 호버 시 면·효과를 표시한다.

**Architecture:** 주사위 = `DieDefinitionSO`(에셋). 덱 슬롯 = `DieInstance`(베이스 SO + 붙은 효과). `DiceDeckManager`가 덱을 소유(파티 모집 시 표준 주사위 2개/명 시드). `DiceManager.RollDice`가 파티×2 익명 생성 대신 덱을 굴린다. 사용 확정 시 `DiceManager.MarkUsed`가 효과를 발동. 효과는 이벤트/유물과 같은 `[SerializeReference]` 다형성.

**Tech Stack:** Unity 6000.3.8f1, C#, TextMeshPro, New Input System(EventSystem). **테스트 프레임워크 없음** → 검증은 **컴파일 체크 + 플레이 모드 육안**(스펙 §11).

**스펙:** `Docs/superpowers/specs/2026-07-29-dice-deck-design.md` (Phase 1 = §3~5).

## 검증 방법 (모든 태스크 공통)

- **컴파일 체크**: Unity 에디터 포커스 → 자동 재컴파일 후 콘솔에 `error CS` 0개. (에디터 접근 불가 시: 헤드리스 MSBuild — 새 파일은 `Assembly-CSharp.csproj`에 없을 수 있어 에디터 리프레시가 정석.)
- **플레이 검증**: 각 태스크 말미의 "플레이 확인" 참고. 오버레이 UI라 캡처 대신 육안.

## 파일 구조 (Phase 1)

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieEffect.cs` (신규) | 효과 추상 클래스 + 컨텍스트 + 구체 효과 2종 |
| `.../Dices/DieDefinitionSO.cs` (신규) | 주사위 정의 SO + `DieInstance` 덱 슬롯 |
| `.../Dices/DiceData.cs` (수정) | `Source` 참조 + 값 클램프 완화 |
| `.../Dices/DiceDeckManager.cs` (신규) | 덱 소유·시드·API |
| `.../Dices/DiceManager.cs` (수정) | 덱 굴림, 파티×2 제거, `MarkUsed` |
| `Assets/Scripts/UI/DiceUI.cs` (수정) | `MarkDiceAsUsed`에서 상태 세팅 제거(중앙화) |
| `Assets/Scripts/UI/CharacterActionUI.cs` (수정) | 사용 경로가 `DiceManager.MarkUsed` 호출 |
| `Assets/Scripts/UI/DiceElement.cs` (수정) | 호버 핸들러 |
| `Assets/Scripts/UI/DiceHoverTooltipUI.cs` (신규) | 호버 툴팁(면 2×3 + 효과) |

---

## Task 1: 효과 모델 (DieEffect + 구체 효과)

**Files:**
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieEffect.cs`

- [ ] **Step 1: 파일 작성**

```csharp
using UnityEngine;
using DiceOrbit.Core;   // Character, GoldManager, SubclassPickerAttribute

namespace DiceOrbit.Data
{
    /// <summary>주사위 사용 확정 시 넘어오는 컨텍스트 (최소).</summary>
    public class DieUseContext
    {
        public Character User;      // 이 주사위를 배정해 행동한 캐릭터 (null 가능)
        public int RolledValue;     // 이번에 굴려 나온 면 값
    }

    /// <summary>
    /// 주사위 '사용 시 효과' — 이벤트 결과(EventOutcome)/유물과 같은 [SerializeReference] 다형성.
    /// 새 효과 = 이 클래스를 상속한 클래스 하나 추가.
    /// </summary>
    [System.Serializable]
    public abstract class DieEffect
    {
        public Sprite Icon;                              // 호버 효과 행 아이콘(선택)
        public abstract string Apply(DieUseContext ctx); // 발동 + 사람이 읽을 요약 반환
        public virtual string Preview() => "";           // 호버 라벨용 짧은 설명
    }

    /// <summary>사용 시 골드 +N.</summary>
    [System.Serializable]
    public class GainGoldOnUse : DieEffect
    {
        public int amount = 20;
        public override string Apply(DieUseContext ctx) { GoldManager.EnsureInstance().AddGold(amount); return $"골드 +{amount}"; }
        public override string Preview() => $"골드 +{amount}";
    }

    /// <summary>사용 시 사용한 캐릭터 HP +N.</summary>
    [System.Serializable]
    public class HealUserOnUse : DieEffect
    {
        public int amount = 5;
        public override string Apply(DieUseContext ctx)
        {
            var u = ctx?.User;
            if (u != null && u.IsAlive && u.Stats != null)
                u.Stats.CurrentHP = Mathf.Min(u.Stats.MaxHP, u.Stats.CurrentHP + amount);
            return $"HP +{amount}";
        }
        public override string Preview() => $"HP +{amount}";
    }
}
```

- [ ] **Step 2: 컴파일 체크** — Unity 리프레시 후 콘솔 `error CS` 0개. (검증 포인트: `GoldManager`/`Character`/`SubclassPickerAttribute`가 `DiceOrbit.Core`에서 해결됨. 안 되면 정확한 네임스페이스 using 추가.)

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieEffect.cs"
git commit -m "feat(dice): 주사위 사용 효과 모델(DieEffect) + 구체 효과 2종"
```

---

## Task 2: 주사위 정의 SO + 덱 슬롯 (DieDefinitionSO, DieInstance)

**Files:**
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieDefinitionSO.cs`

- [ ] **Step 1: 파일 작성**

```csharp
using UnityEngine;
using DiceOrbit.Core;   // SubclassPickerAttribute

namespace DiceOrbit.Data
{
    /// <summary>
    /// 주사위 한 종류 = 에셋 1개. 표준(면 1~6, 효과 없음) 및 특수 주사위 모두 이 SO로 만든다.
    /// saveId = 에셋 파일명 (후속 세이브 리팩터의 SaveIdCatalog와 정합).
    /// </summary>
    [CreateAssetMenu(fileName = "Die", menuName = "DiceOrbit/Die Definition")]
    public class DieDefinitionSO : ScriptableObject
    {
        public string Name = "표준 주사위";
        public int[] Faces = { 1, 2, 3, 4, 5, 6 };       // 길이 6 권장
        [SerializeReference, SubclassPicker] public DieEffect Effect;  // null = 효과 없음
        public Sprite Icon;                              // 보상/교체 목록 표시용

        public int RollFace()
            => (Faces != null && Faces.Length > 0) ? Faces[Random.Range(0, Faces.Length)] : 1;
    }

    /// <summary>
    /// 덱의 한 칸 (런타임). 베이스 주사위(SO) + 이벤트로 붙은 효과(선택).
    /// 교체 = BaseDie 스왑 / 효과 부여 = AttachedEffect 세팅 (Phase 2~3).
    /// </summary>
    public class DieInstance
    {
        public DieDefinitionSO BaseDie;
        public DieEffect AttachedEffect;   // 없으면 BaseDie.Effect 사용

        public DieInstance(DieDefinitionSO baseDie) { BaseDie = baseDie; }

        public int[] Faces => BaseDie != null ? BaseDie.Faces : System.Array.Empty<int>();
        public DieEffect Effect => AttachedEffect ?? BaseDie?.Effect;
        public int RollFace() => BaseDie != null ? BaseDie.RollFace() : 1;
    }
}
```

- [ ] **Step 2: 컴파일 체크** — 콘솔 `error CS` 0개.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DieDefinitionSO.cs"
git commit -m "feat(dice): DieDefinitionSO(주사위 에셋) + DieInstance(덱 슬롯)"
```

---

## Task 3: DiceData에 Source 추가 + 값 클램프 완화

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DiceData.cs`

- [ ] **Step 1: 필드/프로퍼티/생성자 추가 + 클램프 완화**

`private DiceState state;` 아래에 필드 추가:
```csharp
        // 이 주사위가 나온 덱 슬롯 (효과 발동 + 호버 표시용). 직렬화하지 않음.
        [System.NonSerialized] private DieInstance source;
```

`public object AssignedCharacter => assignedCharacter;` 아래에 프로퍼티 추가:
```csharp
        public DieInstance Source => source;
```

기존 생성자 `public DiceData(int id, int value)` 를 **다음으로 교체**(클램프 완화 + Source 오버로드):
```csharp
        public DiceData(int id, int value) : this(id, value, null) { }

        public DiceData(int id, int value, DieInstance source)
        {
            this.id = id;
            this.value = value;           // 면 값이 권위 — 1~6 하드 클램프 제거
            this.state = DiceState.Available;
            this.assignedCharacter = null;
            this.source = source;
        }
```

`SetValue`의 클램프도 완화:
```csharp
        public void SetValue(int newValue)
        {
            value = newValue;             // 커스텀 면 값 허용 (구 Mathf.Clamp(1,6) 제거)
        }
```

- [ ] **Step 2: 컴파일 체크** — 콘솔 `error CS` 0개. (`DieInstance`는 같은 `DiceOrbit.Data` 네임스페이스라 using 불필요.)

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DiceData.cs"
git commit -m "feat(dice): DiceData에 덱 슬롯(Source) 참조 + 면 값 클램프 완화"
```

---

## Task 4: DiceDeckManager (덱 소유 · 시드 · API)

**Files:**
- Create: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DiceDeckManager.cs`

- [ ] **Step 1: 파일 작성**

```csharp
using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 플레이어 소유 주사위 덱의 단일 출처 (ArtifactManager/PotionManager와 대칭).
    /// 기본 덱 = 캐릭터당 표준 주사위 2개(모집 시 +2). 교체/효과 부여는 Phase 2~3.
    /// 저장은 이번 범위 밖 — 이어하기 시 파티 인원 기준 기본 덱으로 복귀.
    /// </summary>
    public class DiceDeckManager : MonoBehaviour
    {
        public static DiceDeckManager Instance { get; private set; }

        [Header("덱 구성")]
        [Tooltip("시드용 표준 주사위 (면 1~6, 효과 없음)")]
        [SerializeField] private DieDefinitionSO standardDie;
        [Tooltip("보상 획득용 특수 주사위 풀 (Phase 2에서 사용)")]
        [SerializeField] private List<DieDefinitionSO> specialPool = new List<DieDefinitionSO>();
        [Tooltip("캐릭터 1명당 시드되는 표준 주사위 수")]
        [SerializeField] private int diePerCharacter = 2;

        private readonly List<DieInstance> deck = new List<DieInstance>();
        public IReadOnlyList<DieInstance> Deck => deck;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public static DiceDeckManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            Instance = FindAnyObjectByType<DiceDeckManager>(FindObjectsInactive.Include);
            if (Instance == null)
                Debug.LogWarning("[DiceDeckManager] 씬에 인스턴스가 없습니다. 씬에 배치하고 standardDie를 배선해주세요.");
            return Instance;
        }

        private void Start()
        {
            SyncDeckToParty();
            var pm = PartyManager.Instance;
            if (pm != null) pm.OnPartyChanged += HandlePartyChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var pm = PartyManager.Instance;
            if (pm != null) pm.OnPartyChanged -= HandlePartyChanged;
        }

        private void HandlePartyChanged(int partySize) => SyncDeckToParty();

        /// <summary>덱이 파티 인원×diePerCharacter 만큼 되도록 표준 주사위를 채운다(부족분만 추가 — 특수/부여분 보존).</summary>
        public void SyncDeckToParty()
        {
            int target = Mathf.Max(0, (PartyManager.Instance?.PartySize ?? 0) * Mathf.Max(1, diePerCharacter));
            while (deck.Count < target && standardDie != null)
                deck.Add(new DieInstance(standardDie));
            if (standardDie == null && deck.Count < target)
                Debug.LogWarning("[DiceDeckManager] standardDie 미배선 — 덱을 채울 수 없습니다.");
        }

        // ── Phase 2~3 API (지금은 로직만 준비, 호출부는 후속) ──
        public DieDefinitionSO DrawRandomSpecial()
            => (specialPool != null && specialPool.Count > 0) ? specialPool[Random.Range(0, specialPool.Count)] : null;

        public void Replace(int index, DieDefinitionSO newBase)
        {
            if (index < 0 || index >= deck.Count || newBase == null) return;
            deck[index].BaseDie = newBase;
            deck[index].AttachedEffect = null;   // 교체 시 붙은 효과 초기화
        }

        public void AttachEffect(int index, DieEffect effect)
        {
            if (index < 0 || index >= deck.Count) return;
            deck[index].AttachedEffect = effect;
        }
    }
}
```

- [ ] **Step 2: 컴파일 체크** — 콘솔 `error CS` 0개.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DiceDeckManager.cs"
git commit -m "feat(dice): DiceDeckManager — 덱 소유·파티 시드·교체/부여 API"
```

---

## Task 5: DiceManager를 덱 굴림으로 전환 + MarkUsed

**Files:**
- Modify: `Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DiceManager.cs`

- [ ] **Step 1: 파티×2 필드/로직 제거**

인스펙터 필드에서 제거: `usePartyBasedDiceCount`, `dicePerCharacter`, `diceCountPerTurn`(선택 유지 가능하나 미사용). 메서드 제거: `RefreshDiceCountFromParty()`, `HandlePartyChanged(int)`. `Start()`/`OnDestroy()`의 `PartyManager.OnPartyChanged` 구독/해제 라인 제거(덱 관리는 DiceDeckManager로 이관). `minDiceValue`/`maxDiceValue`는 재굴림 폴백용으로 남겨도 됨.

`Start()`는 최소로:
```csharp
        private void Start() { }   // 파티 기반 개수 동기화 제거 (덱은 DiceDeckManager 소유)
```

- [ ] **Step 2: `RollDice`를 덱 굴림으로 교체**

`public void RollDice()` 와 `public void RollDice(int count)` 를 **다음으로 교체**:
```csharp
        /// <summary>소유 덱을 굴린다 — 각 DieInstance의 면에서 랜덤 1개.</summary>
        public void RollDice()
        {
            currentDice.Clear();

            var deck = DiceDeckManager.EnsureInstance()?.Deck;
            if (deck == null || deck.Count == 0)
            {
                Debug.LogWarning("[DiceManager] 덱이 비어 있습니다 — 굴릴 주사위 없음. DiceDeckManager/standardDie 확인.");
            }
            else
            {
                foreach (var inst in deck)
                    currentDice.Add(new DiceData(diceIdCounter++, inst.RollFace(), inst));
            }

            // 일기예보 바이어스 (기존 유지)
            if (_forecastBiasTurnsLeft > 0 && _forecastBiasPercent > 0)
            {
                foreach (var die in currentDice)
                    if (die.Value > 3 && Random.value < _forecastBiasPercent / 100f)
                        die.SetValue(Random.Range(1, 4));
                _forecastBiasTurnsLeft--;
            }

            if (diceUI != null) diceUI.DisplayDice(currentDice);
            OnDiceRolled?.Invoke(currentDice);
        }
```

- [ ] **Step 3: 재굴림을 면 기반으로**

`RerollAvailableDice()` 안의 재굴림 루프를 교체:
```csharp
            foreach (var die in available)
                die.SetValue(die.Source != null ? die.Source.RollFace() : Random.Range(minDiceValue, maxDiceValue + 1));
```

- [ ] **Step 4: `MarkUsed` 추가**

`AssignDice` 아래에 추가:
```csharp
        /// <summary>주사위 사용 확정 — 상태를 Used로, 부착 효과를 발동, 시각 제거.</summary>
        public void MarkUsed(DiceData dice, Character user)
        {
            if (dice == null) return;
            dice.State = DiceState.Used;

            var effect = dice.Source?.Effect;
            if (effect != null)
            {
                string summary = effect.Apply(new DieUseContext { User = user, RolledValue = dice.Value });
                Debug.Log($"[DiceManager] 주사위 효과 발동: {summary}");
            }

            diceUI?.MarkDiceAsUsed(dice);   // 시각 제거(상태는 위에서 세팅)
        }
```

- [ ] **Step 5: 컴파일 체크** — 콘솔 `error CS` 0개. (`Character`는 같은 `DiceOrbit.Core`, `DieUseContext`는 `DiceOrbit.Data` — DiceManager는 이미 `using DiceOrbit.Data;` 보유.)

- [ ] **Step 6: 커밋**

```bash
git add "Assets/Scripts/Core/Stage/BattleStage/BattleStageSystem/Dices/DiceManager.cs"
git commit -m "feat(dice): 덱 굴림으로 전환(파티x2 제거) + MarkUsed로 사용 효과 발동"
```

---

## Task 6: 사용 경로를 MarkUsed로 배선

**Files:**
- Modify: `Assets/Scripts/UI/DiceUI.cs:161`
- Modify: `Assets/Scripts/UI/CharacterActionUI.cs:565-569`

- [ ] **Step 1: DiceUI.MarkDiceAsUsed에서 상태 세팅 제거(중앙화)**

`Assets/Scripts/UI/DiceUI.cs`의 `MarkDiceAsUsed`에서 이 줄 제거:
```csharp
            diceData.State = DiceState.Used;
```
→ 상태는 이제 `DiceManager.MarkUsed`가 세팅. `MarkDiceAsUsed`는 시각 갱신/제거만 담당. (직접 호출 시에도 안전하도록 나머지는 그대로 둔다.)

- [ ] **Step 2: CharacterActionUI가 DiceManager.MarkUsed 호출**

`Assets/Scripts/UI/CharacterActionUI.cs`의 `MarkDiceUsed` 교체:
```csharp
        private void MarkDiceUsed(DiceData dice)
        {
            Core.DiceManager.Instance?.MarkUsed(dice, currentCharacter);
        }
```
(검증 포인트: `currentCharacter`가 `DiceOrbit.Core.Character` 타입인지 확인 — `MarkUsed(DiceData, Character)` 시그니처와 일치해야 함.)

- [ ] **Step 3: 컴파일 체크** — 콘솔 `error CS` 0개.

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Scripts/UI/DiceUI.cs" "Assets/Scripts/UI/CharacterActionUI.cs"
git commit -m "feat(dice): 사용 확정 경로를 DiceManager.MarkUsed로 중앙화"
```

- [ ] **Step 5: 에디터 셋업 (코드 밖 — 여기서 첫 플레이 검증 가능)**

1. `Create > DiceOrbit > Die Definition` 로 **표준 주사위** 에셋 생성(`Assets/.../Dice/StandardDie.asset`), Faces `1~6`, Effect 없음.
2. 빈 GameObject에 `DiceDeckManager` 부착, `standardDie`에 위 에셋 배선. BattleScene에 배치.
3. (효과 확인용) 특수 주사위 1개 생성: Faces 예 `1,1,2,2,3,3`, Effect = `GainGoldOnUse`(amount 20). `standardDie` 대신 임시로 이걸 시드로 써서 효과 발동을 확인해도 됨.

- [ ] **Step 6: 플레이 확인**
전투 진입 → 파티×2개(= 덱 수) 주사위가 굴려짐 / 스킬에 주사위 확정 사용 시 콘솔에 `[DiceManager] 주사위 효과 발동: …`(효과 주사위인 경우) / 취소 시 미발동 / 파티 모집 시 주사위 2개 증가.

---

## Task 7: 호버 UI (DiceElement + DiceHoverTooltipUI)

**Files:**
- Modify: `Assets/Scripts/UI/DiceElement.cs`
- Create: `Assets/Scripts/UI/DiceHoverTooltipUI.cs`

- [ ] **Step 1: DiceHoverTooltipUI 작성**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 주사위 호버 툴팁 — 호버한 주사위 위에 6면(2줄×3, 실제 주사위 이미지)과 사용 효과를 표시.
    /// 씬에 배치하거나 EnsureInstance로 자동 생성. 캔버스는 최상단 정렬.
    /// </summary>
    public class DiceHoverTooltipUI : MonoBehaviour
    {
        public static DiceHoverTooltipUI Instance { get; private set; }

        [Header("참조 (씬 배치 시)")]
        [SerializeField] private RectTransform panel;        // 툴팁 루트 패널
        [SerializeField] private RectTransform faceGrid;     // 2줄×3 GridLayoutGroup
        [SerializeField] private RectTransform effectRow;    // 효과 행 (없으면 숨김)
        [SerializeField] private Image effectIcon;
        [SerializeField] private TextMeshProUGUI effectLabel;
        [SerializeField] private Sprite dieFaceSprite;       // Assets/Sprites/Dice.png
        [SerializeField] private Vector2 aboveOffset = new Vector2(0f, 90f);

        private readonly List<GameObject> _faceCells = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static DiceHoverTooltipUI EnsureInstance()
        {
            if (Instance != null) return Instance;
            Instance = FindAnyObjectByType<DiceHoverTooltipUI>(FindObjectsInactive.Include);
            if (Instance == null)
                Debug.LogWarning("[DiceHoverTooltipUI] 씬에 인스턴스가 없습니다. 패널을 배치해주세요.");
            return Instance;
        }

        public void Show(DiceElement element)
        {
            var src = element != null ? element.Data?.Source : null;
            if (src == null || panel == null) return;

            BuildFaces(src.Faces);

            var effect = src.Effect;
            if (effectRow != null) effectRow.gameObject.SetActive(effect != null);
            if (effect != null)
            {
                if (effectLabel != null) effectLabel.text = effect.Preview();
                if (effectIcon != null)
                {
                    effectIcon.enabled = effect.Icon != null;
                    if (effect.Icon != null) effectIcon.sprite = effect.Icon;
                }
            }

            panel.gameObject.SetActive(true);
            // 호버한 주사위 위에 배치
            panel.position = (Vector2)element.transform.position + aboveOffset;
            panel.SetAsLastSibling();
        }

        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        /// <summary>faceGrid 아래에 면 6칸을 채운다 (각 칸 = 주사위 이미지 + 값). GridLayoutGroup가 2줄×3로 정렬.</summary>
        private void BuildFaces(int[] faces)
        {
            if (faceGrid == null) return;
            foreach (var c in _faceCells) Destroy(c);
            _faceCells.Clear();
            if (faces == null) return;

            foreach (int v in faces)
            {
                var cell = new GameObject("FaceCell", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(faceGrid, false);
                var img = cell.GetComponent<Image>();
                img.sprite = dieFaceSprite;
                img.raycastTarget = false;

                var txt = new GameObject("Val", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                txt.transform.SetParent(cell.transform, false);
                txt.text = v.ToString();
                txt.alignment = TextAlignmentOptions.Center;
                txt.raycastTarget = false;
                txt.enableAutoSizing = true;
                var rt = txt.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

                _faceCells.Add(cell);
            }
        }
    }
}
```

- [ ] **Step 2: DiceElement에 호버 핸들러 추가**

`Assets/Scripts/UI/DiceElement.cs` — 클래스 선언에 인터페이스 추가:
```csharp
    public class DiceElement : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
```
`OnPointerClick` 아래에 추가:
```csharp
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (diceData?.Source == null) return;
            DiceHoverTooltipUI.EnsureInstance()?.Show(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            DiceHoverTooltipUI.Instance?.Hide();
        }
```
(`IPointerEnterHandler`/`IPointerExitHandler`는 `UnityEngine.EventSystems` — 파일 상단에 이미 `using UnityEngine.EventSystems;` 있음.)

- [ ] **Step 3: 컴파일 체크** — 콘솔 `error CS` 0개.

- [ ] **Step 4: 커밋**

```bash
git add "Assets/Scripts/UI/DiceHoverTooltipUI.cs" "Assets/Scripts/UI/DiceElement.cs"
git commit -m "feat(dice): 주사위 호버 툴팁(면 2x3 + 효과) + DiceElement 호버 핸들러"
```

- [ ] **Step 5: 에디터 셋업**
1. Canvas 아래에 `DiceHoverTooltipUI` 패널 구성: 루트 `panel`(비활성 시작), 그 안에 `faceGrid`(GridLayoutGroup, constraint=고정 열 3, 셀 크기 적당), `effectRow`(Image `effectIcon` + `effectLabel`).
2. `dieFaceSprite`에 `Assets/Sprites/Dice.png` 배선. TMP 폰트 Pretendard SDF.

- [ ] **Step 6: 플레이 확인**
주사위에 마우스 올림 → 위에 6면(주사위 이미지 + 값, 2줄×3) 표시 / 효과 주사위는 효과 행(아이콘+요약) 표시, 표준은 효과 행 숨김 / 마우스 벗어나면 사라짐.

---

## Self-Review 체크 (작성자 확인 완료)

- **스펙 커버리지**: §3 데이터 모델(T1,T2,T3) / §4 덱·굴림·MarkUsed(T4,T5,T6) / §5 호버(T7) — Phase 1 전 항목에 태스크 대응. Phase 2·3은 별도 계획.
- **타입 일관성**: `DieInstance.RollFace/Faces/Effect`, `DiceData.Source`, `DiceManager.MarkUsed(DiceData, Character)`, `DieUseContext{User,RolledValue}` — 태스크 간 시그니처 일치.
- **플레이스홀더**: 없음(모든 스텝에 실제 코드/명령).
- **통합 검증 포인트 명시**: 네임스페이스 해결(T1 Step2), `currentCharacter` 타입(T6 Step2), Dice.png 배선(T7).

## 후속

- **Phase 2 (보상·교체)** / **Phase 3 (효과 부여)** 는 Phase 1 완료·플레이 검증 후 각각 계획 작성.
