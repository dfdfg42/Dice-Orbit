# CharacterUI 설정 가이드

## 🎨 캐릭터 위 HP 바 만들기

### 1. World Space Canvas 생성

**Character GameObject 하위에 생성:**

1. **Character (예: Hero1)** 우클릭
2. **UI → Canvas** 생성
3. 이름: "CharacterCanvas"

**Canvas 설정:**
- Render Mode: **World Space**
- Rect Transform:
  - Pos X: 0, Y: 1.2, Z: 0 (캐릭터 위)
  - Width: 200, Height: 50
  - Scale: (0.01, 0.01, 0.01) - 작게

---

### 2. HP 바 생성

**CharacterCanvas 하위에:**

1. **UI → Panel** → 이름: "HPBar"
   - Anchor: Stretch All
   - Color: 검정 반투명 (배경)

2. **HPBar의 자식 → UI → Slider** → 이름: "HPSlider"
   - Anchor: Stretch All
   - Min Value: 0
   - Max Value: 30
   - Value: 30
   
   **HPSlider 하위 조정:**
   - Background: 회색
   - Fill: 빨간색 또는 초록색
   - Handle Slide Area: 비활성화 (필요없음)

3. **HPBar의 자식 → TextMeshPro** → 이름: "HPText"
   - Text: "30/30"
   - Font Size: 24
   - Alignment: Center
   - Color: 흰색

---

### 3. 이름 & 레벨 (선택사항)

**CharacterCanvas 하위에:**

1. **TextMeshPro** → "NameText"
   - Text: "Hero"
   - Pos Y: 20
   - Font Size: 28
   - Alignment: Center

2. **TextMeshPro** → "LevelText"
   - Text: "Lv.1"
   - Pos Y: 35
   - Font Size: 20
   - Alignment: Center

---

### 4. CharacterUI 컴포넌트 추가

**CharacterCanvas에:**

1. **Add Component**: **CharacterUI**
2. **Inspector 설정**:
   - Character: 부모 Character 연결
   - World Canvas: CharacterCanvas 자신
   - HP Slider: HPSlider 연결
   - Name Text: NameText 연결 (있으면)
   - HP Text: HPText 연결 (있으면)
   - Level Text: LevelText 연결 (있으면)
   - UI Offset: (0, 1.2, 0)
   - Auto Find Character: ✓

---

## 🎯 간단 버전 (최소 설정)

**필수만:**
1. Canvas (World Space)
2. Slider (HP 바)
3. CharacterUI 컴포넌트

나머지(이름, 레벨, HP 텍스트)는 선택사항입니다!

---

## ✅ 테스트

1. **Play**
2. 캐릭터 위에 **HP 바** 표시 확인
3. 주사위로 **레벨업 타일**로 이동
4. **HP 바가 꽉 참** (레벨업 풀 회복)
5. **레벨 텍스트 증가** (Lv.1 → Lv.2)

---

## 🎨 UI 예쁘게 만들기

### HP 바 색상
- 초록색: HP 많음
- 노란색: HP 중간
- 빨간색: HP 적음

### 배경
- 어두운 패널로 가독성 향상
- 약간 둥근 모서리

### 폰트
- Google Fonts에서 가져오기
- Bold 폰트 추천

---

**이제 캐릭터마다 HP 바가 따라다닙니다!** 🎉
