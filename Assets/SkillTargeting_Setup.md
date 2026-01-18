# 스킬 타겟팅 시스템 설정

## 🎯 SkillTargetSelector 설정

### 1. GameObject 생성

**Hierarchy에서:**
1. **Create Empty** → 이름: "SkillTargetSelector"
2. **Add Component**: **SkillTargetSelector**
3. **Add Component**: **Line Renderer** (자동 추가됨)

---

### 2. Line Renderer 설정

**SkillTargetSelector Inspector:**

**Visual:**
- Valid Target Color: 초록색 (0, 255, 0)
- Invalid Target Color: 빨간색 (255, 0, 0)
- Line Width: 0.1

**Line Renderer (자동 설정됨):**
- Width: 0.1
- Material: Sprites/Default
- Color: 초록색

---

## 🎮 사용 방법

### 스킬 사용 플로우

1. **주사위 굴리기**
2. **캐릭터 선택**
3. **주사위 드래그 → Skill**
4. **타겟 선택 모드 시작**:
   - 캐릭터에서 마우스까지 점선 표시
   - 유효한 타겟: **초록색**
   - 무효한 타겟: **빨간색**
5. **타겟 클릭**:
   - 몬스터 클릭 → 공격!
   - 우클릭 → 취소

---

## 🎯 타겟 타입별 동작

### SingleEnemy (단일 적)
- 몬스터 클릭 가능
- 초록색 라인 표시

### AllEnemies (모든 적)
- 아무 몬스터나 클릭
- 모든 몬스터에게 데미지

### Self (자신)
- 자기 자신 클릭 (힐링 등)

### Ally (아군)
- 다른 캐릭터 클릭 (버프, 힐링 등)

---

## ✅ 테스트

1. **Play**
2. **Roll Dice**
3. 캐릭터 선택 → **Skill**
4. **마우스를 몬스터 위로**:
   - 점선이 **초록색**으로 변함
5. **몬스터 클릭**:
   - 데미지 적용!
   - Console: "Hero attacks Slime for XX damage!"

**취소:**
- 우클릭으로 타겟 선택 취소

---

**이제 스킬이 제대로 작동합니다!** ⚔️✨
