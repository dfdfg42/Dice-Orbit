# Phase 4 Setup: 전투 시스템

## ✅ 생성된 파일

### 데이터 (Data/)
- **MonsterStats.cs** - 몬스터 HP, ATK, DEF 관리
- **AttackIntent.cs** - 공격 의도 타입 및 데이터
- **SkillData.cs** [수정] - 타겟팅 타입, 범위 공격, 특수 효과

### Core 시스템 (Core/)
- **Monster.cs** - 몬스터 클래스 (공격 의도 AI, 파티 공격)
- **CombatManager.cs** - 전투 관리 (싱글톤)
- **Character.cs** [수정] - UseSkill 확장 (타겟팅 및 범위 공격)

---

## 🎮 Unity 설정 방법

### 1. CombatManager 설정

**Hierarchy에 빈 GameObject 생성:**
1. Create Empty → 이름: "CombatManager"
2. Add Component: **CombatManager**

**자동 기능:**
- Scene의 모든 Monster를 자동으로 감지
- 전투 시작/종료 자동 관리

---

### 2. Monster 생성

**중앙 구역에 Monster GameObject 생성:**

1. **Create Empty** → 이름: "TestMonster"
2. **Add Component**:
   - Sprite Renderer
   - Monster
   - Box Collider 2D (선택)
3. **Position**: 중앙 구역 (0, 1, 0)
4. **Scale**: (2, 2, 1)

**Monster Inspector 설정:**
- **Stats**:
  - Monster Name: "Slime"
  - Max HP: 50
  - Attack: 8
  - Defense: 2
- **Visual**:
  - Monster Sprite: 몬스터 스프라이트

---

## ⚔️ 스킬 설정

**Character의 Skills:**

1. **기본 공격** (예시):
   - Skill Name: "Attack"
   - Target Type: **Single Enemy**
   - Damage Multiplier: 1
   - Min Dice Value: 1

2. **범위 공격** (예시):
   - Skill Name: "Fireball"
   - Target Type: **All Enemies**
   - Damage Multiplier: 1
   - Min Dice Value: 4

3. **방어 무시 공격** (예시):
   - Skill Name: "Pierce"
   - Target Type: **Single Enemy**
   - Damage Multiplier: 2
   - Ignore Defense: ✓

---

## 🎯 게임 플로우

### 전투 시작
1. Play → CombatManager가 Monster 감지
2. Console: "Combat started! 1 monster(s)"

### 플레이어 턴
1. **"Roll Dice"**
2. **캐릭터 클릭**
3. **주사위 드래그**
4. **"Skill" 클릭** → 몬스터 공격!
5. Console: 데미지 로그 확인

### 몬스터 턴 (수동 실행)
**Console에서:**
```
CombatManager.Instance.ExecuteMonsterTurn();
```

또는 TurnManager의 "Turn End" 버튼 (나중에 추가 예정)

---

## ✅ 테스트 방법

### 1. 기본 전투 테스트
1. **Play**
2. Console: "Combat started!"
3. 주사위로 캐릭터 선택
4. **Skill 사용**
5. Console 확인:
   ```
   Hero uses Attack with dice 5!
   Slime took 11 damage! (HP: 39/50)
   ```

### 2. 범위 공격 테스트
- 몬스터 여러 마리 생성
- 범위 스킬 (All Enemies) 사용
- 모든 몬스터에게 데미지 확인

### 3. 승리/패배 테스트
- **승리**: 몬스터 HP를 0으로 만들기
  ```
  Slime defeated!
  Victory! All monsters defeated!
  ```
- **패배**: 캐릭터 HP를 0으로 (테스트용)

---

## 🔜 다음 단계 (전투 UI)

**남은 작업:**
- [ ] MonsterUI (HP 바)
- [ ] IntentIcon (공격 의도 표시)
- [ ] DamagePopup (데미지 숫자)
- [ ] 턴 종료 버튼

---

**이제 전투 시스템 핵심이 완성되었습니다!** ⚔️

테스트 후 전투 UI를 추가하면 Phase 4 완료!
