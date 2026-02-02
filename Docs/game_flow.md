# 게임 전체 흐름도 (Game Flow Overview)

이 문서는 **Dice Orbit**의 게임 초기화부터 종료까지의 전체 흐름을 설명합니다. (TurnManager 통합 반영)

## 1. 매니저별 역할 및 흐름도 (Manager Responsibility Flow)

각 매니저가 어떤 영역을 담당하고, 서로 어떻게 연결되는지 보여주는 구조도입니다.

```mermaid
graph TD
    %% --- GameFlowManager Area ---
    subgraph GFM [GameFlowManager]
        direction TB
        StateMain[메인 메뉴]
        StateCombat[전투 상태]
        StateRecruit[영입 단계]
        StateReward[보상 단계]
        StateVictory[승리 / 게임오버]
        
        StateMain -->|Start| StateCombat
        StateRecruit --> StateReward
        StateReward -->|Next Wave| StateCombat
        StateReward -->|End| StateVictory
    end

    %% --- WaveManager Area ---
    subgraph WM [WaveManager]
        Spawn[몬스터 스폰]
        CheckWave{웨이브 클리어?}
        
        StateCombat -.->|1. 요청| Spawn
        CheckWave -->|Yes| StateRecruit
    end

    %% --- CombatManager Area ---
    subgraph CM [CombatManager]
        direction TB
        StartBattle[전투 시작]
        TurnCycle{턴 사이클}
        
        %% Player Turn Detail
        subgraph PlayerCycle [플레이어 턴 진행]
            OnTurnStart(턴 시작: 상태이상/패시브 체크)
            PlayerUiAct(UI 활성화 & 입력 대기)
            PreAttack(공격 전: 패시브 체크)
            AttackExec(공격 실행 & 데미지 계산)
            PostAttack(공격 후: 패시브 체크)
        end
        
        MonsterTurn[몬스터 턴]
        BattleEnd[전투 종료]
        
        Spawn -.->|2. 이벤트| StartBattle
        StartBattle --> TurnCycle
        TurnCycle --> OnTurnStart
        OnTurnStart --> PlayerUiAct
        
        PlayerUiAct -->|공격 시| PreAttack
        PreAttack --> AttackExec
        AttackExec --> PostAttack
        PostAttack --> PlayerUiAct
        
        PlayerUiAct -->|턴 종료| MonsterTurn
        MonsterTurn -->|Next Turn| TurnCycle
        
        MonsterTurn -->|All Killed| BattleEnd
        BattleEnd -->|Victory| CheckWave
        BattleEnd -->|Defeat| StateVictory
    end

    %% --- Support Systems Area ---
    subgraph Support [Support Systems]
        Party[PartyManager: 파티 관리]
        Dice[DiceManager: 주사위]
        Skill[SkillManager: 스킬 사용]
        Effect[StatusEffectManager: 도트뎀/버프]
        Passive[PassiveManager: 조건부 발동]
        
        OnTurnStart -.-> Effect
        OnTurnStart -.-> Passive
        PreAttack -.-> Passive
        PostAttack -.-> Passive
    end
```

## 2. 매니저 상호작용 시퀀스 (Interaction Sequence)

시간 순서에 따른 매니저 간의 호출 흐름입니다.

```mermaid
sequenceDiagram
    participant GF as GameFlowManager
    participant WM as WaveManager
    participant CM as CombatManager
    participant PM as PartyManager
    participant SEM as StatusEffectManager
    participant PVM as PassiveManager
    
    Note over GF: 게임 시작 (Start Game)
    GF->>WM: StartFirstWave()
    
    loop Wave Execution
        Note over WM: 웨이브 시작
        WM->>WM: SpawnMonsters()
        WM-->>GF: OnWaveStart (Event)
        GF->>CM: StartCombat()
        
        Note over CM: 전투 루프 시작 (Turn 1)
        CM->>CM: StartPlayerTurn()
        
        Note over PM: [체크포인트 1] 턴 시작
        CM->>PM: Character.OnStartTurn()
        PM->>SEM: Process DoT/Duration (독, 화상 등)
        PM->>PVM: OnTurnStart() (스택 쌓기 등)
        
        loop Player Action
            Note over CM: 플레이어 입력 대기
            opt Player Attacks
                CM->>PM: Use Skill
                PM->>PVM: OnBeforeAttack (데미지 보정)
                PM->>CM: Deal Damage
                PM->>PVM: OnAfterAttack (추가타 등)
            end
        end
        
        CM->>CM: EndPlayerTurn()
        
        Note over CM: 몬스터 턴
        CM->>CM: ExecuteMonsterTurn()
        CM->>PM: DamageCharacter()
        PM->>PVM: OnDamageTaken (피격 시 효과)
        CM->>CM: StartPlayerTurn() (Next Turn)
        
        Note over CM: 모든 몬스터 처치
        CM->>WM: CheckWaveClear()
        WM-->>GF: OnWaveCleared (Event)
    end
```

## 3. 상세 시스템 동작 (Detailed Mechanics)

### A. 상태 이상 & 패시브 체크 시점
사용자의 질문대로 전투 중간중간 지속적으로 체크가 이루어집니다.

1.  **턴 시작 시 (`OnTurnStart`)**:
    *   **StatusEffectManager**: 지속시간 감소, 도트 데미지(독, 화상), 힐(재생) 적용.
    *   **PassiveManager**: 턴 시작 시 발동하는 패시브(예: `FocusStackPassive` 스택 증가) 처리.
2.  **공격 전 (`OnBeforeAttack`)**:
    *   **PassiveManager**: 데미지 계산 전에 개입하여 공격력을 증가시키거나 속성을 부여합니다. (예: `BattleCryPassive`, `PositioningPassive`)
3.  **이동 시 (`OnMove`)**:
    *   **PassiveManager**: 이동 거리에 따른 보너스를 계산합니다. (`StancePassive`, `PositioningPassive`)
4.  **피격 시 (`OnDamageTaken`)**:
    *   **PassiveManager**: 데미지를 입었을 때 발동하는 방어 기제나 반격 로직을 처리합니다.
