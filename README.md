# Dice Orbit

Unity 기반 턴제 전략 로그라이크 게임 프로토타입

## 🎮 게임 개요

- **장르**: 턴제 전략 로그라이크
- **핵심 메카닉**: 주사위 + 원형 궤도 이동
- **플랫폼**: Unity 2021.3 이상

---

## ✅ 구현 완료 (Phase 1-3)

### Phase 1: 궤도 타일 시스템
- ✅ 원형 타일 배치 (20개)
- ✅ 중앙 몬스터 구역
- ✅ 레벨업 타일 (녹색)
- ✅ 3가지 타일 타입

### Phase 2: 주사위 시스템
- ✅ 주사위 굴리기 (1~6)
- ✅ 드래그 & 드롭 UI
- ✅ 캐릭터 선택 시스템
- ✅ 즉시 행동 실행
- ✅ 타일 단위 이동 애니메이션

### Phase 3: 캐릭터 & 파티 시스템
- ✅ 캐릭터 스탯 (HP, ATK, DEF)
- ✅ 레벨업 시스템
- ✅ 파티 관리 (최대 4명)
- ✅ HP 바 UI
- ✅ 2.5D 스프라이트 지원

---

## 🎯 게임 플레이

1. 주사위를 굴려서 행동 결정
2. 캐릭터를 클릭하여 선택
3. 주사위를 드래그하여 할당
4. Move/Skill 선택
5. 타일 위를 이동하며 레벨업

---

## 📁 프로젝트 구조

```
Assets/
├── Scripts/
│   ├── Core/         # 게임 핵심 로직
│   ├── Data/         # 데이터 구조
│   ├── UI/           # UI 컴포넌트
│   └── Visuals/      # 비주얼 효과
├── Prefabs/          # 프리팹
├── Materials/        # 머티리얼
└── Sprites/          # 2D 스프라이트
```

---

## 🚀 시작하기

### 필요 사항
- Unity 2021.3 LTS 이상
- TextMeshPro
- Input System Package

### Unity 설정
1. `Assets/README_PHASE1.md` - 궤도 시스템
2. `Assets/SETUP_PHASE2.md` - 주사위 & UI
3. `Assets/README_PHASE3.md` - 캐릭터 시스템
4. `Assets/CharacterUI_Setup.md` - HP 바 UI

---

## 📚 문서

- [전체 작업 계획](task.md)
- [Phase 1 가이드](Assets/README_PHASE1.md)
- [Phase 2 가이드](Assets/README_PHASE2.md)
- [Phase 3 가이드](Assets/README_PHASE3.md)
- [스프라이트 가이드](Assets/SPRITE_CHARACTER_GUIDE.md)

---

## 🔜 다음 단계 (Phase 4+)

- [ ] 몬스터 시스템
- [ ] 전투 시스템
- [ ] 스킬 확장
- [ ] Wave 시스템
- [ ] 게임 오버/승리 조건

---

## 🛠️ 기술 스택

- **Engine**: Unity
- **Language**: C#
- **UI**: TextMeshPro, Unity UI
- **Input**: New Input System
- **Architecture**: 싱글톤 패턴, 이벤트 시스템

---

## 📝 라이센스

MIT License

---

**현재 버전**: Phase 3 완료 (캐릭터 & 파티 시스템)
