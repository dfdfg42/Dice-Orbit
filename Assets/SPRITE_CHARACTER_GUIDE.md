# 2D 스프라이트 캐릭터 사용하기

## 🎨 Unity에서 2D 스프라이트로 변경하기

### 방법 1: 기존 TestHero를 스프라이트로 변경

1. **Sphere 제거**
   - TestHero GameObject 선택
   - Mesh Filter와 Mesh Renderer 제거 (Remove Component)
   - Sphere Collider는 그대로 유지! (클릭 감지용)

2. **SpriteRenderer 추가**
   - Add Component → **Sprite Renderer**
   - Sprite: 캐릭터 스프라이트 선택
   - Color: White

3. **CharacterSpriteVisual 추가 (선택사항)**
   - Add Component → **CharacterSpriteVisual**
   - Character Sprite: 캐릭터 스프라이트 할당
   - Billboard To Camera: ✓ (항상 카메라를 향함)

4. **위치 조정**
   - Position: 타일 위
   - Scale: (1.5, 1.5, 1) - 적당한 크기로

---

### 방법 2: 새로운 스프라이트 캐릭터 만들기

1. **GameObject 생성**
   - Hierarchy 우클릭 → Create Empty
   - 이름: "SpriteHero"

2. **컴포넌트 추가**
   - Add Component → **Sprite Renderer**
     - Sprite: 캐릭터 스프라이트
   - Add Component → **Sphere Collider** (클릭용)
     - Radius: 0.5
   - Add Component → **TestCharacter**
   - Add Component → **CharacterSpriteVisual** (optional)

3. **설정**
   - Position: (0, 0.5, 0) - 타일 위
   - Scale: (1.5, 1.5, 1)
   - Sorting Layer: Default
   - Order in Layer: 10 (타일 위에 표시)

---

## 🎯 CharacterSpriteVisual 기능

### Billboard 효과
- **항상 카메라를 향함** - Top-down 뷰에서도 스프라이트가 똑바로 보임
- `billboardToCamera` 체크하면 자동

### 하이라이트
- 마우스 호버 시 색상 변경
- `normalColor`, `highlightColor` 설정 가능

### 코드 사용 예시
```csharp
var visual = GetComponent<CharacterSpriteVisual>();

// 스프라이트 변경
visual.SetSprite(newSprite);

// 하이라이트
visual.SetHighlight(true);

// 색상 변경
visual.SetColor(Color.red);
```

---

## ✅ 체크리스트

- [ ] Sprite Renderer 추가
- [ ] 캐릭터 스프라이트 할당
- [ ] Collider 있음 (클릭용)
- [ ] TestCharacter 컴포넌트 있음
- [ ] 위치가 타일 위에 있음
- [ ] (선택) CharacterSpriteVisual 추가

---

## 🎨 스프라이트 준비 팁

1. **Import Settings** (Sprite Inspector)
   - Texture Type: **Sprite (2D and UI)**
   - Sprite Mode: Single
   - Pixels Per Unit: 100
   - Filter Mode: Point (픽셀아트) 또는 Bilinear

2. **크기**
   - 권장: 128x128, 256x256, 512x512
   - 투명 배경 (PNG)

3. **Sorting Layer**
   - 캐릭터가 타일 위에 보이도록
   - Order in Layer를 높게 설정

---

**이제 TestHero가 2D 스프라이트로 표시됩니다!** 🎉

모든 기능(클릭, 이동, 하이라이트)이 정상 작동합니다.
