# CharacterUI 프리팹 설정

## 수정 완료 ✅

CharacterSelectionUI가 이제 **CharacterUI 프리팹**을 사용합니다!

---

## Unity 설정

### 1. CharacterUI 프리팹 찾기/생성

**기존 프리팹이 있다면:**
- `Assets/Prefabs/UI/CharacterUI.prefab`

**없다면 만들기:**
1. **Hierarchy** → Create Empty → "CharacterUI"
2. **Canvas** 추가 (Render Mode: World Space)
3. HP 바, 이름 텍스트 등 구성
4. **CharacterUI.cs** 스크립트 추가
5. Prefabs 폴더로 드래그 → 프리팹 생성

---

### 2. CharacterSelectionUI 설정

**Hierarchy에서 CharacterSelectionCanvas 선택**

**Inspector:**
- **Prefabs** 섹션:
  - **Character UI Prefab**: CharacterUI 프리팹 드래그

---

## 장점

✅ **간단함** - 복잡한 동적 생성 코드 제거
✅ **관리 편함** - Unity 에디터에서 UI 수정
✅ **디버깅** - Hierarchy에서 바로 확인

---

## Prefab이 없으면?

- 경고 로그만 출력
- UI 없이 캐릭터만 생성됨
- 게임은 정상 작동

나중에 프리팹 만들면 자동으로 작동합니다!
