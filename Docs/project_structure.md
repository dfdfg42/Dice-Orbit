# 프로젝트 전체 구조 (Project Structure Overview)

Dice Orbit 프로젝트의 파일 구조와 시스템 아키텍처에 대한 개요입니다.
(Unity 2022.3 기준, `main` 브랜치 2026-07-03 검증. 게임플레이 스크립트 약 140개.)

## 1. 폴더 구조 (Folder Structure)

주요 스크립트(`Assets/Scripts`)는 다음과 같이 분류되어 있습니다. `Core`는 스테이지 단위로 깊게 중첩되어 있으며, 전투 시스템 전체가 `Core/Stage/BattleStage/BattleStageSystem/Combat` 아래에 모여 있습니다. (`Systems/`, `Core/Managers`, `Core/Pipeline`, `Core/Logic` 폴더는 **존재하지 않습니다.**)

```text
Assets/Scripts/
├── Core/                                    # 게임 진입점 및 핵심 로직
│   ├── CharacterSpawner.cs                  # 캐릭터 스폰 (런타임 Animator AddComponent)
│   ├── GameFlowManager.cs                   # 게임 흐름(메뉴/전투/보상) 상태 전환
│   ├── GameManager.cs / GameState.cs        # 전역 매니저 / 상태 enum
│   ├── GoldManager.cs                       # 재화 관리
│   └── Stage/
│       ├── PartyManager.cs                  # 파티(플레이어 캐릭터) 관리
│       └── BattleStage/
│           ├── BattleStageSystem/           # 전투 스테이지의 모든 시스템
│           │   ├── CombatManager.cs*        # (*Combat/ 하위) 턴 관리
│           │   ├── OrbitManager.cs          # 원형 궤도 보드 / Tiles
│           │   ├── WaveManager.cs           # 웨이브 스폰
│           │   ├── ActionType.cs, CenterZone.cs, Background/Debug Manager
│           │   ├── Combat/                  # === 전투 파이프라인 & 반응형 시스템 ===
│           │   │   ├── Pipeline/            # CombatPipeline, CombatContext, CombatAction, ICombatReactor, CharacterModfierContext
│           │   │   ├── Passive/             # IPassive, PassiveManager, CharacterPassive, PassiveAbility
│           │   │   ├── Effects/             # StatusEffect, StatusEffectManager, BasicEffect
│           │   │   ├── Artifact/            # ArtifactData, ArtifactManager, RuntimeArtifact
│           │   │   ├── Skills/              # CharacterActiveTemplate, RuntimeAbility, (tombstones)
│           │   │   ├── SkillData/           # SkillData
│           │   │   ├── CombatManager.cs, SkillManager.cs, SkillTargetSelector.cs
│           │   │   ├── ActionQueueManager.cs, EffectData.cs
│           │   ├── Dices/                   # DiceData, DiceManager, DiceRequirement
│           │   └── Tile/                    # TileData, TileAttribute
│           └── Units/                       # 유닛 계층
│               ├── Unit.cs, UnitStats.cs
│               ├── Character/               # Character, CharacterPreset, Stats, Selector, Progression
│               │   └── Modifiers/           # CharacterModifier, ModifierManager
│               └── Monster/                 # Monster, MonsterStats, MonsterAI, MonsterSkill, Preset ...
│
├── Data/                                    # 데이터 정의 및 콘텐츠 (SerializeReference / SO)
│   ├── Artifacts/                           # 유물 (예: PowerfullPunch)
│   ├── Character Preset/                    # 클래스별 액티브/패시브 (Warrior/Rogue/Alchemist/Mage)
│   ├── Modifiers/                           # 스킬 강화 모디파이어 (Generic/Warrior/Rogue/Mage/Alchemist) + ModifierRegistry
│   ├── MonsterAI/                           # AI 패턴 (Patterns/)
│   ├── MonsterPresets/                      # 웨이브별 몬스터 프리셋 (Wave1~Wave4)
│   └── Waves/                               # WaveDatabase, WaveSpawnPoint
│
├── Editor/                                  # 커스텀 인스펙터 (CharacterPresetEditor, MonsterPresetEditor)
│
├── Legacy/                                  # (비어 있음 - EMPTY, 파일 없음)
│
├── UI/                                      # 사용자 인터페이스 (평면 구조, 하위 폴더 없음)
│   # MainMenuUI, DiceUI, CharacterActionUI, RecruitUI, CombatNotifier,
│   # FloatingLabelPopup, HoverTooltipUI, GlossaryContainerUI, TooltipKeywordDatabase ...
│
└── Visuals/                                 # 시각적 연출 (VfxManager, CombatVfxProfile, TileVisual ...)
```

