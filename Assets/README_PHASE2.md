# Phase 2 Setup Guide: 주사위 시스템

## 🎯 구현 완료

Phase 2의 모든 스크립트가 완성되었습니다! 이제 Unity에서 UI를 설정해야 합니다.

## 📦 생성된 파일

### Scripts (8개)
- [ActionType.cs](file:///d:/Dice%20Orbit/Assets/Scripts/Data/ActionType.cs) - 행동 타입 열거형
- [DiceData.cs](file:///d:/Dice%20Orbit/Assets/Scripts/Data/DiceData.cs) - 주사위 데이터
- [DiceManager.cs](file:///d:/Dice%20Orbit/Assets/Scripts/Core/DiceManager.cs) - 주사위 관리자
- [TurnManager.cs](file:///d:/Dice%20Orbit/Assets/Scripts/Core/TurnManager.cs) - 턴 관리
- [DiceElement.cs](file:///d:/Dice%20Orbit/Assets/Scripts/UI/DiceElement.cs) - 주사위 UI 요소
- [DiceUI.cs](file:///d:/Dice%20Orbit/Assets/Scripts/UI/DiceUI.cs) - 주사위 UI 컨테이너
- [ActionPanel.cs](file:///d:/Dice%20Orbit/Assets/Scripts/UI/ActionPanel.cs) - 행동 선택 패널
- [TestCharacter.cs](file:///d:/Dice%20Orbit/Assets/Scripts/Core/TestCharacter.cs) - 테스트 캐릭터

## 🎮 Unity UI 설정 방법

### 1. Canvas 생성
1. Hierarchy에서 우클릭 → **UI > Canvas**
2. Canvas 이름: `GameCanvas`
3. Canvas Scaler 설정:
   - UI Scale Mode: **Scale With Screen Size**
   - Reference Resolution: **1920 x 1080**

### 2. EventSystem 확인
- Canvas 생성 시 자동으로 생성됨
- 없으면 Hierarchy에서 우클릭 → **UI > Event System**

### 3. DicePanel 생성 (하단)

**GameCanvas 하위에 생성:**

1. **DicePanel** (Panel)
   - Rect Transform:
     - Anchor: Bottom Stretch
     - Pos Y: 0
     - Height: 150
   - Color: 반투명 검정 (0, 0, 0, 100)

2. **DiceContainer** (DicePanel의 자식)
   - Add Component: **Horizontal Layout Group**
     - Spacing: 10
     - Child Force Expand: Width, Height 체크
   - Add Component: **Content Size Fitter**
     - Horizontal Fit: Preferred Size
   - Rect Transform:
     - Anchor: Center
     - Width: 800
     - Height: 100

3. **RollButton** (DicePanel의 자식)
   - UI > Button - TextMeshPro
   - Text: "Roll Dice"
   - Rect Transform:
     - Anchor: Bottom Right
     - Pos: (-150, 75)
     - Size: (200, 60)

### 4. DicePrefab 생성

**Assets/Prefabs 폴더에 생성:**

1. Hierarchy에서 우클릭 → **UI > Image**
2. 이름: `DicePrefab`
3. 구성:
   - **Image** (Root)
     - Rect Transform: Width 80, Height 80
     - Color: 흰색
   - **ValueText** (자식) - TextMeshPro
     - Text: "6"
     - Font Size: 48
     - Alignment: Center
     - Color: 검정
4. Add Component: **DiceElement**
5. Add Component: **Canvas Group**
6. Prefab으로 저장: Hierarchy에서 DicePrefab을 Prefabs 폴더로 드래그

### 5. ActionPanel 생성 (중앙)

**GameCanvas 하위에 생성:**

1. **ActionPanel** (Panel)
   - Rect Transform:
     - Anchor: Center
     - Pos: (0, 0)
     - Size: (400, 300)
   - Color: 반투명 회색 (0.2, 0.2, 0.2, 0.9)
   - 기본 비활성: Inspector에서 체크 해제

2. **InfoText** (ActionPanel의 자식) - TextMeshPro
   - Rect Transform:
     - Anchor: Top Stretch
     - Height: 100
   - Text: "Choose Action"
   - Alignment: Center

3. **MoveButton** (ActionPanel의 자식)
   - UI > Button - TextMeshPro
   - Text: "Move"
   - Rect Transform:
     - Pos: (-80, -50)
     - Size: (150, 60)

4. **SkillButton** (ActionPanel의 자식)
   - UI > Button - TextMeshPro
   - Text: "Skill"
   - Rect Transform:
     - Pos: (80, -50)
     - Size: (150, 60)

5. **CancelButton** (ActionPanel의 자식)
   - UI > Button - TextMeshPro
   - Text: "Cancel"
   - Rect Transform:
     - Pos: (0, -120)
     - Size: (150, 60)

### 6. Manager 오브젝트 설정

**Hierarchy에 빈 GameObject 추가:**

1. **DiceManager**
   - Add Component: **DiceManager**
   - Dice Count Per Turn: 4

2. **TurnManager**
   - Add Component: **TurnManager**

3. **GameCanvas의 DicePanel**
   - Add Component: **DiceUI**
   - Inspector 설정:
     - Dice Container: DiceContainer 연결
     - Dice Element Prefab: DicePrefab 연결
     - Roll Button: RollButton 연결

4. **GameCanvas의 ActionPanel**
   - Add Component: **ActionPanel**
   - Inspector 설정:
     - Panel Object: ActionPanel 자신
     - Move Button: MoveButton 연결
     - Skill Button: SkillButton 연결
     - Cancel Button: CancelButton 연결
     - Info Text: InfoText 연결

### 7. TestCharacter 생성

1. Hierarchy에서 **3D Object > Sphere** 생성
2. 이름: `TestHero`
3. Add Component: **TestCharacter**
4. Add Component: **Box Collider** (마우스 클릭 감지용)
5. Position: 궤도 위 (레벨업 타일 위치)
6. Scale: (0.5, 0.5, 0.5)

### 8. GameManager에 참조 연결

GameManager의 Inspector에서:
- Dice Manager 연결
- Turn Manager 연결

## ✅ 테스트 방법

1. **Play 버튼** 클릭
2. **"Roll Dice"** 버튼 클릭
   - 주사위 4개 생성 확인
3. **주사위를 드래그**하여 TestHero(구체)에 드롭
4. **ActionPanel** 표시 확인
5. **"Move" 또는 "Skill"** 선택
6. Console에서 로그 확인

## 🐛 문제 해결

- **주사위가 드래그 안됨**: EventSystem이 있는지 확인
- **드롭이 안됨**: TestHero에 Collider가 있는지 확인
- **ActionPanel 안 뜸**: ActionPanel이 비활성 상태인지 확인 (정상)
- **TextMeshPro 깨짐**: Window > TextMeshPro > Import TMP Essential Resources

---

**다음 단계**: Phase 3에서 실제 캐릭터 시스템을 구현하고 TestCharacter를 대체합니다.
