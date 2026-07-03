# Dice Orbit

Unity 기반 턴제 전략 로그라이크 게임 프로토타입

---

## 게임 개요

캐릭터들이 원형 궤도 위를 이동하며 웨이브 몬스터와 전투하는 턴제 전략 게임입니다.
주사위를 굴려 나온 눈금을 캐릭터에 할당해 이동 또는 스킬을 사용하고, 전투 파이프라인을 통해 패시브·상태이상이 자동으로 반응합니다.

- **장르**: 턴제 전략 로그라이크
- **핵심 메카닉**: 주사위 할당 + 원형 궤도 이동 + 웨이브 전투
- **엔진**: Unity 2022 이상

---

## 현재 구현 기능

### 코어 시스템
- 원형 궤도 타일 (20개, NextTile/PreviousTile 순환 연결)
- 주사위 굴리기 및 드래그 & 드롭 할당
- 턴 예산 시스템 (캐릭터당 이동 1회 + 행동 1회)
- 웨이브 기반 몬스터 소환 및 승리/패배 판정
- 액션 큐 (이동·스킬이 순서대로 처리)

### 캐릭터 시스템
- 캐릭터 4종: 전사, 마법사, 연금술사, 도적
- 캐릭터 선택 화면 (카드 선택 + 입장 애니메이션)
- 레벨업 타일 통과 시 경험치 획득 및 스킬 레벨업
- 파티 최대 4명, 우측 파티 로스터 UI

### 스킬 시스템
- 액티브 스킬: 주사위 눈금 조건 충족 시 타겟 선택 후 발동
- 패시브 스킬: 전투 파이프라인(CombatTrigger) 통해 자동 반응
- 스킬 데이터를 CharacterPreset 내 `[SerializeReference]`로 인라인 관리 (개별 .asset 파일 불필요)
- 상태이상: Focus, MoveBuff, MoveDebuff 등

### 비주얼 / UX
- 타일 스킬 프리뷰: 스킬 타겟팅 시 대상 타일 외곽 트레일 효과 (OBB 기반, 회전된 타일 정확 추적)
- 패시브 범위 상시 표시: Warrior BattleCry 패시브의 인접 두 칸을 항상 트레일로 표시
- 이동 버튼 hover 시 목적지 타일 프리뷰
- 몬스터 공격 의도 표시 (FloatingIntentUI)
- 데미지 팝업, 호버 툴팁, 키워드 용어집

---

## 프로젝트 구조

```
Assets/
├── Scripts/
│   ├── Core/               # 게임 핵심 로직 (Character, OrbitManager, CombatManager 등)
│   │   └── Stage/
│   │       └── BattleStage/
│   │           ├── Units/  # Character, Monster, Unit 기반 클래스
│   │           └── BattleStageSystem/
│   │               ├── Combat/   # Pipeline, PassiveManager, SkillManager
│   │               └── Tile/     # TileData, TileAttribute
│   ├── Data/
│   │   ├── Character Preset/     # 캐릭터별 Preset, Active/Passive 스킬 구현
│   │   └── MonsterPresets/       # 웨이브별 몬스터 Preset
│   ├── UI/                       # CharacterActionUI, DiceUI, PartyRosterUI 등
│   └── Visuals/                  # TileSkillPreviewManager, CharacterSpriteVisual
├── Prefabs/
├── Scenes/
│   ├── MainMenu.unity
│   └── BattleScene.unity
└── Sprites/
```

---

## 시작하기

### 필요 사항
- Unity 2022.3 LTS 이상
- TextMeshPro
- Input System Package

### 실행
1. `BattleScene` 씬을 열어 Play
2. 캐릭터 카드 선택 → 주사위 굴리기 → 캐릭터 선택 → 주사위 할당 → Move / Skill

---

## 기술 스택

- **Engine**: Unity (C#)
- **UI**: TextMeshPro, Unity UI
- **직렬화**: `[SerializeReference]` 기반 폴리모픽 스킬 데이터
- **전투 구조**: CombatPipeline (Trigger/Context 패턴), ICombatReactor 인터페이스
- **아키텍처**: 싱글톤 매니저, 액션 큐, 이벤트 기반 패시브 반응
