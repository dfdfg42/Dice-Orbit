# Phase 1 완료: 궤도 타일 시스템

## 🎯 완료된 작업

### 📁 폴더 구조
```
Assets/
├── Scripts/
│   ├── Core/           ✅ OrbitManager, CenterZone, GameManager
│   ├── Visuals/        ✅ TileVisual, OrbitVisualizer
│   ├── Data/           ✅ TileData
│   └── UI/             (Phase 2+)
├── Prefabs/           ✅ 생성 완료
├── Materials/         ✅ NormalTile, LevelUpTile, SpecialTile
└── Scenes/
```

### 🔧 구현된 스크립트

#### 1. **TileData.cs** - 타일 데이터 관리
- 타일 인덱스, 타입 (Normal/LevelUp/Special)
- 연결 구조 (nextTile, previousTile)
- 하이라이트 기능

#### 2. **TileVisual.cs** - 타일 시각화
- 프로그래머틱 메쉬 생성 (육면체)
- 타입별 머티리얼 적용
- 하이라이트 효과

#### 3. **OrbitManager.cs** - 궤도 시스템 핵심
- 원형 타일 배치 알고리즘
- 타일 개수/반지름 설정 가능
- 순환 링크드 리스트 구조
- 런타임 생성 및 초기화

#### 4. **CenterZone.cs** - 중앙 몬스터 구역
- 원형 영역 시각화
- 위치 검증 기능
- Gizmo 표시

#### 5. **OrbitVisualizer.cs** - 궤도 가이드
- 원형 가이드 라인
- LineRenderer 활용

#### 6. **GameManager.cs** - 게임 전체 관리
- 싱글톤 패턴
- 카메라 자동 설정 (Top-down view)
- 매니저들 초기화

## 🎨 머티리얼
- **NormalTile**: 회색 (일반 타일)
- **LevelUpTile**: 녹색 발광 (레벨업 타일)
- **SpecialTile**: 보라색 (특수 타일)

## 🎮 Unity에서 사용 방법

### Scene 설정 (SampleScene)

1. **빈 GameObject 생성** → 이름: `GameManager`
   - `GameManager.cs` 컴포넌트 추가

2. **빈 GameObject 생성** → 이름: `OrbitSystem`
   - `OrbitManager.cs` 컴포넌트 추가
   - Inspector 설정:
     - Tile Count: 12 (원하는 타일 개수)
     - Orbit Radius: 8
     - Clockwise: ✓
     - Materials 할당 (NormalTile, LevelUpTile, SpecialTile)

3. **빈 GameObject 생성** → 이름: `CenterZone`
   - `CenterZone.cs` 컴포넌트 추가
   - Inspector 설정:
     - Radius: 3

4. **OrbitSystem에 Child GameObject 추가** → 이름: `Visualizer`
   - `OrbitVisualizer.cs` 컴포넌트 추가
   - Inspector 설정:
     - Orbit Radius: 8 (OrbitManager와 동일)

5. **Main Camera 설정**
   - GameManager가 자동으로 설정하지만, 수동으로도 가능:
   - Position: (0, 20, -10)
   - Rotation: (60, 0, 0)

### 실행
- Play 버튼 클릭
- Scene View에서 원형 궤도와 타일들 확인
- 녹색 타일이 레벨업 타일 (시작점)

## 📊 테스트 체크리스트

- [ ] Unity에서 Play 시 타일들이 원형으로 배치되는가?
- [ ] 레벨업 타일(녹색)이 표시되는가?
- [ ] Scene View에서 Gizmo로 중앙 구역이 보이는가?
- [ ] Inspector에서 타일 개수/반지름 변경 시 실시간으로 업데이트되는가?
- [ ] Console에 "Orbit generated with X tiles" 메시지가 출력되는가?

## 🚀 Next Steps (Phase 2 Preview)

다음 Phase에서는 **주사위 시스템**을 구현합니다:
- 주사위 모델 및 굴리기
- 드래그 & 드롭 UI
- 주사위 값으로 캐릭터 이동 명령

---

**참고**: Unity에서 스크립트가 컴파일 오류 없이 로드되는지 확인하세요. 모든 네임스페이스와 의존성이 올바르게 설정되어 있습니다.