## 2. 시스템 아키텍처 (System Architecture)

게임은 크게 **매니지먼트 계층**, **파이프라인 계층**, **반응형 시스템 계층**으로 나뉩니다. 전투의 핵심은 `CombatPipeline`이 다양한 `CombatContext`(추상 베이스)를 처리하고, `ICombatReactor` 구현체들이 타입별(DIM) 훅으로 개입하는 구조입니다.

```mermaid
graph TD
    %% --- Layer Definitions ---
   subgraph Presentation["프레젠테이션 (UI/View)"]
        MainMenuUI
        CombatUI[Combat / Dice / Monster UI]
    end

    subgraph Management ["메인 매니지먼트 (Flow Control)"]
        GFM[GameFlowManager]
        WM[WaveManager]
        CM[CombatManager]
        NoteCM[턴 관리 및 UI 제어]
    end

    subgraph Pipeline ["액션 파이프라인 (Core Logic)"]
        ActionPipe[CombatPipeline]
        Context[CombatContext 추상 베이스]
        Sub[AttackContext / HealContext / MoveContext / TurnEventContext]
    end

    subgraph Reactors ["반응형 시스템 (ICombatReactor 계열)"]
        SysPassive[PassiveManager]
        SysStatus[StatusEffectManager]
        SysArtifact[ArtifactManager]
        SysTile[Tiles / TileData]
        SysMod[ModifierManager]
        SysSkill[SkillManager]
    end

    %% --- Connections ---

    %% Presentation -> Management
    MainMenuUI --> GFM
    CombatUI --> CM

    %% Management Internal
    GFM -->|State Control| WM
    GFM -->|State Control| CM
    WM -->|Spawn Request| CM
    CM -.- NoteCM

    %% Manager -> Pipeline
    CM -->|Use Skill / Attack| ActionPipe
    SysSkill -->|Build Context| ActionPipe

    %% Pipeline Process
    ActionPipe -->|Process| Context
    Context --> Sub

    %% Pipeline -> Reactors (NotifyReactors)
    ActionPipe -->|Notify| SysPassive
    ActionPipe -->|Notify| SysStatus
    ActionPipe -->|Notify| SysArtifact
    ActionPipe -->|Notify| SysTile

    %% Skill prep uses Modifiers
    SysSkill -.->|GenerateContext + ApplyTo| SysMod
```

## 3. 핵심 모듈 설명 (Core Modules)

### 매니저 (Core Layers)
*   **GameFlowManager**: 게임의 진입점. 메인 메뉴, 전투 진입, 승리/패배 상태를 전환하며 전체 흐름을 잡습니다.
*   **CombatManager**: **(턴 관리)** 전투의 턴 순서(Player ↔ Monster)를 관리하고 UI를 제어합니다(`StartPlayerTurn`/`ProgressMonsterTurn` 등). 실제 데미지/효과 로직은 **CombatPipeline**에 위임합니다. 타일 지속시간 틱은 CombatManager 내부에서 직접 호출됩니다(파이프라인 브로드캐스트가 아님).

### 파이프라인 (Pipeline Layer)
*   **CombatContext** (`Combat/Pipeline/CombatContext.cs`): **추상 베이스**입니다. 공통 필드로 `SourceUnit`, `Target`, `IsCancelled`, `IsSimulation`, `Type`(마이그레이션 잔재)을 가집니다.
    *   `EffectContext`(Name/BaseValue/OutputValue/Tags/Effects) → `AttackContext`(+`IsEffected`), `HealContext`
    *   `MoveContext`(Steps), `TurnEventContext`(`EventPhase Phase`; enum = `TurnStart`/`TurnEnd`/`TileTick`)
