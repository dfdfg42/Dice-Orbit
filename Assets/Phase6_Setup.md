# Phase 6 Setup Guide: Character Selection

## 📋 생성된 파일

1. `GameState.cs` - 게임 상태 enum
2. `GameFlowManager.cs` - 게임 플로우 관리
3. `CharacterPreset.cs` - 캐릭터 프리셋 (ScriptableObject)
4. `CharacterSelectionUI.cs` - 선택 UI 관리
5. `CharacterCard.cs` - 개별 캐릭터 카드

---

## 🎮 Unity 설정

### 1. GameFlowManager 설정

**Hierarchy:**
1. Create Empty → "GameFlowManager"
2. Add Component: **GameFlowManager**

**Inspector 설정:**
- Character Selection UI: (나중에 연결)
- Combat UI: (기존 UI)
- Shop UI: (나중에 추가)

---

### 2. CharacterPreset 생성 (4개)

**Assets 폴더:**
1. `Assets/Data/Characters` 폴더 생성
2. 우클릭 → **Create → DiceOrbit → Character Preset**
3. 4개 생성:
   - "Warrior"
   - "Mage"
   - "Rogue"
   - "Cleric"

**각 Preset 설정 예시 (Warrior):**
```
Character Name: "전사"
Max HP: 40
Attack: 6
Defense: 2
Description: "높은 체력과 방어력을 가진 전사"
```

**Mage:**
```
Character Name: "마법사"
Max HP: 25
Attack: 8
Defense: 0
Description: "강력한 마법 공격"
```

**Rogue:**
```
Character Name: "도적"
Max HP: 30
Attack: 7
Defense: 1
Description: "빠르고 정확한 공격"
```

**Cleric:**
```
Character Name: "성직자"
Max HP: 35
Attack: 5
Defense: 1
Description: "회복과 버프 능력"
```

---

### 3. Character Selection UI 캔버스

**Hierarchy:**
1. **UI → Canvas** (이미 있으면 사용)
2. 이름: "CharacterSelectionCanvas"

**Canvas 하위:**
```
CharacterSelectionCanvas
├── Panel (Background)
├── Title (Text)
└── CardContainer (Horizontal Layout Group)
```

**CardContainer 설정:**
- Add Component: **Horizontal Layout Group**
- Spacing: 20
- Child Alignment: Middle Center

---

### 4. Character Card Prefab

**Card Prefab 구조:**
```
CharacterCard (Prefab)
├── Background (Image)
├── Portrait (Image)
├── NameText (TextMeshPro)
├── DescriptionText (TextMeshPro)
├── StatsText (TextMeshPro)
└── SelectButton (Button)
    └── ButtonText (TextMeshPro)
```

**Prefab 저장:**
- `Assets/Prefabs/UI/CharacterCard.prefab`

---

### 5. CharacterSelectionUI 설정

**CharacterSelectionCanvas에 추가:**
1. Add Component: **CharacterSelectionUI**

**Inspector 설정:**
- All Characters: (4개 Preset 드래그)
- Card Container: CardContainer GameObject
- Character Card Prefab: CharacterCard prefab
- Number Of Choices: 4

---

## ✅ 테스트

1. **Play**
2. 자동으로 **캐릭터 선택 화면** 표시
3. 4개 랜덤 캐릭터 카드
4. 선택 → 전투 시작

---

## 🎯 다음 단계

**Phase 7에서:**
- 몬스터 공격 시각화
- 실제 캐릭터 스킬 구현
