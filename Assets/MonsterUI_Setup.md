# MonsterUI & DamagePopup 설정 가이드

## 🎨 몬스터 UI 만들기

### 1. World Space Canvas 생성

**Monster GameObject 하위에:**

1. **Monster (예: TestMonster)** 우클릭
2. **UI → Canvas** 생성
3. 이름: "MonsterCanvas"

**Canvas 설정:**
- Render Mode: **World Space**
- Rect Transform:
  - Pos X: 0, Y: 2.0, Z: 0 (몬스터 위)
  - Width: 300, Height: 100
  - Scale: (0.1, 0.1, 0.1)

---

### 2. HP 바 생성

**MonsterCanvas 하위에:**

1. **UI → Panel** → 이름: "HPBar"

2. **HPBar 자식 → UI → Slider** → "HPSlider"
   - Anchor: Stretch All
   - Min Value: 0
   - Max Value: 50
   - Value: 50
   - Fill: 빨간색

3. **HPBar 자식 → TextMeshPro** → "HPText"
   - Text: "50/50"
   - Alignment: Center

---

### 3. 공격 의도 UI

**MonsterCanvas 하위에:**

1. **UI → Image** → "IntentIcon"
   - Pos Y: 30
   - Width: 40, Height: 40
   - Color: 빨간색

2. **IntentIcon 자식 → TextMeshPro** → "IntentText"
   - Text: "8"
   - Font Size: 24
   - Alignment: Center

---

### 4. MonsterUI 컴포넌트 추가

**MonsterCanvas에:**

1. **Add Component**: **MonsterUI**
2. **Inspector 설정**:
   - Monster: 부모 Monster 연결
   - World Canvas: MonsterCanvas
   - HP Slider: HPSlider
   - HP Text: HPText
   - Intent Icon: IntentIcon
   - Intent Text: IntentText
   - Auto Find Monster: ✓

---

## 💥 DamagePopup 만들기

### 1. Prefab 생성

**Hierarchy에서:**

1. **Create → UI → Canvas**
2. Canvas 설정:
   - Render Mode: **World Space**
   - Width: 100, Height: 50
   - Scale: (0.05, 0.05, 0.05)

3. **Canvas 자식 → TextMeshPro** → "DamageText"
   - Text: "99"
   - Font Size: 48
   - Alignment: Center
   - Color: White
   - Outline: 검정 (두께 0.2)

4. **Canvas에 Add Component**:
   - **DamagePopup**
   - **Canvas Group**

5. **Inspector 설정**:
   - Damage Text: DamageText 연결
   - Move Speed: 2
   - Lifetime: 1.5

---

### 2. Prefab 저장

1. **Create Folder**: `Assets/Resources/Prefabs`
2. Canvas를 **DamagePopup** 이름으로 Prefabs 폴더에 드래그
3. Hierarchy에서 Canvas 삭제

---

## 🎯 사용 방법

### MonsterUI
- 자동으로 HP와 의도 업데이트
- Monster의 의도가 바뀌면 아이콘 색상도 자동 변경

### DamagePopup
코드에서 호출:
```csharp
DamagePopup.Create(damage, monsterPosition, isCritical);
```

---

## 🎨 의도 아이콘 색상

- **빨간색**: 공격
- **파란색**: 방어
- **노란색**: 버프
- **마젠타**: 특수

---

**이제 몬스터 위에 HP 바와 공격 의도가 표시됩니다!** ⚔️
