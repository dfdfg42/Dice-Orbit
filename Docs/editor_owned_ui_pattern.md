# 에디터 소유 UI 패턴 가이드

> 2026-07-05 정립, 이후 모든 신규 UI에 적용. "코드가 만들되, 스타일은 씬이 소유한다."
> 배경: UI가 순수 코드 생성이면 인스펙터에서 만질 수 없고, 순수 씬 제작이면 흐름 배선이 수작업이 됨 — 이 패턴은 둘의 중간.

## 패턴 3요소

```csharp
public class SomeUI : MonoBehaviour
{
    // ① 슬롯: 코드가 "내용"을 채울 자리 — 씬 오브젝트 참조
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private RectTransform listRoot;

    // ② 에디터 스캐폴드: 우클릭 1회로 기본 계층 생성 → 씬에 영구 저장 → 자유 스타일링
    [ContextMenu("기본 레이아웃 생성")]
    private void BuildDefaultLayout()
    {
        // 계층 생성 + 슬롯 자동 배선 + (에디터 모드면) SetDirty + MarkSceneDirty
    }

    // ③ 런타임 폴백: 슬롯이 비어 있으면 Show() 시점에 같은 메서드로 생성
    public void Show()
    {
        if (rootCanvas == null) BuildDefaultLayout();   // 셋업 전에도 게임이 멈추지 않음
        ...
    }
}
```

**규약:**
- 생성 루트는 `_XxxCanvas` 같은 언더스코어 이름 — "재생성하려면 이걸 지우세요"의 표식
- 이미 있으면 경고만 하고 반환 (중복 생성 방지)
- 버튼 리스너는 씬 배선이 아니라 **코드가 참조로 연결** (`WireButtons`, 1회 가드) — OnClick을 씬에서 만질 필요 없음
- 내용 행(보상 행/노드/칩처럼 런타임에 개수가 변하는 것)은 슬롯 컨테이너 안에 코드가 채움
- 씬 구조가 바뀌는 코드 수정 후에는 `_XxxCanvas` 삭제 → 재생성 필요 (레이아웃은 씬에 박제되므로)

## 프리팹 슬롯 (내용물 모양 커스텀)

런타임 생성 내용물의 모양을 바꾸고 싶을 때 — 프리팹을 꽂으면 그걸로 찍어냄:

| UI | 슬롯 | 프리팹 요구사항 |
|---|---|---|
| RewardUI | 씬 템플릿 5종 (`RewardCanvas/Templates`) | 뷰 컴포넌트(RewardLootChip 등)가 붙은 비활성 오브젝트 — 런타임에 복제 (2026-10-03) |
| ShopUI | choiceButtonPrefab | Button 루트 + 자식 TMP |
| RunHudUI | chipPrefab | Image 루트 + 자식 `Icon`(Image)/`Label`(TMP) 이름 탐색 |
| BattleInfoPanelUI | cardPrefab (GlossaryCardUI) | GlossaryCardUI 컴포넌트 |

## SubclassPicker ([SerializeReference] 인라인 선택)

`[SerializeReference]` 필드는 기본 인스펙터에서 타입을 고를 수 없다 → 드로어 제공:

```csharp
[SerializeReference, SubclassPicker] public RuntimeArtifact effect;   // 예: ArtifactData의 유물 효과
```
- `Core/SubclassPickerAttribute.cs` (런타임) + `Editor/SubclassPickerDrawer.cs`
- 드롭다운에 파생 타입 자동 나열 (TypeCache) → 선택 시 인스턴스 생성, 필드 인라인 편집
- 새 효과 클래스를 만들면 컴파일만 해도 드롭다운에 나타남

## 적용 현황

