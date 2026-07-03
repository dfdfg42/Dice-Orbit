# 레거시/데드코드 감사 리포트 (Assets/Scripts 전체)

> **업데이트 2026-07-03**: 이 리포트 이후 Tier 1-B 항목 일부가 실제 삭제됨 — `SampleMonster.cs`(`SampleAttack`/`SampleDeath`/`SamplePassive`), `SampleTile.cs`(`SampleTileAttribute`), `HoneyTile.cs`(`HoneyTileAttribute`/`HoneyDebuff`). Honey 로직은 이제 `Wave2/HoneyPawTile.cs`에 존재. 나머지 Tier-1 항목은 아직 미조치(un-actioned). 아래 티어별 본문은 원본 그대로 유지.

> 작성일 2026-07-02 · 브랜치 `refactor/combat-context-merge-20260621`
> 방식: 9개 폴더 청크 finder → 청크별 adversarial verifier(전체 트리 grep + `.cs.meta guid` → 씬/프리팹/에셋 조회) → whole-file/public 삭제 건은 독립 refuter로 재검증.
> **상태: 리포트 전용. 아직 아무것도 삭제하지 않음.** 아래 Tier를 승인하면 삭제 진행.

## 검증 범위 / 근거
- 스크립트 143개 전수(Core 58 / Data 39 / UI 34 / Visuals 10 / Editor 2).
- 사용처 대조 대상: 게임 씬 3개(`BattleScene`, `MainMenu`, `TestScene`), 게임 프리팹 13개, ScriptableObject `.asset` 38개.
- 제외: 서드파티(`TextMesh Pro`, `JMO Assets`, `Samples`, `Packages`, `MeshyImports`)와 `_Recovery`(크래시 백업 — 참조돼도 live로 안 침).
- Unity 암묵 사용 벡터 필터 적용: 매직 메서드, `SerializeField`, UnityEvent(m_MethodName), SendMessage, 인터페이스/override 다형성, DIM 기본 훅, enum int 직렬화.

**요약**: 총 120건(중복 포함) → 확실히 죽음 **83** · 아마 죽음 **15** · 불확실 **3** · 오탐(살아있음) **19**.
refuter가 되살린 오탐: `SkillCardUI`, `TargetSelectionOverlay`(타입), `PowerfullPunch`, `GameState.CharacterSelection`, `DiceData.Reset` 등.

---

## Tier 1 — 즉시 삭제 안전 (컴파일/직렬화 영향 없음, 순수 제거)

### 1-A. 통째로 죽은 파일 (파일 + `.meta` 삭제)
| 파일 | 근거 |
|---|---|
| `UI/RecruitUI.cs` | MonoBehaviour인데 guid가 어떤 씬/프리팹/에셋에도 없음. 리크루트 UI는 `CharacterSelectionUI`가 담당. |
| `Combat/Skills/CharacterSkillBase.cs` | 2줄 tombstone 주석만 존재. 타입 자체가 없음(`CharacterSkillTargetType`는 `CharacterActiveTemplate.cs`로 이동됨). |
| `Combat/Skills/PassiveSkillAsset.cs` | 1줄 tombstone 주석만 존재. 타입 없음. |
| `BattleStageSystem/ActionType.cs` | 파일 전체(1-14줄) 주석 처리. live 코드는 `DiceOrbit.Core.Pipeline.ActionType` 사용. |

### 1-B. 죽은 타입 (참조 0, 직렬화 0)
- `PassiveAbility.cs` → `PassiveType` 클래스 (구설계 잔재, 메타데이터는 `PassiveAbility`로 이동).
- `Combat/EffectData.cs` → `EffectData` 클래스 + `GenerateDescription()`. **⚠️ 같은 파일의 `EffectType` enum은 광범위 사용 중 → 반드시 유지.**
- `Combat/SkillData/SkillData.cs` → `SkillTargetType` enum (live인 `CharacterSkillTargetType`와 별개, 이름만 유사).
- `UI/DiceElement.cs` → `IDropZone` 인터페이스 + `GetDropTarget()` (구 드래그드롭 잔재, 현재는 클릭 방식).
- `Data/MonsterPresets/SamplePreset/SampleMonster.cs` → `SampleAttack`/`SampleDeath`/`SamplePassive` ([SerializeReference] 스캐폴딩, 에셋 참조 없음).
- `Data/MonsterPresets/SamplePreset/SampleTile.cs` → `SampleTileAttribute` (`new` 호출 없음).
- `Data/MonsterPresets/Wave2/HoneyTile.cs` → `HoneyTileAttribute` + `HoneyDebuff` (live는 `HoneyPawTile`). *참고: 삭제 시 `EffectType.Honey`/툴팁 항목이 orphan design 잔재로 남지만 별도 파일이라 컴파일 영향 없음.*

