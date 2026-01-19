# CharacterPreset 스프라이트 설정 가이드

## 문제: 캐릭터 스프라이트가 비어있음

CharacterPreset ScriptableObject에 스프라이트를 설정해야 합니다.

---

## 해결 방법 1: 기본 스프라이트 사용

코드를 수정하여 스프라이트가 없을 때 흰색 사각형을 자동 생성합니다. ✅ (완료)

---

## 해결 방법 2: Unity에서 스프라이트 설정

### 1. 간단한 테스트 스프라이트 만들기

**Unity 에디터:**
1. **Hierarchy** → 우클릭 → **2D Object → Sprites → Square**
2. Scene에 흰색 사각형 생성됨
3. 이 Square의 Sprite를 복사

**또는 외부 이미지 사용:**
1. 프로젝트에 이미지 파일 드래그 (.png)
2. **Inspector**에서:
   - Texture Type: **Sprite (2D and UI)**
   - Apply 클릭

---

### 2. CharacterPreset에 스프라이트 할당

**Assets/Data/Characters 폴더:**
1. CharacterPreset 클릭 (예: Warrior)
2. **Inspector**:
   - **Character Sprite**: 위에서 만든 스프라이트 드래그
   - **Sprite Color**: 원하는 색상 (예: 빨강, 파랑 등)

**4개 모두 설정:**
- Warrior: 빨간색
- Mage: 파란색
- Rogue: 초록색
- Cleric: 노란색

---

## 현재 상태

- ✅ 스프라이트 없으면 **흰색 사각형** 자동 생성
- ✅ Sprite Color는 정상 적용

**테스트:**
1. 캐릭터 선택
2. 시작 타일에 **색깔 있는 사각형** 생성됨

나중에 실제 캐릭터 스프라이트로 교체하면 됩니다!