| UI | 파일 | 비고 |
|---|---|---|
| 전투 정보 패널 | `InfoPanel/BattleInfoPanelUI.cs` | 섹션 앵커 고정형 |
| 타일 패널 | `InfoPanel/TileInfoPanelUI.cs` | 정보 패널 왼쪽 경계 도킹 |
| 노드맵 | `NodeMapUI.cs` | 세로 스크롤 (ScrollRect 뼈대는 씬, 노드는 런타임) |
| 보상 | `Reward/RewardUI.cs` | 전리품 시트 3박자 — 스캐폴드는 에디터 메뉴 [DiceOrbit/Rebuild Reward UI Layout], 런타임 폴백 없음 ([[reward_screen_system]]) |
| 상점 | `ShopUI.cs` | 무대형 (배경/상인 스프라이트 슬롯) |
| 이벤트 | `EventUI.cs` | 무대형 + EventDefinition 에셋 |
| 런 HUD | `RunHudUI.cs` | 상태별 자동 표시/숨김 |

## 새 UI 만들 때 체크리스트

1. 슬롯 필드 선언 (`[Header("슬롯 (씬에서 배치 — [기본 레이아웃 생성]으로 자동 배선)")]`)
2. `[ContextMenu("기본 레이아웃 생성")]` — 중복 가드, 슬롯 배선, SetDirty/MarkSceneDirty
3. Show()에서 폴백 호출 + `BattleInfoPanelUI.SetVisible(false)` (전체화면 UI라면)
4. 색·스프라이트는 `UiSkin.Current` (`Assets/Scripts/UI/Skin/UiSkin.cs`, 에셋 `Resources/UI/UiSkin.asset`) — 팔레트 상수를 파일에 복사하지 않는다. 각 UI는 `private static UiSkin Skin => UiSkin.Current;` 한 줄로 잡고 `Skin.Ink/Accent/...`를 읽는다. 패널/카드/칩/툴팁/슬롯/구분선은 `ApplyPanel/ApplyCard/...`, 버튼은 `ApplyButton(btn, ButtonKind)` (SpriteSwap), 틴트가 필요한 원형은 `ApplyCircle(img, tint)`. (2026-09-25 리스킨 2·3단계 — 코드 UI 13파일 전환 완료)
5. 씬에 배치하는 패널/칩/버튼은 `UiSkinImage` 컴포넌트 + 파트 지정 (`Assets/Scripts/UI/UiSkinImage.cs`, Awake/OnValidate에서 적용). 절차 생성 라운드 사각형(`UiRoundedSprite`)과 `Assets/Art/Generated`는 2026-09-25 철거됨. 점검: 「도구/Dice Orbit/UI 스킨 점검」
6. 정렬 질서: 사이드바 -5 / HUD 100 / 노드맵 900 / 상점·이벤트 1450 / 보상 1500 / 커서 툴팁 30000
7. **레이아웃 그룹이 있는 오브젝트에 스킨 Image를 같이 두지 않는다.** `Image`는 ILayoutElement로 스프라이트 원본 크기를 선호 크기로 내놓고, 같은 우선순위의 LayoutGroup 값과 최댓값이 채택되므로 글자에 맞춰야 할 칩·카드가 스프라이트 크기(칩 312×106, 카드 ~150px)로 부푼다. 배경은 앵커 스트레치 + `LayoutElement.ignoreLayout` 자식(`Bg`)으로 분리한다 (2026-09-25 정보 패널·타일 카드에서 발견)

> 2026-09-26 추가 — 스킨은 역할 카탈로그다. 씬 Image에는 `UiSkinImage.part`(역할), 씬 Button에는 `UiSkinButton.button`(역할)만 적고 스프라이트를 직접 배선하지 않는다. 새 역할이 필요하면 `SkinPart`/`SkinButton`에 값을 명시 번호로 추가하고 `UiSkin.asset`에 항목을 넣는다(비면 점검기가 잡는다). 전용 아트가 오기 전에는 `Provisional=true`로 임시 스프라이트를 꽂는다 — 조용한 폴백 대신 드러난 임시.