*   **CombatAction** (`Combat/Pipeline/CombatAction.cs`): 클래스가 **CombatContext로 병합**되어 이제는 `enum ActionType` + `struct ActionEffectInfo`만 남았습니다. `new CombatAction(...)`이나 `context.Action`은 더 이상 존재하지 않습니다.
*   **CombatPipeline** (`Combat/Pipeline/CombatPipeline.cs`): `Instance.Process(CombatContext)`가 **PreAction → Calculate(`OnCalculateOutput`) → Apply → Post(`OnHit`, `OnPostAction`)** 순으로 실행됩니다. `CombatTrigger` enum은 정확히 `{ OnPreAction, OnCalculateOutput, OnHit, OnPostAction }` 뿐이며, 턴 이벤트는 트리거가 아니라 `TurnEventContext`+`EventPhase`로 전달됩니다. `NotifyReactors`가 소스/타겟 패시브, 파티 전체 패시브, 상태 효과, 유물(ArtifactManager), 타일(OrbitManager.Tiles/TileData)로부터 리액터를 수집합니다. 데미지/힐은 `Unit.TakeDamage/Heal`로 바로 적용되고, 상태/버프/디버프/DoT는 `StatusEffectManager.CreateEffect`+`AddEffect`로 처리됩니다.
*   **ICombatReactor** (`Combat/Pipeline/ICombatReactor.cs`): **DIM(default interface method)** 방식. 기본 `OnReact(trigger, context)`가 컨텍스트 구체 타입에 따라 타입별 훅(`OnAttack`/`OnHeal`/`OnMove`/`OnTurnEvent`)으로 분기합니다. 단순 리액터는 필요한 훅만 override하고, 전파형 리액터(PassiveManager/StatusEffectManager/TileData)는 `OnReact` 자체를 override합니다. 상세: `Docs/combat_reactor_dispatch.md`.

### 반응형 시스템 계열 (Reactor Families)
5개 계열이 모두 `ICombatReactor`를 구현합니다: **패시브 / 상태효과 / 유물 / 타일 / 모디파이어**.
*   **Passive System** (`Combat/Passive/`): `CharacterPassiveSkill : IPassive`(캐릭터 패시브), `PassiveAbility : IPassive`(몬스터 패시브) 모두 `[Serializable]` 추상 클래스이며 타입별 DIM 훅을 사용합니다(ScriptableObject 아님). `IPassive : ICombatReactor`. `PassiveManager`가 리액터로서 소속 패시브에 이벤트를 전파합니다.
*   **Effect System** (`Combat/Effects/`): `StatusEffect`는 타입별 DIM 훅을 사용하며, 지속시간은 **턴 시작 시**(`OnTurnEvent`에서 `Phase==TurnStart`) 감소합니다. `StatusEffectManager`가 `CreateEffect`/`AddEffect`와 만료 정리(TurnStart)를 담당합니다. `EffectType` enum은 `EffectData.cs`에 있습니다.
*   **Artifact System** (`Combat/Artifact/`): `ArtifactManager`가 유물 리액터를 파이프라인에 공급합니다.
*   **Tile System** (`BattleStageSystem/Tile/`): `TileData : ICombatReactor`가 `Dictionary<TileAttributeType, TileAttribute>`를 순회하며 `OnReact`를 속성으로 전달합니다. `TileAttribute` 기본 훅은 `OnReact/OnArrive/OnTraverse/OnEndTurn` + `SetOwner/TickDuration`. 지속시간은 `TickTurnEnd`로 처리됩니다.
*   **Modifier System** (`Units/Character/Modifiers/`, `Data/Modifiers/`): 컨텍스트 베이스는 `CharacterModfierContext`(철자 주의: *Modfier*)입니다. `CharacterModifier`는 추상 `ICombatReactor`이며 `OnRefreshSkill(CharacterModfierContext)`를 override합니다. `ModifierManager.ApplyTo(ctx)`가 모디파이어들의 `OnRefreshSkill`을 순회하며 `IsCancelled`에서 멈춥니다. 흐름: `ActiveSkillSlot`이 `skill.GenerateContext(...)` 후 `Modifiers.ApplyTo(ctx)`를 호출.