### 1-C. 죽은 멤버 (메서드/프로퍼티/필드 — 호출·읽기 0)
- `PassiveAbility.ConfigureMetadata(string,string,Sprite)`
- `CharacterModfierContext.cs` → `WarriorGreatswordModifiedContext.BaseDamageMultiplier` (write-only)
- `ArtifactManager`: `RemoveArtifact<T>()`, `RemoveArtifact(RuntimeArtifact)`
- `PassiveManager.RemovePassive(IPassive)`
- `CombatManager`: `OnMonsterTurnStart` 이벤트(구독자 0), `AttackMonster(...)`, `AttackAllMonsters(...)`, `GetAliveMonsters()`
- `ActionQueueManager`: `ClearQueue()`, `QueueCount`, `IsProcessing`(public getter — **내부 `isProcessing` 필드는 유지**)
- `RuntimeAbility.cs` → `ActiveSkillSlot.GetDescription()`, `GetRequirement()`
- `GameManager.GetCenterZone()`
- `GameFlowManager.OnCombatVictory()` (빈 바디)
- `GoldManager`: `TrySpend(int)`(상점용 speculative), `ResetGold()`
- `CenterZone`: `IsInCenterZone(Vector3)`, `SetRadius(float)` (타입 자체는 live)
- `DiceData`: `AssignedCharacter`(프로퍼티+비직렬화 백킹필드 함께), `AssignedAction`(**getter만** — `[SerializeField] assignedAction` 필드는 유지)
- `DiceManager`: `ResetDice()`(+`DiceData.Reset()` 동반), `GetDice(int)`, `ApplyForecastBias(int,int)`(+`_forecastBias*` 필드 + RollDice bias 분기 = '일기예보' 미완 기능 전체)
- `OrbitManager`: `LevelUpTile`, `GetTileFromStart(int)`
- `Character.SyncPassiveLevelsFromRuntime()`
- `Unit`: `InitializeSystems()`(빈 훅), `UpdateVisuals(Sprite,Color)` + `originalColor` 필드, `Heal()` 내 미사용 지역변수 `hpBefore`
- `PartyManager`: `TryConsumeTeamFirstAction()` + `TeamFirstActionUsed` + `ResetTeamFirstAction()` + `teamFirstActionUsed` 필드 + `CombatManager` 호출 2곳(273,319) = 미완 '팀 선공' 기능 전체
- `ModifierManager.Count`
- `AttackIntent`: 미사용 생성자 2개 — `(IntentType,int,string)`, `(IntentType,TargetType,List<Unit>,Sprite)` (live는 `(MonsterSkill,Monster)`만)
- `ModifierRegistry`: `CreateAll()`, `GetRandomChoices(int)` (live는 `GetRandomChoicesFor(...)`)
- `DiceRollAnimator.EaseInOutQuad(float)` (self-labeled Legacy)
- `CharacterActionUI`: `PopulateSkillList(Character)` + `OnSpecificSkillClicked(int)` (**둘을 함께** — 388줄이 후자를 참조. 관련 `[SerializeField] skillButtonContainer`/`skillSelectButtonPrefab`는 씬에 wiring돼 있어 필드 제거 시 씬 정리 필요 → 메서드만 지우면 필드는 dead-data로 잔존)
- `UI/DiceElement.IsSelected`, `UI/DiceUI.IsPanelVisible` (get-only, 리더 0)
- `UI/CombatNotifier.DefaultStatusColor` (**동명의 `TooltipKeywordFormatter.DefaultStatusColor`는 사용 중 — 건드리지 말 것**)
- `UI/TargetSelectionOverlay.OnOverlayClicked()`
- `UI/MonsterAttackIntentManager`: `highlightedTiles` + `RecalculateHighlightedTiles()`(+호출 163,279) + `tileAttackColor`([SerializeField])
- `Visuals/CharacterSpriteVisual`: `idleSprite`/`moveSprite`/`damageSprite`/`skillSprite` + `spriteOffset` + `ReturnToIdleAfter(float)`(미사용 코루틴)
- `Visuals/TileSkillPreviewManager`: `ShowPassiveRange(IEnumerable<TileData>,TilePreviewStyle)` 2-arg 래퍼 + `_globalPassiveKey` (**keyed 3-arg 오버로드와 `ClearPassiveRangeForKey`는 유지**)

