```mermaid
flowchart TD
    A[게임 턴 시작]

    %% Player Turn Start (simultaneous processing)
    %% 효과 지속시간 감소는 턴 시작 시 처리됨 (StatusEffect.OnTurnEvent / Phase==TurnStart)
    %% 플레이어(캐릭터)는 임시 방어도 초기화 없음 — owner 체크 누락 TODO 존재
    A --> P_START
    subgraph P_START_GROUP [플레이어 턴 시작 단계]
        direction TB
        P_START[플레이어 턴 시작]
        P_EFFECT[효과 지속시간 감소]
        P_START --> P_EFFECT
    end

    P_EFFECT --> P_ACTION[플레이어 행동]

    %% Player Turn End (simultaneous processing)
    P_ACTION --> P_END
    subgraph P_END_GROUP [플레이어 턴 종료 단계]
        direction TB
        P_END[플레이어 턴 종료]
    end

    %% Monster Turn Start
    %% 몬스터만 임시 방어도를 0으로 초기화 (UnitStats.OnTurnEvent, Monster && MonsterStats)
    P_END --> M_START
    subgraph M_START_GROUP [몬스터 턴 시작 단계]
        direction TB
        M_START[몬스터 턴 시작]
        M_BLOCK[임시 방어도 초기화 (몬스터 전용)]
        M_EFFECT[효과 지속시간 감소]
        M_START --> M_BLOCK
        M_BLOCK --> M_EFFECT
    end

    M_EFFECT --> M_ACTION[몬스터 행동]

    %% Monster Turn End
    M_ACTION --> M_END
    subgraph M_END_GROUP [몬스터 턴 종료 단계]
        direction TB
        M_END[몬스터 턴 종료]
    end

    %% Game Turn End
    M_END --> G_END
    subgraph G_END_GROUP [게임 턴 종료 단계]
        direction TB
        G_END[게임 턴 종료]
        TILE_EFFECT[타일 효과 지속시간 감소]
        G_END --> TILE_EFFECT
    end

    TILE_EFFECT --> A
```