### 스킬 (Skills)
*   **CharacterActiveSkill** (`Combat/Skills/CharacterActiveTemplate.cs`): `[Serializable]` **추상 클래스**(SerializeReference, ScriptableObject 아님). `Execute/CalculateRawDamage/BuildPreview/Clone`을 제공하고 `[SerializeField] CombatVfxProfile vfxProfile`를 보유. `CharacterSkillTargetType`(OneEnemy/OneAlly/MultiEnemy/... /AllTiles) enum 포함.
*   **ActiveSkillSlot** (`Combat/Skills/RuntimeAbility.cs`): `BaseSkill`(SerializeReference) + `CurrentLevel` + `RuntimeInstance`. 액티브 전용. (`RuntimeAbility`라는 클래스명은 없음.)
*   **SkillManager** (`Combat/SkillManager.cs`): 진입점은 `PrepareSkill(Character, skillIndex, DiceData)`. `SkillTargetSelector`로 대상 선택 또는 즉시 `ActionQueue` 등록. (`ExecuteSkill` 메서드 없음.)
*   **SkillTargetSelector**: 커서/확정 라인은 시전자 기준 **방사형(radial)**. 데미지 미리보기 툴팁은 OneEnemy/MultiEnemy에만. 타일 타겟은 `TileSkillPreviewManager` + `PreviewStyle` 사용.
*   *(제거됨/tombstone: `CharacterSkillBase`, `PassiveSkillAsset`, `SkillAsset`, `CharacterSkill`, `SkillLevelData`, `EffectManager`, `IEffect` 등은 존재하지 않습니다.)*

### 콘텐츠 (Data)
*   **캐릭터**: 정확히 4개 — Warrior/Rogue/Alchemist/Mage (`Data/Character Preset/<Class>/`).
    *   액티브: WarriorGreatswordActive, MageEnergyBallActive, RogueAmbushActive, AlchemistThrowActive
    *   패시브: BattleCryPassive(Warrior), FocusPassive(Mage), PositioningPassive(Rogue), ReagentPrepPassive(Alchemist)
    *   (Meteorologist/Scout/StableReactionPassive/ScoutingPassive 등은 없음.)
*   **CharacterPreset**: `[SerializeReference]` 리스트 **2개** — `StartingActives`(List&lt;CharacterActiveSkill&gt;), `StartingPassives`(List&lt;CharacterPassiveSkill&gt;). (통합 `StartingSkills` 없음.) `ModifierContextTypeName` 필드로 SerializeReference 컨텍스트 타입을 연결.
*   **모디파이어**: `Generic/Warrior/Rogue/Mage/Alchemist` 폴더 + `ModifierRegistry.GetRandomChoicesFor(character, count)`.

### 연출 및 알림 (Visuals / Notifications)
*   **VFX** (`Assets/Scripts/Visuals/`): `CombatVfxProfile`(SO) + `VfxManager`(PlayCast/PlayHit/PlayHeal/PlayTile 등). `vfxProfile`는 `CharacterActiveSkill`에 있고, VFX는 `Execute` 내부에서 인라인 재생됩니다(PlayCast → 대상별 AttackContext → 파이프라인 → PlayHit). 커스텀 VFX 억제는 `context.AddTag("CustomVfx")` 태그로 처리. 프로필은 `Assets/Resources/Skill/VFX/`.
*   **알림/툴팁**: `FloatingLabelPopup`(월드 공간), `CombatNotifier`(NotifyStatus/NotifyPassive + 스태거), `HoverTooltipUI` + `GlossaryContainerUI` + `TooltipKeywordDatabase` + `IHoverTooltipProvider`(Character/Monster/TileData).
