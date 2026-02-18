```mermaid
flowchart TD
    A[Action 요청]

    %% Pre-Action Phase
    A --> PRE
    subgraph PRE_GROUP [1. Pre-Action 단계]
        direction TB
        PRE[행동 가능 여부 확인]
        PRE_CHECK[기절 / 봉인 등 상태 확인]
        PRE --> PRE_CHECK
    end

    PRE_CHECK -->|행동 가능| CALC
    PRE_CHECK -->|행동 불가| CANCEL[행동 취소]

    %% Calculate Phase
    subgraph CALC_GROUP [2. Calculate 단계]
        direction TB
        CALC[기본 수치]
        CALC_BUFF[버프 / 디버프 적용]
        CALC_RESULT[최종 데미지 / 힐 계산]
        CALC --> CALC_BUFF --> CALC_RESULT
    end

    %% Apply Phase
    CALC_RESULT --> APPLY
    subgraph APPLY_GROUP [3. Apply 단계]
        direction TB
        APPLY[수치 적용]
        APPLY_HP[피해 / 회복 처리]
        APPLY_DEATH[사망 여부 판정]
        APPLY --> APPLY_HP --> APPLY_DEATH
    end

    %% Post-Action / Reaction Phase
    APPLY_DEATH --> POST
    subgraph POST_GROUP [4. Post-Action / Reaction 단계]
        direction TB
        POST[반응 이벤트 처리]
        POST_HIT[OnHit / OnDamaged]
        POST_KILL[OnKill / OnDeath]
        POST --> POST_HIT
        POST --> POST_KILL
    end

    POST_HIT --> END[Action 종료]
    POST_KILL --> END