### 1-D. 불필요 using / 주석 코드 (trivial)
- `using` 정리: `ArtifactManager`(VisualScripting+System.Linq), `CombatPipeline`(TextCore.Text), `ArtifactData`(중복 nested `using UnityEngine`), `SkillData`(TextCore.Text + static VisualScripting.Member), `OrbitManager`(VisualScripting.Antlr3...), `UnitStats`/`PowerfullPunch`/`LunaPriest`(static GridLayoutGroup), `RandMineTile`(static Member/GraphicsBuffer/DebugUI 3개). *`CombatPipeline`/`SkillData`의 `System.Linq`는 사용 중이니 유지.*
- 주석 코드 삭제: `OrbitManager`(rotate 라인 87), `Character`(UseSkill 355-358), `Monster`(InitializeFromPreset 81-84), `SolraKnight`(`SolraKnightDeath` /*...*/ 클래스 68-83)
- 빈 override 정리: `BattleCryPassive.ApplyLevel(int)`, `SnowMan.HappySnowmanPassive.ApplyLevel(int)` (base virtual과 동일 no-op)

---

## Tier 2 — 죽었지만 주의 필요 (직렬화/enum 순서/public API)

- **`CombatContext.Type` shim** — write-only(유일 read가 주석). self-labeled `제거 예정`. 삭제하려면 모든 `CombatContext`/`EffectContext`/`Attack·Heal·MoveContext` 생성자에서 `ActionType` 파라미터를 걷어내고 `TurnEventContext.PhaseToType`도 제거해야 함 → **여러 생성자 시그니처를 건드리는 리팩터**(1줄 삭제 아님). 이걸 하면 아래 enum 정리도 같이 가능.
- **`CombatAction.cs`의 `ActionType.Utility`/`OnArrive`/`OnTreaverse`** — 참조 0. 단 중간 enum 멤버라 뒤 멤버 int ordinal이 shift(단, `assignedAction`을 직렬화한 에셋이 하나도 없어 실제 위험 낮음). `OnTreaverse`는 `Traverse` 오타.
- **`GameState.Shop`** — 참조 0이나 enum 재배열 위험(씬은 `currentState:0`만 저장 → 현 위험 낮음). 마지막에서 지우거나 명시적 `= n` 부여 권장.
- **publish-only 이벤트(구독자 0)** — `DiceManager.OnDiceRolled`/`OnDiceUsed`/`OnAllDiceUsed`, `CombatManager.OnCombatStart`/`OnCombatEnd`/`OnMonsterDeath`. 미래 확장점으로 의도됐을 수 있음 → 삭제 시 Invoke 지점도 함께.
- **caller 없는 public setter류(아마 죽음)** — `MonsterUI.SetMonster`, `TooltipKeywordDatabase.TryGet`, `CharacterSpriteVisual.SetSprite`/`SetColor`, `OrbitVisualizer.SetOrbitRadius`/`SetOrbitLineVisible`, `TileSkillPreviewManager.HidePassiveRange()`x2. 컴파일상 삭제 안전하나 "외부 API 의도" 가능성만 확인.
- **`ActionQueueManager.CanExecute`** — pause/resume 기능 미구동. public getter 제거는 안전, 내부 pause 로직 의도성 확인 후.
- **`PassiveAbility.Icon`(불확실)** — getter 미사용이나 `[SerializeField] icon` 백킹필드는 직렬화 데이터 보유 + 패시브 UI 훅 가능성. 프로퍼티만 제거는 저위험, 필드는 유지 권장.
- **`UnitStats.Defense`(불확실/legacy)** — 로직 미사용이나 12개 몬스터 `.asset`(값 0) + `TestHero.prefab`/`TestMonster.prefab`(값 2)에 직렬화됨. 삭제=직렬화 변경(무해하나 의도적으로). `CopyBaseTo`도 함께 정리.

