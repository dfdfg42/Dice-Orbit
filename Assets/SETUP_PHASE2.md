# Phase 2 완료 체크리스트 ✅

## 🎮 Unity Scene 설정 (필수!)

### 1. Manager 오브젝트들

#### GameManager (기존에 있음)
- **Add Component**: `CharacterSelector` ← **새로 추가!**
- Inspector 설정:
  - ❌ ~~Dice Manager~~: 자동으로 찾음 (연결 불필요)
  - ❌ ~~Turn Manager~~: 자동으로 찾음 (연결 불필요)

#### DiceManager (빈 GameObject)
- **Add Component**: `DiceManager`
- Dice Count Per Turn: `4`

#### TurnManager (빈 GameObject)
- **Add Component**: `TurnManager`
- ❌ Dice Manager: 자동으로 찾음 (연결 불필요)

---

### 2. UI Canvas

#### DicePanel의 DiceUI 컴포넌트
**DicePanel에 Add Component: `DiceUI`**
- ✅ Dice Container: `DiceContainer` 연결
- ✅ Dice Element Prefab: `DicePrefab` 연결
- ✅ Roll Button: `RollButton` 연결

#### ActionPanel 컴포넌트
**ActionPanel에 Add Component: `ActionPanel`**
- ✅ Panel Object: `ActionPanel` 자기 자신
- ✅ Move Button: `MoveButton` 연결
- ✅ Skill Button: `SkillButton` 연결
- ✅ Cancel Button: `CancelButton` 연결
- ✅ Info Text: `InfoText` 연결

---

### 3. TestCharacter (Sphere)

**3D Object > Sphere 생성**
- 이름: `TestHero`
- Position: 레벨업 타일 위 (y축 +0.5 정도)
- Scale: `(0.5, 0.5, 0.5)`
- **Add Component**: `TestCharacter`
- ✅ **Collider**: Sphere Collider (기본 있음)
- Material Color: 파란색 등 (구분용)

---

## 🎯 테스트 플로우

1. **Play** 버튼
2. **"Roll Dice"** 버튼 → 주사위 4개 생성
3. **TestHero (구체) 클릭** → ActionPanel 나타남 ("Drag a dice here!")
4. **주사위를 드래그** → ActionPanel에 드롭
5. **Move 또는 Skill** 선택
6. Console에서 이동 로그 확인

---

## ⚠️ 문제 해결

### 구체 클릭이 안 됨
- ✅ GameManager에 **CharacterSelector** 추가했는지 확인
- ✅ TestHero에 **Collider**가 있는지 확인
- ✅ TestHero의 **Layer**가 Ignore Raycast가 아닌지 확인

### 주사위 드래그가 안 됨
- ✅ **EventSystem**이 있는지 확인
- ✅ DicePrefab에 **Canvas Group** 있는지 확인

### ActionPanel이 안 뜸
- ✅ Console에서 "selected! Waiting for dice..." 로그 확인
- ✅ ActionPanel이 기본 **비활성** 상태여야 함

---

## 📋 빠른 설정 요약

**필수 컴포넌트 추가:**
1. GameManager → **CharacterSelector** ← 중요!
2. DicePanel → **DiceUI** + 참조 3개
3. ActionPanel → **ActionPanel** + 참조 5개
4. TestHero (Sphere) → **TestCharacter** + Collider

**모든 참조 연결 후 Play 테스트!**
