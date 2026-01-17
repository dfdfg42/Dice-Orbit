# Phase 3 Setup: 캐릭터 & 파티 시스템

## ✅ 생성된 파일

### 데이터 (Data/)
- **CharacterStats.cs** - HP, ATK, DEF, 레벨 관리
- **SkillData.cs** - 스킬 데이터 및 데미지 계산

### Core 시스템 (Core/)
- **Character.cs** - 실제 캐릭터 클래스 (TestCharacter 기능 통합)
- **PartyManager.cs** - 파티 관리 (최대 4명)

---

## 🎮 Unity 설정 방법

### 1. PartyManager 설정

**Hierarchy에 빈 GameObject 생성:**
1. Create Empty → 이름: "PartyManager"
2. Add Component: **PartyManager**

**자동 기능:**
- Scene의 모든 Character를 자동으로 파티에 추가
- 최대 4명까지 관리

---

### 2. Character 생성

#### 방법 A: TestHero를 Character로 교체

1. **TestHero GameObject 선택**
2. **Remove Component**: TestCharacter
3. **Add Component**: **Character**
4. **Inspector 설정**:
   - **Stats** 섹션:
     - Character Name: "Hero"
     - Level: 1
     - Max HP: 30
     - Attack: 5
     - Defense: 0
   - **Visual**:
     - Character Sprite: 캐릭터 스프라이트
     - Sprite Color: White
   - **Movement**:
     - Start Tile Index: 0

#### 방법 B: 새로운 Character 생성

1. **Create Empty** → 이름: "Hero1"
2. **Add Component**:
   - Sprite Renderer
   - Character
   - Sphere Collider (클릭용)
3. **Position**: (0, 0.5, 0)
4. **Scale**: (0.5, 0.5, 1)
5. **스탯 설정** (위와 동일)

---

## 🎯 주요 기능

### Character 클래스

#### 스탯 시스템
```csharp
stats.Heal(10);           // HP 회복
stats.TakeDamage(5);      // 데미지 받기
stats.LevelUp();          // 레벨업 (HP+5, ATK+2, DEF+1)
bool alive = stats.IsAlive;  // 생존 확인
```

#### 이동 & 행동
- `Move(int steps)` - 타일 단위 이동 (Phase 2와 동일)
- `UseSkill(int diceValue)` - 스킬 사용
- `OnSelected()` - 클릭 시 호출

#### 자동 레벨업
- 레벨업 타일 도착 시 자동으로 `stats.LevelUp()` 호출
- HP 풀 회복
- 스탯 상승

---

### PartyManager

#### 자동 관리
```csharp
// Scene의 모든 Character 자동 감지
var party = PartyManager.Instance.Party;

// 생존 캐릭터만
var alive = PartyManager.Instance.GetAliveCharacters();

// 전멸 확인
bool wiped = PartyManager.Instance.IsPartyWiped();
```

#### 수동 추가/제거
```csharp
PartyManager.Instance.AddCharacter(character);
PartyManager.Instance.RemoveCharacter(character);
```

---

## ✅ 테스트 방법

### 1. 기본 테스트
1. **Play**
2. Console에서 "PartyManager: Detected X characters" 확인
3. 캐릭터 클릭 → 주사위 사용
4. 이동 확인

### 2. 레벨업 테스트
1. 주사위로 이동
2. **레벨업 타일(녹색)** 도착
3. Console에서 레벨업 로그 확인:
   ```
   Hero leveled up to 2! HP: 35, ATK: 7, DEF: 1
   ```

### 3. 다중 캐릭터 테스트
1. Hero1, Hero2 등 여러 캐릭터 생성
2. 각각 다른 타일에 배치 (Start Tile Index 다르게)
3. 파티 사이즈 확인

---

## 📋 체크리스트

- [ ] PartyManager GameObject 생성
- [ ] Character 컴포넌트로 교체 또는 추가
- [ ] 캐릭터 스탯 설정
- [ ] 스프라이트 할당
- [ ] Collider 있음 (클릭용)
- [ ] Play → 파티 감지 확인
- [ ] 레벨업 타일에서 레벨업 테스트

---

## 🔜 다음 단계

**Phase 3 남은 작업:**
- [ ] CharacterUI.cs - HP 바, 스탯 표시
- [ ] PartyUI.cs - 전체 파티 UI

**Phase 4 예정:**
- 몬스터 시스템
- 전투 시스템
- 데미지 계산

---

**이제 TestCharacter를 Character로 교체하고 테스트해보세요!** 🎉