---

## Tier 3 — 리팩터/정리 (삭제 아님, 판단 필요)

- `TooltipKeywordFormatter.AppendKeywordSection` — `FormatMainTooltipText`로 그냥 forwarding하는 legacy shim. 호출 3곳(Monster 353, TileData 169, Character 516)을 inline 후 제거.
- `TileVfxManager.PlayTileEvent`의 `Character actor` 파라미터 — 바디에서 미사용(호출 3곳이 값 전달만). 제거하려면 `TileData` 호출 3곳 동시 수정. actor-aware VFX 확장점일 수도.
- `MonsterSkill.cachedIntent` — 재사용 안 되는 캐시(사실상 지역변수). `[NonSerialized]`라 에셋 영향 없음.
- `Unit.CollectReactors` — `if (passive is ICombatReactor reactor)`에서 `reactor` 바인딩 미사용, `passive`를 add. `is ICombatReactor`로 단순화.
- `RogueAmbushActive` vs `AlchemistThrowActive` — 로직 동일(multiplier 기본값만 2 vs 1). **둘 다 live**(각 preset `.asset`의 SerializeReference). 통합하려면 두 에셋의 SerializeReference 타입명 마이그레이션 필요 → 안전한 blind delete 아님. 별도 스킬 정체성이면 유지도 타당.
- **no-op이지만 호출됨(caller 동반 제거 필요)**: `CharacterSpriteVisual.SetAnimationSprites(...)`+`Character.cs:82`, `TileVisual.SetTileSize(...)`+`OrbitManager.cs:136`. 메서드 단독 삭제 시 컴파일 깨짐.
- **다형 dispatch로 도달되는 빈 override(동작 동일, cosmetic)**: `SnowPrisonTileAttribute.OnTraverse`(base 빈 virtual과 동일). `TileAttribute.OnReact` no-op은 **인터페이스 계약 주의** — DIM 기본 제거 시 계약 유지 확인 필요.

---

## Tier 4 — 유지 (오탐, 삭제 금지)
- `GameState.CharacterSelection` — `GameFlowManager.cs:145` switch case로 참조 + enum ordinal.
- `DiceData.Reset()` — `DiceManager.ResetDice()`에서 호출(단 ResetDice 자체가 dead → 함께 처리 대상).
- `DiceRequirement.MinDiceCount` — 4개 preset 에셋에 직렬화(`MinDiceCount: 1`).
- `SkillCardUI` — `LevelUPCard.prefab`/`LevelUPCard 1.prefab`에 부착(단 두 프리팹은 어디서도 참조 안 됨 → **별도 에셋 정리 후보**).
- `TargetSelectionOverlay`(타입) — `CharacterActionUI.cs:44` 필드로 컴파일 의존(런타임 항상 null이지만 타입 제거 불가).
- `PowerfullPunch` 클래스 — `ArtifactManager.cs:96`에서 `new`(컴파일 의존). 씬 필드 미할당이라 런타임 휴면. 1000뎀 디버그 치트.
- `BattleStageDebugManager` — `BattleScene.unity`에 컴포넌트로 부착(스페이스바 디버그 하네스). 삭제 시 씬의 GameObject/컴포넌트도 제거해야.
- `IntentType.Multi` — `MonsterUI` switch 2곳에서 사용(deprecated 표기). 마지막 멤버라 지워도 재배열 없음, UI 정리 후 은퇴 가능.
- `CharacterPreset.ModifierContextTypeName` — `Warrior.asset`에 값 직렬화 + `CharacterPresetEditor` 참조.
- `CharacterSelector`의 else 진단 블록(61-65) — 런타임 실행 코드(주석 라인 53만 dead).

## 부수 발견 (코드 밖, 별도 판단)
- orphan 프리팹: `Prefabs/LevelUPCard.prefab`, `Prefabs/LevelUPCard 1.prefab`(어디서도 참조 X — `SkillCardUI`와 함께 정리 가능).
- orphan 에셋: `PowerfullPunch.asset`(m_Name만).
- 이름 유사 프리팹: `Skill Button.prefab` vs `SkillButton.prefab` — 이번 코드 감사 범위 밖(수동 확인 권장).
