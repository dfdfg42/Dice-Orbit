# UI 리스킨 3단계 — 코드 UI UiSkin 마이그레이션 + UiSkinImage + 라운드 헬퍼 철거 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 코드가 그리는 UI 13파일이 파일마다 복사해 든 색상 상수와 절차 생성 라운드 사각형 대신 `UiSkin.Current`를 읽게 바꾸고, 씬에 박힌 라운드 참조(UiRoundedImage 7개 + Art/Generated 스프라이트 13개)를 `UiSkinImage`로 전환한 뒤 `UiRoundedSprite`·`Art/Generated`·`TutorialSkin`을 삭제한다.

**Architecture:** 각 UI 파일은 `private static UiSkin Skin => UiSkin.Current;` 한 줄로 스킨을 잡고, 색은 `Skin.Ink/Accent/...`, 배경은 `Skin.ApplyPanel/Card/Chip/Slot/Tooltip(image)`, 버튼은 `Skin.ApplyButton(btn, kind)`(SpriteSwap)로 바꾼다. 원형이 필요한 곳(경로 점·배지 링·상태 칩 폴백)은 새 `Skin.Circle`(로컬 생성 흰 원, 틴트 허용)을 쓴다. 씬 배치 UI는 `UiSkinImage(part)`가 Awake/OnValidate에서 같은 헬퍼를 호출한다. `UiRoundedImage.cs`는 `.meta` GUID를 유지한 채 개명해 씬 참조가 끊기지 않게 한다.

**Tech Stack:** Unity 6000.3.8f1, Unity MCP (`Unity_RunCommand`, `Unity_GetConsoleLogs`), 에디터 자가 테스트(`UiSkinSelfTests.RunAll`), 점검 메뉴(`UiSkinValidator`), Python 3.11 + PIL(원형 스프라이트 1장).

**스펙:** `Docs/superpowers/specs/2026-09-25-ui-reskin-uiskin-higgsfield-design.md` §3.3, §3.5, §3.6, §6(3단계). 1·2단계 산출: `Assets/Scripts/UI/Skin/UiSkin.cs`, `Assets/Resources/UI/UiSkin.asset`, `Assets/Scripts/Editor/UiSkinValidator.cs`, `Assets/Scripts/Editor/UiSkinSelfTests.cs`.

## Global Constraints

- 브랜치: `feature/ui-skin-core-20260925`에서 `feature/ui-skin-migration-20260925`를 딴다 (1·2단계 미병합 상태 위에 쌓는다).
- **폴백 금지**: 스킨 필드가 비면 예외(이미 UiSkin이 던진다). 기존 "스프라이트 없으면 단색" 분기는 삭제하고 스킨을 쓴다. 단, 이미 있던 "아이콘 없으면 글자" 류 콘텐츠 폴백은 이번 범위가 아니므로 유지.
- **스프라이트가 색을 가진다**: Apply 헬퍼가 `color = white`로 둔다. 파트 배경에 틴트를 곱하지 않는다. 틴트가 필요한 원형(`ApplyCircle`)만 예외.
- 팔레트 매핑(구 → 신): `Felt`(어두운 배경) → `Skin.Scrim` / `Card`·`Tile`(카드 배경) → `ApplyCard` / `Chip`·`Bar`·`GoldPill` → `ApplyChip` / `Ink`(글자) → `Skin.Ink` / `Gold` → `Skin.Accent` / `GoldInk`·`BtnInk`·`InkDark` → `Skin.Ink` / `Dim`·`InkMuted`·`MutedColor` → `Skin.InkMuted` / `Danger` → `Skin.Danger` / `CardEdge`(선택 강조) → `Skin.Secondary` / `Slate`·`Tan`·`Navy`(보조 버튼) → `ButtonSecondary` / `PaperColor` → `Skin.Paper` / `ChipColor` → `Skin.PaperDeep`.
- 버튼은 ColorBlock 틴트를 지우고 `ApplyButton`(SpriteSwap). `interactable=false`는 그대로 두면 Disabled 스프라이트가 나온다.
- 텍스트 색은 크림 배경 기준: 버튼 라벨 = `Skin.Ink`.
- 컴파일 검증 = `Unity_GetConsoleLogs(logTypes:"Error")` 0건 (typeof 게이트 금지). 각 태스크 끝에 `UiSkinSelfTests.RunAll()` PASS.
- 씬 편집(Task 2·11)은 RunCommand로 하되 **씬이 dirty면 중단**하고 사용자에게 먼저 저장/되돌리기를 요청한다.
- 파일 1개 = 커밋 1개 원칙 (Task 3·10은 묶음 커밋 허용). 메시지 끝 `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## File Structure

| 경로 | 변경 |
|---|---|
| `Assets/Sprites/UI Skin/circle.png` | 신규 — 256×256 흰 원 (PIL 생성, 힉스필드 아님) |
| `Assets/Scripts/UI/Skin/UiSkin.cs` | `SkinPart.Scrim`, `Circle` 필드, `ApplySprite(Image, SkinPart)`, `ApplyCircle(Image, Color)` |
| `Assets/Scripts/Editor/UiSkinValidator.cs` | `Circle` 점검 추가 |
| `Assets/Scripts/Editor/UiSkinSelfTests.cs` | 신규 테스트 4개, 빈 스킨 이슈 18 |
| `Assets/Scripts/UI/UiRoundedImage.cs` → `Assets/Scripts/UI/UiSkinImage.cs` | `git mv`(GUID 유지), 내용 교체: `part` 필드 + Awake/OnValidate Apply |
| `Assets/Scripts/UI/InfoPanel/InfoPanelRows.cs` | 팔레트 상수 10개 삭제, `ModifierColorHex` 프로퍼티화 |
| `Assets/Scripts/UI/InfoPanel/BattleInfoPanelUI.cs`, `InfoPanel/TileInfoPanelUI.cs`, `MonsterActionLabel.cs` | InfoPanelRows 상수 → Skin |
| `Assets/Scripts/UI/RunHudUI.cs`, `NodeMapUI.cs`, `ShopUI.cs`, `EventUI.cs`, `RewardUI.cs` | 상수 삭제 + Apply 헬퍼 |
| `Assets/Scripts/UI/Tutorial/TutorialOverlayUI.cs`, `Tutorial/TutorialPromptUI.cs` | TutorialSkin 의존 제거 → UiSkin |
| `Assets/Scripts/UI/Tutorial/TutorialSkin.cs`, `Assets/Resources/TutorialSkin.asset` | **삭제** (호출처 0) |
| `Assets/Scripts/UI/DiceHoverTooltipUI.cs`, `DiceElement.cs`, `StatusIconRow.cs`, `GameResultUI.cs` | 상수 삭제 + Apply/ApplyCircle |
| `Assets/Scripts/UI/UiRoundedSprite.cs`, `Assets/Art/Generated/` | **삭제** (호출처·참조 0 확인 후) |
| `Assets/Scenes/BattleScene.unity` | RewardCanvas 7개 파트 지정, Art/Generated 참조 13개 → UiSkinImage |
| `Docs/editor_owned_ui_pattern.md`, `Docs/README.md`, 스펙 상태 | 갱신 |

---

### Task 1: Circle 스프라이트 + UiSkin 확장 (Scrim 파트·ApplySprite·ApplyCircle)

**Files:**
- Modify: `Assets/Scripts/Editor/UiSkinSelfTests.cs`
- Modify: `Assets/Scripts/UI/Skin/UiSkin.cs`
- Modify: `Assets/Scripts/Editor/UiSkinValidator.cs`
- Create: `Assets/Sprites/UI Skin/circle.png` (+ .meta는 Unity 생성)
- Modify: `Assets/Resources/UI/UiSkin.asset` (RunCommand로 Circle 배선)

**Interfaces:**
- Produces: `SkinPart.Scrim` (색만 — 스프라이트 없음), `UiSkin.Circle : Sprite`, `void UiSkin.ApplySprite(Image image, SkinPart part)` (Scrim이면 sprite=null·Simple·color=Scrim, 그 외 해당 파트 Sliced·white; 버튼 파트는 Normal 면), `void UiSkin.ApplyCircle(Image image, Color tint)` (Circle·Simple·preserveAspect·color=tint).

- [x] **Step 1: 브랜치**

```bash
cd "D:/Dice Orbit" && git checkout -q feature/ui-skin-core-20260925 && git checkout -q -b feature/ui-skin-migration-20260925 && git branch --show-current
```

- [x] **Step 2: 테스트 먼저 — UiSkinSelfTests.cs 수정**

`RunAll()`의 호출 목록에 4줄 추가 (기존 `TestValidatorPassesCompleteSkin();` 뒤):
```csharp
            TestApplySpriteByPart();
            TestScrimPartIsColorOnly();
            TestApplyCircleKeepsTint();
            TestUiSkinImageAppliesPart();
```
`MakeCompleteSkin()`의 `s.Close = MakeSprite(false);` 뒤에 `s.Circle = MakeSprite(false);` 추가.

`TestGetSpriteCoversEveryPart`의 foreach를 다음으로 교체 (Scrim은 null이 정답):
```csharp
            foreach (SkinPart part in Enum.GetValues(typeof(SkinPart)))
            {
                if (part == SkinPart.Scrim) { Check(skin.GetSprite(part) == null, "GetSprite(Scrim) == null (색만)"); continue; }
                Check(skin.GetSprite(part) != null, $"GetSprite({part}) != null");
            }
```
`TestValidatorReportsEmptySkin`의 기대치를 `issues.Count == 18`로, 라벨을 `"빈 스킨 이슈 18건 (6 sliced + 8 button + 3 icon + circle), 실제 {issues.Count}"`로.

새 테스트 4개 (클래스 끝, 마지막 `}` 두 개 앞에 추가):
```csharp
        private static void TestApplySpriteByPart()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-part", typeof(Image));
            var img = go.GetComponent<Image>();
            skin.ApplySprite(img, SkinPart.Chip);
            Check(img.sprite == skin.Chip && img.type == Image.Type.Sliced && img.color == Color.white, "ApplySprite(Chip): Chip·Sliced·white");
            skin.ApplySprite(img, SkinPart.ButtonSecondary);
            Check(img.sprite == skin.ButtonSecondary.Normal, "ApplySprite(ButtonSecondary): Normal 면");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestScrimPartIsColorOnly()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-scrim", typeof(Image));
            var img = go.GetComponent<Image>();
            img.sprite = skin.Panel;
            skin.ApplySprite(img, SkinPart.Scrim);
            Check(img.sprite == null && img.type == Image.Type.Simple && img.color == skin.Scrim, "ApplySprite(Scrim): sprite null·Simple·Scrim 색");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestApplyCircleKeepsTint()
        {
            var skin = MakeCompleteSkin();
            var go = new GameObject("skin-circle", typeof(Image));
            var img = go.GetComponent<Image>();
            skin.ApplyCircle(img, Color.red);
            Check(img.sprite == skin.Circle && img.type == Image.Type.Simple && img.preserveAspect && img.color == Color.red, "ApplyCircle: Circle·Simple·preserveAspect·틴트 유지");
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(skin);
        }

        private static void TestUiSkinImageAppliesPart()
        {
            // 실제 Resources/UI/UiSkin.asset을 쓴다 (씬 배치 컴포넌트의 통합 확인)
            var go = new GameObject("skin-image", typeof(Image), typeof(DiceOrbit.UI.UiSkinImage));
            var comp = go.GetComponent<DiceOrbit.UI.UiSkinImage>();
            comp.SetPart(SkinPart.Card);
            var img = go.GetComponent<Image>();
            Check(img.sprite == UiSkin.Current.Card && img.type == Image.Type.Sliced, "UiSkinImage.SetPart(Card): Card 스프라이트 Sliced");
            UnityEngine.Object.DestroyImmediate(go);
        }
```
(`TestUiSkinImageAppliesPart`는 Task 2의 `UiSkinImage`를 참조한다. Task 2 완료 전까지 컴파일 에러가 정상 — Task 1 Step 6에서는 이 테스트 1개만 임시로 주석 처리하지 말고, **Task 1과 Task 2를 연달아 진행**한 뒤 함께 검증한다.)

- [x] **Step 3: UiSkin.cs 구현**

enum 교체:
```csharp
    /// <summary>스킨 부품 이름 — Apply 헬퍼와 씬 배치용 UiSkinImage가 공유한다. Scrim은 스프라이트 없이 색만.</summary>
    public enum SkinPart { Panel, Card, Chip, Tooltip, Slot, Divider, ButtonPrimary, ButtonSecondary, Scrim }
```
`[Header("아이콘")]` 블록 뒤에 추가:
```csharp
        [Header("도형")]
        public Sprite Circle;   // 흰 원 — 경로 점·배지 링·상태 칩처럼 틴트가 필요한 원형 전용
```
`GetSprite`의 switch에 `SkinPart.Scrim => null,` 추가 (`_ =>` 앞).
`ApplyDivider` 줄 뒤에 추가:
```csharp
        /// <summary>파트 이름으로 적용 — 씬 배치용 UiSkinImage가 쓴다. 버튼 파트는 Normal 면만(상태는 ApplyButton).</summary>
        public void ApplySprite(Image image, SkinPart part)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (part == SkinPart.Scrim)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Scrim;
                return;
            }
            ApplySliced(image, GetSprite(part), part.ToString());
        }

        /// <summary>틴트 가능한 흰 원 — 경로 점·배지 링·상태 칩 폴백 전용.</summary>
        public void ApplyCircle(Image image, Color tint)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            Require(Circle, nameof(Circle));
            image.sprite = Circle;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = tint;
        }
```

- [x] **Step 4: UiSkinValidator.cs — Circle 점검**

`Validate()`의 `CheckPresent(issues, skin.Close, nameof(skin.Close));` 뒤에:
```csharp
            CheckPresent(issues, skin.Circle,          nameof(skin.Circle));
```

- [x] **Step 5: circle.png 생성·반입·임포트 설정·배선**

```bash
cd "D:/Dice Orbit" && python - <<'EOF'
from PIL import Image, ImageDraw
S, SS = 256, 4
big = Image.new("RGBA", (S*SS, S*SS), (0,0,0,0))
ImageDraw.Draw(big).ellipse([0, 0, S*SS-1, S*SS-1], fill=(255,255,255,255))
big.resize((S, S), Image.LANCZOS).save("Assets/Sprites/UI Skin/circle.png")
print("circle.png", Image.open("Assets/Sprites/UI Skin/circle.png").size)
EOF
```
Expected: `circle.png (256, 256)`.

RunCommand (임포트 + 배선):
```csharp
using UnityEngine;
using UnityEditor;
using DiceOrbit.UI.Skin;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh();
        const string path = "Assets/Sprites/UI Skin/circle.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { result.LogError("임포터 없음: " + path); return; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.Tight;
        importer.SetTextureSettings(settings);
        importer.spriteBorder = Vector4.zero;
        importer.SaveAndReimport();

        var skin = AssetDatabase.LoadAssetAtPath<UiSkin>("Assets/Resources/UI/UiSkin.asset");
        result.RegisterObjectModification(skin);
        skin.Circle = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        result.Log("Circle 배선: {0}", skin.Circle);
    }
}
```
Expected: `Circle 배선: circle`.

- [x] **Step 6: 검증은 Task 2 Step 4에서 함께** (UiSkinImage 테스트 때문에 컴파일이 아직 실패한다). 커밋도 Task 2에서.

---

### Task 2: UiSkinImage (UiRoundedImage 개명, GUID 유지) + RewardCanvas 7개 파트 지정

**Files:**
- Rename: `Assets/Scripts/UI/UiRoundedImage.cs` → `Assets/Scripts/UI/UiSkinImage.cs` (+ `.meta` 동반 `git mv`)
- Modify: `Assets/Scenes/BattleScene.unity` (RunCommand)

**Interfaces:**
- Produces: `DiceOrbit.UI.UiSkinImage : MonoBehaviour` — `[SerializeField] SkinPart part`, `SkinPart Part`, `void SetPart(SkinPart)`, `void Apply()`. Awake·OnValidate에서 Apply.

- [x] **Step 1: git mv (GUID 유지)**

```bash
cd "D:/Dice Orbit" && git mv Assets/Scripts/UI/UiRoundedImage.cs Assets/Scripts/UI/UiSkinImage.cs && git mv Assets/Scripts/UI/UiRoundedImage.cs.meta Assets/Scripts/UI/UiSkinImage.cs.meta && grep guid Assets/Scripts/UI/UiSkinImage.cs.meta
```
Expected: `guid: 6c688f30bc9e2414f819c279aa5df3f4` (씬 7개 참조가 이 GUID를 본다).

- [x] **Step 2: UiSkinImage.cs 내용 교체**

```csharp
using System;
using DiceOrbit.UI.Skin;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 씬에 배치하는 스킨 Image (2026-09-25 리스킨 3단계, 구 UiRoundedImage 개명 — .meta GUID 유지).
    /// 파트만 저장해 두고 Awake/OnValidate에서 UiSkin의 같은 헬퍼로 스프라이트를 꽂는다.
    /// 버튼 파트 + Button 컴포넌트가 있으면 상태 스프라이트(SpriteSwap)까지 세팅한다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UiSkinImage : MonoBehaviour
    {
        [Tooltip("스킨 파트 — 「도구/Dice Orbit/UI 스킨 점검」으로 에셋 상태 확인")]
        [SerializeField] private SkinPart part = SkinPart.Panel;

        public SkinPart Part => part;

        public void SetPart(SkinPart newPart)
        {
            part = newPart;
            Apply();
        }

        private void Awake() => Apply();

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            Apply();   // 인스펙터에서 파트를 바꾸면 씬에 바로 반영 (스프라이트 참조가 씬에 저장됨)
        }

        public void Apply()
        {
            var image = GetComponent<Image>();
            if (image == null) throw new InvalidOperationException($"[UiSkinImage] '{name}'에 Image가 없습니다.");
            var skin = UiSkin.Current;

            if (part == SkinPart.ButtonPrimary || part == SkinPart.ButtonSecondary)
            {
                var button = GetComponent<Button>();
                if (button != null)
                {
                    skin.ApplyButton(button, part == SkinPart.ButtonPrimary ? ButtonKind.Primary : ButtonKind.Secondary);
                    return;
                }
            }
            skin.ApplySprite(image, part);
        }
    }
}
```

- [x] **Step 3: Refresh → 컴파일 확인**

RunCommand `AssetDatabase.Refresh()` → `Unity_GetConsoleLogs(Error)`.
Expected: 0건. (남아 있으면 Task 1·2 코드의 오타 — 에러 메시지대로 고친다.)

- [x] **Step 4: 자가 테스트 + 점검**

```
Unity_RunCommand: bool ok = DiceOrbit.EditorTools.UiSkinSelfTests.RunAll(); result.Log("{0}", ok ? "PASS" : "FAIL");
Unity_RunCommand: var n = DiceOrbit.EditorTools.UiSkinValidator.Validate(DiceOrbit.UI.Skin.UiSkin.Current).Count; result.Log("issues={0}", n);
```
Expected: `PASS`, `issues=0`.

- [x] **Step 5: RewardCanvas 7개 파트 지정 (씬 편집)**

```csharp
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DiceOrbit.UI;
using DiceOrbit.UI.Skin;

internal class CommandScript : IRunCommand
{
    private static readonly (string path, SkinPart part)[] Map =
    {
        ("RewardCanvas/MainPanel", SkinPart.Panel),
        ("RewardCanvas/UpgradePanel", SkinPart.Panel),
        ("RewardCanvas/MainPanel/Paper", SkinPart.Card),
        ("RewardCanvas/UpgradePanel/Paper", SkinPart.Card),
        ("RewardCanvas/MainPanel/GoldBar", SkinPart.Chip),
        ("RewardCanvas/MainPanel/계속Button", SkinPart.ButtonPrimary),
        ("RewardCanvas/UpgradePanel/취소Button", SkinPart.ButtonSecondary),
    };

    public void Execute(ExecutionResult result)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "BattleScene") { result.LogError("BattleScene이 활성 씬이 아닙니다: " + scene.name); return; }
        if (scene.isDirty) { result.LogError("씬에 저장 안 된 변경이 있습니다 — 먼저 저장하거나 되돌린 뒤 다시 실행하세요."); return; }

        var all = Resources.FindObjectsOfTypeAll<UiSkinImage>().Where(c => c.gameObject.scene == scene).ToArray();
        int done = 0;
        foreach (var (path, part) in Map)
        {
            var comp = all.FirstOrDefault(c => HierarchyPath(c.transform) == path);
            if (comp == null) { result.LogError("UiSkinImage 없음: " + path); continue; }
            var so = new SerializedObject(comp);
            so.FindProperty("part").enumValueIndex = (int)part;
            so.ApplyModifiedPropertiesWithoutUndo();
            comp.Apply();
            EditorUtility.SetDirty(comp);
            EditorUtility.SetDirty(comp.GetComponent<UnityEngine.UI.Image>());
            done++;
            result.Log("{0} → {1}", path, part);
        }
        if (done != Map.Length) { result.LogError("일부 실패 — 씬을 저장하지 않았습니다"); return; }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        result.Log("BattleScene 저장 — RewardCanvas 파트 {0}개", done);
    }

    private static string HierarchyPath(Transform t)
    {
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
```
Expected: 7줄 `→` 로그 + `BattleScene 저장 — RewardCanvas 파트 7개`. `isDirty` 중단이면 사용자에게 알리고 대기.

- [x] **Step 6: 씬 검증 + 커밋**

```bash
cd "D:/Dice Orbit/Assets" && python - <<'EOF'
import re
s=open("Scenes/BattleScene.unity",encoding="utf-8").read()
blocks=[b for b in s.split("--- !u!") if b.startswith("114") and "6c688f30bc9e2414f819c279aa5df3f4" in b]
print("UiSkinImage 컴포넌트:", len(blocks), "| radius 잔존:", sum("radius:" in b for b in blocks), "| part 값:", sorted(re.search(r"part: (\d+)",b).group(1) for b in blocks if re.search(r"part: (\d+)",b)))
EOF
cd "D:/Dice Orbit" && git add Assets/Scripts/UI/Skin/UiSkin.cs Assets/Scripts/Editor/UiSkinValidator.cs Assets/Scripts/Editor/UiSkinSelfTests.cs "Assets/Sprites/UI Skin/circle.png" "Assets/Sprites/UI Skin/circle.png.meta" Assets/Resources/UI/UiSkin.asset Assets/Scripts/UI/UiSkinImage.cs Assets/Scripts/UI/UiSkinImage.cs.meta Assets/Scenes/BattleScene.unity && git -c core.quotepath=off status --short && git commit -q -m "feat(ui-skin): UiSkinImage(구 UiRoundedImage 개명) + Circle 도형 + Scrim 파트 — RewardCanvas 7개 파트 지정

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```
Expected: `UiSkinImage 컴포넌트: 7 | radius 잔존: 0 | part 값: ['0','0','1','1','2','6','7']` (Panel=0, Card=1, Chip=2, ButtonPrimary=6, ButtonSecondary=7).

---

### Task 3: InfoPanelRows 팔레트 → UiSkin (+ BattleInfoPanelUI·TileInfoPanelUI·MonsterActionLabel)

**Files:**
- Modify: `Assets/Scripts/UI/InfoPanel/InfoPanelRows.cs:13-28, 68`
- Modify: `Assets/Scripts/UI/InfoPanel/BattleInfoPanelUI.cs:325,327,339,349,427`
- Modify: `Assets/Scripts/UI/InfoPanel/TileInfoPanelUI.cs:79-91, 167-170, 190`
- Modify: `Assets/Scripts/UI/MonsterActionLabel.cs:139,144,181`

- [x] **Step 1: InfoPanelRows.cs — 상수 삭제, Skin 프록시 없음**

14~25행의 `public static readonly Color ...` 10줄과 28행 `public const string ModifierColorHex = "#6A48B8";`를 삭제하고 그 자리에:
```csharp
        private static UiSkin Skin => UiSkin.Current;

        /// <summary>모디파이어 효과 라인용 리치텍스트 색 (스킬 설명에 인라인 삽입 시).</summary>
        public static string ModifierColorHex => "#" + ColorUtility.ToHtmlStringRGB(Skin.Modifier);
```
파일 상단 using에 `using DiceOrbit.UI.Skin;` 추가. 68행 `SectionTitleColor` → `Skin.Ink`.

- [x] **Step 2: BattleInfoPanelUI.cs**

`using DiceOrbit.UI.Skin;` 추가. 327행 `InfoPanelRows.InkDark` → `UiSkin.Current.Ink`, 339행 `InfoPanelRows.PassiveColor` → `UiSkin.Current.Passive`, 349행 `InfoPanelRows.ModifierColor` → `UiSkin.Current.Modifier`, 427행 `InfoPanelRows.InkDark` → `UiSkin.Current.Ink`. (325행 `ModifierColorHex`는 프로퍼티라 그대로.)

- [x] **Step 3: TileInfoPanelUI.cs**

`using DiceOrbit.UI.Skin;` 추가. 79~91행 블록을:
```csharp
                var sprite = t.Type == Data.TileType.LevelUp ? levelUpTileSprite : normalTileSprite;
                if (sprite != null)
                {
                    tileImage.sprite = sprite;
                    tileImage.type = Image.Type.Simple;
                    tileImage.color = Color.white;
                }
                else
                {
                    UiSkin.Current.ApplySlot(tileImage);   // 스프라이트 미지정: 스킨 슬롯(아이콘의 홈)
                }
```
167~170행(`var bg = ...` 4줄)을:
```csharp
            var bg = card.AddComponent<Image>();
            UiSkin.Current.ApplyCard(bg);
            bg.raycastTarget = true;                                // 키워드 링크 호버 영역 확보
```
190행 `InfoPanelRows.MutedColor` → `UiSkin.Current.InkMuted`.

- [x] **Step 4: MonsterActionLabel.cs**

`using DiceOrbit.UI.Skin;` 추가. 139행 `bg.color = InfoPanelRows.PaperColor;` → `UiSkin.Current.ApplyTooltip(bg);` (말풍선 = 툴팁 파트, 색은 스프라이트가 가진다). 144행 `var ink = InfoPanelRows.InkDark;` → `var ink = UiSkin.Current.Ink;`. 181행 `InfoPanelRows.InkDark` → `UiSkin.Current.Ink`.
`Outline` 컴포넌트(145~148행)는 스프라이트에 외곽선이 있으므로 **삭제** (3줄 + `var ink` 줄이 다른 곳에서 안 쓰이면 함께).

- [x] **Step 5: 컴파일·테스트·커밋**

Refresh → 콘솔 에러 0 → 자가 테스트 PASS.
```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/UI/InfoPanel/InfoPanelRows.cs Assets/Scripts/UI/InfoPanel/BattleInfoPanelUI.cs Assets/Scripts/UI/InfoPanel/TileInfoPanelUI.cs Assets/Scripts/UI/MonsterActionLabel.cs && git commit -q -m "refactor(ui-skin): 정보 패널 팔레트를 UiSkin으로 — InfoPanelRows 상수 삭제, 타일/속성 카드·몬스터 말풍선 스킨 적용

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: RunHudUI

**Files:**
- Modify: `Assets/Scripts/UI/RunHudUI.cs:41-47, 128-165, 178, 197, 230-315`

- [x] **Step 1: 상수 삭제 + Skin**

41~47행 7개 상수를 삭제하고:
```csharp
        private static UiSkin Skin => UiSkin.Current;
```
`using DiceOrbit.UI.Skin;` 추가.

- [x] **Step 2: 골드 코인 — Resources.Load 제거**

`RefreshGold()`를:
```csharp
        private void RefreshGold()
        {
            if (goldText == null) return;
            EnsureCoinIcon();
            goldText.text = $"{GoldManager.Instance?.Gold ?? 0}";
        }
```
`EnsureCoinIcon()`의
```csharp
            var coin = Resources.Load<Sprite>("UI/코인");
            if (coin == null) return;   // 스프라이트 없으면 ● 폴백 유지
```
를 `var coin = Skin.Coin;`로 (null이면 UiSkin 점검이 잡는다 — 여기서 `Require`가 없으므로 `_coinIcon.sprite = coin;` 앞에 `if (coin == null) throw new System.InvalidOperationException("[RunHudUI] UiSkin.Coin이 비어 있습니다 — 「도구/Dice Orbit/UI 스킨 점검」");` 추가).

- [x] **Step 3: 칩 — tint 파라미터 제거, Chip/Slot 스프라이트**

시그니처 `CreateChip(RectTransform parent, Sprite icon, string fallbackName, Color tint, bool empty = false, bool bare = false)` → `CreateChip(RectTransform parent, Sprite icon, string fallbackName, bool empty = false, bool bare = false)`. 178행 호출 `..., title, RelicTint, bare: true)` → `..., title, bare: true)`. 197행 `..., PotionTint, empty: !filled)` → `..., empty: !filled)`. `CreateChipFromPrefab(..., Color tint, bool empty)` → `(..., bool empty)`, 그 안의 `bg.color = empty ? CardWell : ...` 3줄을:
```csharp
            var bg = go.GetComponent<Image>();
            if (bg != null) { if (empty) Skin.ApplySlot(bg); else Skin.ApplyChip(bg); }
```
기본 생성 경로의
```csharp
            var bg = go.AddComponent<Image>();
            bg.sprite = UiRoundedSprite.Get(12);
            bg.type = Image.Type.Sliced;
            // bare 칩의 배경은 투명 — 시각적으론 아이콘만, 호버 히트 영역으로만 기능
            bg.color = bareIcon ? Color.clear : empty ? CardWell : Color.Lerp(CardWell, tint, 0.35f);
```
를:
```csharp
            var bg = go.AddComponent<Image>();
            if (bareIcon) bg.color = Color.clear;          // 배경판 없이 아이콘만 — 호버 히트 영역으로만 기능
            else if (empty) Skin.ApplySlot(bg);            // 빈 홈
            else Skin.ApplyChip(bg);
```
274행 `tmp.color = Ink;` → `tmp.color = Skin.Ink;`. `empty`면 슬롯 안에 `Skin.PotionSlotEmpty` 아이콘을 넣는다 — `if (empty) return go;` 를:
```csharp
            if (empty)
            {
                var emptyIcon = new GameObject("EmptyIcon", typeof(RectTransform));
                emptyIcon.transform.SetParent(go.transform, false);
                var er = (RectTransform)emptyIcon.transform;
                er.anchorMin = Vector2.zero; er.anchorMax = Vector2.one;
                er.offsetMin = new Vector2(10f, 10f); er.offsetMax = new Vector2(-10f, -10f);
                var eimg = emptyIcon.AddComponent<Image>();
                eimg.sprite = Skin.PotionSlotEmpty;
                eimg.preserveAspect = true;
                eimg.raycastTarget = false;
                eimg.color = new Color(1f, 1f, 1f, 0.55f);   // 빈 자리 표시는 흐리게
                return go;
            }
```

- [x] **Step 4: 컴파일·테스트·커밋**

Refresh → 에러 0 → PASS.
```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/UI/RunHudUI.cs && git commit -q -m "refactor(ui-skin): RunHudUI를 UiSkin으로 — 칩/빈 슬롯/코인 스프라이트, 팔레트 상수·Resources.Load 제거

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: NodeMapUI

**Files:**
- Modify: `Assets/Scripts/UI/NodeMapUI.cs:52-57, 92-96, 155, 207-218, 234-266, 277-284`

- [x] **Step 1: 상수 삭제 + Skin**

52~57행 6개 상수 삭제 → `private static UiSkin Skin => UiSkin.Current;`, `using DiceOrbit.UI.Skin;`.

- [x] **Step 2: 배경**

```csharp
            if (backgroundImage != null)
            {
                backgroundImage.sprite = backgroundSprite;
                backgroundImage.color = backgroundSprite != null ? Color.white : Skin.Scrim;
            }
```

- [x] **Step 3: 간선 색 (155행)**

`Color c = active ? Gold : traveled ? CardEdge : new Color(0.25f, 0.27f, 0.34f);` → `Color c = active ? Skin.Accent : traveled ? Skin.Secondary : Skin.InkMuted;`

- [x] **Step 4: 경로 점 (207~218행)**

```csharp
                var img = dotGo.AddComponent<Image>();
                if (edgeDotSprite != null)
                {
                    img.sprite = edgeDotSprite;
                    img.preserveAspect = true;
                    img.color = color;
                }
                else
                {
                    Skin.ApplyCircle(img, color);   // 기본 = 스킨 원형 점 (edgeDotSprite는 선택 오버라이드)
                }
                img.raycastTarget = false;
```

- [x] **Step 5: 노드 이미지 (234~266행)**

`else` 분기(스킨 스프라이트 없음)를 다음으로 — 노드 스프라이트 미배선은 배선 오류이므로 알린다:
```csharp
            else
            {
                Debug.LogError($"[NodeMapUI] {node.Type} 노드 스프라이트가 비어 있습니다 — 인스펙터의 Node Sprites를 배선하세요.");
                Skin.ApplyCard(img);
            }
```
라벨(스킨 없을 때만) 266행 `label.color = node.Visited && !isCurrent ? Dim : Ink;` → `label.color = node.Visited && !isCurrent ? Skin.InkMuted : Skin.Ink;`

- [x] **Step 6: 클릭 (277~284행)** — ColorBlock은 통짜 아이콘의 밝기 상태를 표현하므로 **유지**한다(스프라이트 스왑 대상이 아님). `cb.highlightedColor = skin != null ? Color.white : new Color(1.2f, 1.2f, 1.2f);` → `cb.highlightedColor = Color.white;`

- [x] **Step 7: 컴파일·테스트·커밋**

```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/UI/NodeMapUI.cs && git commit -q -m "refactor(ui-skin): NodeMapUI를 UiSkin으로 — 간선/점/라벨 팔레트, 경로 점 Circle, 노드 스프라이트 미배선은 에러

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: ShopUI

**Files:**
- Modify: `Assets/Scripts/UI/ShopUI.cs:65-72, 109-113, 178, 273, 281-300, 457-474, 499`

- [x] **Step 1: 상수 삭제 + Skin** (65~72행 8개 → `private static UiSkin Skin => UiSkin.Current;`, using 추가)

- [x] **Step 2: 배경** — 112행 `Felt` → `Skin.Scrim`.

- [x] **Step 3: 골드 리치텍스트** — 178행·273행의 `ColorUtility.ToHtmlStringRGB(Gold)` → `ColorUtility.ToHtmlStringRGB(Skin.Accent)`.

- [x] **Step 4: 상품 카드 버튼 (281~300행)**

```csharp
            var img = go.AddComponent<Image>();
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var btn = go.AddComponent<Button>();
            Skin.ApplyButton(btn, ButtonKind.Secondary);   // 상품 = 보조 톤(하늘), 품절/불가는 Disabled 면
            btn.interactable = !sold && affordable;
            btn.onClick.AddListener(() => onBuy());
```
(`UiRoundedSprite.Get(14)`·`Image.Type.Sliced`·`fill`·ColorBlock 8줄 삭제.)

- [x] **Step 5: 선택 카드 버튼 (457~474행)**

```csharp
            var img = go.AddComponent<Image>();
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var btn = go.AddComponent<Button>();
            Skin.ApplyButton(btn, ButtonKind.Primary);
            btn.onClick.AddListener(() => onClick());
```

- [x] **Step 6: 텍스트** — 499행 `tmp.color = Ink;` → `tmp.color = Skin.Ink;`. 품절 라벨의 `#777777`/`#666666`/`#B05050` 인라인 hex는 의미색(품절 회색·불가 적색)이라 유지.

- [x] **Step 7: 컴파일·테스트·커밋**

```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/UI/ShopUI.cs && git commit -q -m "refactor(ui-skin): ShopUI를 UiSkin으로 — 상품/선택 카드 SpriteSwap 버튼, 팔레트 상수 제거

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: EventUI

**Files:**
- Modify: `Assets/Scripts/UI/EventUI.cs:48-54, 110-112, 279, 300, 337-340, 420-440, 454`

- [x] **Step 1: 상수 삭제 + Skin** (48~54행 7개 → 프로퍼티, using)

- [x] **Step 2: 배경** — 111행 `Felt` → `Skin.Scrim`. 279행 `label.color = Gold;` → `Skin.Accent`. 300행 `success ? Gold : Danger` → `success ? Skin.Accent : Skin.Danger`.

- [x] **Step 3: 주사위 타일 (337~340행)**

```csharp
                var img = die.AddComponent<Image>();
                Skin.ApplySlot(img);   // 눈이 들어갈 홈
                img.raycastTarget = false;
```

- [x] **Step 4: 선택지 바 (420~440행)**

```csharp
            var img = go.AddComponent<Image>();
            var btn = go.AddComponent<Button>();
            Skin.ApplyButton(btn, primary ? ButtonKind.Primary : ButtonKind.Secondary);
            btn.interactable = interactable;
            btn.onClick.AddListener(() => onClick());

            var txt = CreateText(go, label, 26, FontStyles.Bold);
            if (!interactable) txt.color = new Color(txt.color.r, txt.color.g, txt.color.b, 0.45f);
            Stretch(txt);
            return btn;
```
(`fill`·ColorBlock 9줄, `txt.color = primary ? GoldInk : Ink;` 삭제 — CreateText가 `Skin.Ink`를 준다.)

- [x] **Step 5: 텍스트** — 454행 `tmp.color = Ink;` → `Skin.Ink`.

- [x] **Step 6: 컴파일·테스트·커밋**

```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/UI/EventUI.cs && git commit -q -m "refactor(ui-skin): EventUI를 UiSkin으로 — 선택지 SpriteSwap 버튼, 주사위 홈 Slot, 팔레트 상수 제거

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: RewardUI

**Files:**
- Modify: `Assets/Scripts/UI/RewardUI.cs:47-53, 193, 268, 284, 296, 300, 343, 437-445, 463`

- [x] **Step 1: 팔레트** — 48~50행 `Tile/Bar/Ink` 삭제 → `private static UiSkin Skin => UiSkin.Current;`, using. 53행 `TileRadius`·54행 `PillRadius` const는 `MakeRoundImage` 호출이 사라지면 미사용이므로 삭제.

- [x] **Step 2: MakeRoundImage → MakeSkinImage**

```csharp
        /// <summary>스킨 파트 Image 하나.</summary>
        private RectTransform MakeSkinImage(RectTransform parent, string name, SkinPart part)
        {
            var rt = MakeChild(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            Skin.ApplySprite(img, part);
            img.raycastTarget = false;
            return rt;
        }
```
호출처 전환: `MakeRoundImage(root, "Card", Tile, TileRadius)` → `MakeSkinImage(root, "Card", SkinPart.Card)`. 그 외 `MakeRoundImage(` 호출을 모두 grep해 `Tile`→`SkinPart.Card`, `Bar`→`SkinPart.Chip`으로 (`grep -n "MakeRoundImage" RewardUI.cs`로 전수 확인).

- [x] **Step 3: 텍스트 색** — `Ink` 참조(193·268·296·300·343·463행) → `Skin.Ink` (463행은 `new Color(Skin.Ink.r, Skin.Ink.g, Skin.Ink.b, 0.75f)`).

- [x] **Step 4: 컴파일·테스트·커밋**

```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/UI/RewardUI.cs && git commit -q -m "refactor(ui-skin): RewardUI 동적 카드를 UiSkin으로 — MakeRoundImage→MakeSkinImage, 팔레트 상수 제거

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: 튜토리얼 2종 → UiSkin, TutorialSkin 삭제

**Files:**
- Modify: `Assets/Scripts/UI/Tutorial/TutorialOverlayUI.cs:28-37, 51-88, 124, 204-214, 232`
- Modify: `Assets/Scripts/UI/Tutorial/TutorialPromptUI.cs` (전체 재작성)
- Delete: `Assets/Scripts/UI/Tutorial/TutorialSkin.cs` (+ .meta), `Assets/Resources/TutorialSkin.asset` (+ .meta)

- [x] **Step 1: TutorialOverlayUI.cs**

28~37행(상수 6개 + `panelSprite`/`buttonSprite` 필드·주석) 삭제 → `private static UiSkin Skin => UiSkin.Current;`, using 추가. `Build()` 시작의 `var skin = TutorialSkin.Get(); if (skin != null) {...}` 2줄 삭제. Dim 이미지: `NewImage(transform, Dim, "Dim" + i)` → `NewImage(transform, Skin.Scrim, "Dim" + i)`; 124행 `d.color = Dim;` → `d.color = Skin.Scrim;`.
버블:
```csharp
            var bubbleImg = NewImage(transform, Color.white, "Bubble");
            Skin.ApplyTooltip(bubbleImg);
            bubble = bubbleImg.rectTransform;
            bubble.anchorMin = bubble.anchorMax = bubble.pivot = new Vector2(0.5f, 0.5f);
            bubble.sizeDelta = new Vector2(560, 150);

            bubbleText = NewText(bubble, "", 26);
            bubbleText.color = Skin.Ink;
```
버튼 생성 호출: `MakeButton(bubble, "다음", ..., Gold)` → `MakeButton(bubble, "다음", new Vector2(-24, 16), new Vector2(1, 0), new Vector2(140, 40), ButtonKind.Primary)`, 스킵 → `ButtonKind.Secondary`.
`MakeButton`:
```csharp
        private Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 anchor, Vector2 size, ButtonKind kind)
        {
            var img = NewImage(parent, Color.white, "Btn");
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var btn = img.gameObject.AddComponent<Button>();
            Skin.ApplyButton(btn, kind);
            var t = NewText(img.rectTransform, text, 20); Stretch(t.rectTransform);
            t.alignment = TextAlignmentOptions.Center;
            t.color = Skin.Ink;
            return btn;
        }
```
`NewText`의 `t.color = Ink;` → `t.color = Skin.Ink;` (static이므로 `UiSkin.Current.Ink`).

- [x] **Step 2: TutorialPromptUI.cs 전체**

```csharp
using System;
using DiceOrbit.UI.Skin;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceOrbit.UI.Tutorial
{
    /// <summary>"튜토리얼 하시겠어요?" 런타임 모달. 씬 배치 불필요 — Show()가 캔버스째 생성. 룩은 UiSkin.</summary>
    public class TutorialPromptUI : MonoBehaviour
    {
        private static UiSkin Skin => UiSkin.Current;

        public static void Show(Action onYes, Action onNo)
        {
            var go = new GameObject("TutorialPromptUI");
            var self = go.AddComponent<TutorialPromptUI>();
            self.Build(onYes, onNo);
        }

        private void Build(Action onYes, Action onNo)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            gameObject.AddComponent<GraphicRaycaster>();

            var dim = NewImage(transform, Skin.Scrim);
            Stretch(dim.rectTransform);

            var panel = NewImage(transform, Color.white);
            Skin.ApplyPanel(panel);
            var prt = panel.rectTransform;
            prt.sizeDelta = new Vector2(560, 260);
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;

            var label = NewText(panel.transform, "튜토리얼을 시작할까요?", 34);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0.5f); lrt.anchorMax = new Vector2(1, 1f);
            lrt.offsetMin = new Vector2(20, 0); lrt.offsetMax = new Vector2(-20, -20);

            MakeButton(panel.transform, "시작", new Vector2(-130, -70), ButtonKind.Primary, () => { Close(); onYes?.Invoke(); });
            MakeButton(panel.transform, "건너뛰기", new Vector2(130, -70), ButtonKind.Secondary, () => { Close(); onNo?.Invoke(); });
        }

        private void Close() => Destroy(gameObject);

        private void MakeButton(Transform parent, string text, Vector2 pos, ButtonKind kind, Action onClick)
        {
            var img = NewImage(parent, Color.white);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(200, 66);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            var btn = img.gameObject.AddComponent<Button>();
            Skin.ApplyButton(btn, kind);
            btn.onClick.AddListener(() => onClick());
            var t = NewText(img.transform, text, 28);
            Stretch(t.rectTransform);
        }

        private static Image NewImage(Transform parent, Color c)
        {
            var go = new GameObject("Img", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            return img;
        }

        private static TextMeshProUGUI NewText(Transform parent, string s, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.alignment = TextAlignmentOptions.Center;
            t.color = Skin.Ink; t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
    }
}
```

- [x] **Step 3: TutorialSkin 삭제**

```bash
cd "D:/Dice Orbit" && grep -rn "TutorialSkin" --include=*.cs Assets/Scripts | grep -v "Tutorial/TutorialSkin.cs" ; echo "(위에 출력 없어야 함)"; git rm -q Assets/Scripts/UI/Tutorial/TutorialSkin.cs Assets/Scripts/UI/Tutorial/TutorialSkin.cs.meta Assets/Resources/TutorialSkin.asset Assets/Resources/TutorialSkin.asset.meta && git status --short
```

- [x] **Step 4: 컴파일·테스트·커밋**

Refresh → 에러 0 (TutorialDevTools 등에서 TutorialSkin을 쓰면 에러가 뜬다 → 그 참조도 삭제) → PASS.
```bash
cd "D:/Dice Orbit" && git add Assets/Scripts/UI/Tutorial && git commit -q -m "refactor(ui-skin): 튜토리얼 오버레이·프롬프트를 UiSkin으로 — TutorialSkin(SO·에셋) 삭제, 단색 폴백 제거

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: 소형 4종 — DiceHoverTooltipUI · DiceElement · StatusIconRow · GameResultUI

**Files:**
- Modify: `Assets/Scripts/UI/DiceHoverTooltipUI.cs:28-29, 189, 218, 226-229`
- Modify: `Assets/Scripts/UI/DiceElement.cs:173-187`
- Modify: `Assets/Scripts/UI/StatusIconRow.cs:128-131`
- Modify: `Assets/Scripts/UI/GameResultUI.cs:107-108, 139-141`

- [x] **Step 1: DiceHoverTooltipUI** — 28~29행 `Ink`/`Card` 삭제 → `private static UiSkin Skin => UiSkin.Current;`, using. 189행 `txt.color = Ink;` → `Skin.Ink`. 218행 `CreateCell(effect.Preview(), effect.Icon, Ink)` → `Skin.Ink`. 226~229행:
```csharp
            var bg = cell.GetComponent<Image>();
            Skin.ApplyCard(bg);
            bg.raycastTarget = false;
```
`HintRed`(216행)는 의미색 → 유지.

- [x] **Step 2: DiceElement** — `MakeCircle` 안의
```csharp
                img.sprite = UiRoundedSprite.Get(circleRadius);
                img.type = Image.Type.Sliced;
                img.color = color;
```
→ `UiSkin.Current.ApplyCircle(img, color);`. `int circleRadius = ...` 줄 삭제(미사용). using 추가. Badge 색 4개는 배지 의미색 → 유지.

- [x] **Step 3: StatusIconRow** — 128~131행:
```csharp
                var bg = go.AddComponent<Image>();
                UiSkin.Current.ApplyCircle(bg, new Color(data.Color.r, data.Color.g, data.Color.b, 0.9f));
                bg.raycastTarget = false;
```
using 추가.

- [x] **Step 4: GameResultUI** — 108행 `dimImg.color = new Color(0f, 0f, 0f, 0.82f);` → `dimImg.color = UiSkin.Current.Scrim;`. 139~141행:
```csharp
            var btnImg = btnGO.gameObject.AddComponent<Image>();
            ui.restartButton = btnGO.gameObject.AddComponent<Button>();
            UiSkin.Current.ApplyButton(ui.restartButton, ButtonKind.Primary);
            ui.restartButton.onClick.AddListener(ui.OnRestartClicked);
```
라벨 색(`label.color`, 150행 근처)이 흰색이면 `UiSkin.Current.Ink`로. using 추가. 타이틀 색 2개(GameOver/Victory)는 유지.

- [x] **Step 5: 컴파일·테스트·커밋**

```bash
cd "D:/Dice Orbit" && grep -rn "UiRoundedSprite" --include=*.cs Assets/Scripts | grep -v "UiRoundedSprite.cs" ; echo "(위에 출력 없어야 함 — 남으면 그 파일도 전환)"; git add Assets/Scripts/UI/DiceHoverTooltipUI.cs Assets/Scripts/UI/DiceElement.cs Assets/Scripts/UI/StatusIconRow.cs Assets/Scripts/UI/GameResultUI.cs && git commit -q -m "refactor(ui-skin): 주사위 툴팁·배지·상태 칩·결과창을 UiSkin으로 — 원형은 Circle 틴트, 라운드 헬퍼 호출 0

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 11: 씬 Art/Generated 참조 13개 → UiSkinImage, 폴더·헬퍼 삭제

**Files:**
- Modify: `Assets/Scenes/BattleScene.unity` (RunCommand)
- Delete: `Assets/Art/Generated/` (전체), `Assets/Scripts/UI/UiRoundedSprite.cs` (+ .meta)

파트 매핑 (2026-09-25 씬 조사):

| 계층 경로 | 파트 |
|---|---|
| `InfoPanelCanvas/Panel/Header/NameText_Chip` | Chip |
| `InfoPanelCanvas/Panel/ActivesSection/Title_Chip` | Chip |
| `InfoPanelCanvas/Panel/PassivesSection/Title_Chip` | Chip |
| `InfoPanelCanvas/Panel/ModifiersSection/Title_Chip` | Chip |
| `_ShopCanvas/GoldPill` | Chip |
| `_ShopCanvas/StepPanel` | Panel |
| `_ShopCanvas/StepPanel/BG` | Card |
| `_ShopCanvas/StepPanel/CancelButton` | ButtonSecondary |
| `_ShopCanvas/SwapButton` | ButtonPrimary |
| `_ShopCanvas/LeaveButton` | ButtonSecondary |
| `_EventCanvas/RightColumn/ChoiceColumn/Choice_도전한다` | ButtonPrimary |
| `_EventCanvas/RightColumn/ChoiceColumn/Choice_지나간다` | ButtonSecondary |
| `_NodeMapCanvas/Felt (1)` | Scrim |

- [x] **Step 1: RunCommand — UiSkinImage 부착 + 파트 지정 + 저장**

```csharp
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DiceOrbit.UI;
using DiceOrbit.UI.Skin;

internal class CommandScript : IRunCommand
{
    private static readonly (string path, SkinPart part)[] Map =
    {
        ("InfoPanelCanvas/Panel/Header/NameText_Chip", SkinPart.Chip),
        ("InfoPanelCanvas/Panel/ActivesSection/Title_Chip", SkinPart.Chip),
        ("InfoPanelCanvas/Panel/PassivesSection/Title_Chip", SkinPart.Chip),
        ("InfoPanelCanvas/Panel/ModifiersSection/Title_Chip", SkinPart.Chip),
        ("_ShopCanvas/GoldPill", SkinPart.Chip),
        ("_ShopCanvas/StepPanel", SkinPart.Panel),
        ("_ShopCanvas/StepPanel/BG", SkinPart.Card),
        ("_ShopCanvas/StepPanel/CancelButton", SkinPart.ButtonSecondary),
        ("_ShopCanvas/SwapButton", SkinPart.ButtonPrimary),
        ("_ShopCanvas/LeaveButton", SkinPart.ButtonSecondary),
        ("_EventCanvas/RightColumn/ChoiceColumn/Choice_도전한다", SkinPart.ButtonPrimary),
        ("_EventCanvas/RightColumn/ChoiceColumn/Choice_지나간다", SkinPart.ButtonSecondary),
        ("_NodeMapCanvas/Felt (1)", SkinPart.Scrim),
    };

    public void Execute(ExecutionResult result)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "BattleScene") { result.LogError("BattleScene이 활성 씬이 아닙니다: " + scene.name); return; }
        if (scene.isDirty) { result.LogError("씬에 저장 안 된 변경이 있습니다 — 먼저 저장하거나 되돌린 뒤 다시 실행하세요."); return; }

        var images = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Image>().Where(i => i.gameObject.scene == scene).ToArray();
        int done = 0;
        foreach (var (path, part) in Map)
        {
            var img = images.FirstOrDefault(i => HierarchyPath(i.transform) == path);
            if (img == null) { result.LogError("Image 없음: " + path); continue; }
            var comp = img.GetComponent<UiSkinImage>();
            if (comp == null) comp = Undo.AddComponent<UiSkinImage>(img.gameObject);
            var so = new SerializedObject(comp);
            so.FindProperty("part").enumValueIndex = (int)part;
            so.ApplyModifiedPropertiesWithoutUndo();
            comp.Apply();
            EditorUtility.SetDirty(comp); EditorUtility.SetDirty(img);
            done++;
            result.Log("{0} → {1}", path, part);
        }
        if (done != Map.Length) { result.LogError("일부 실패 — 씬을 저장하지 않았습니다"); return; }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        result.Log("BattleScene 저장 — UiSkinImage {0}개 부착", done);
    }

    private static string HierarchyPath(Transform t)
    {
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
```
Expected: 13줄 + 저장 로그.

- [x] **Step 2: 참조 0 확인 → 삭제**

```bash
cd "D:/Dice Orbit/Assets" && total=0; for m in Art/Generated/RoundedRect_*.asset.meta; do g=$(grep -m1 "^guid:" "$m" | awk '{print $2}'); n=$(cat Scenes/*.unity Prefabs/*.prefab | grep -c "$g"); total=$((total+n)); done; echo "Art/Generated 잔여 참조: $total"; grep -rn "UiRoundedSprite" --include=*.cs Scripts | grep -v "UiRoundedSprite.cs" | wc -l
```
Expected: `Art/Generated 잔여 참조: 0`, `0`. 둘 다 0일 때만:
```bash
cd "D:/Dice Orbit" && git rm -rq Assets/Art/Generated Assets/Art/Generated.meta Assets/Scripts/UI/UiRoundedSprite.cs Assets/Scripts/UI/UiRoundedSprite.cs.meta && ls Assets/Art 2>/dev/null; git status --short | head
```
(`Assets/Art`가 비면 `Assets/Art.meta`도 `git rm`.)

- [x] **Step 3: 컴파일·테스트·커밋**

Refresh → 에러 0 → PASS → `Unity_GetConsoleLogs(logTypes:"Warning")`에 "missing script"/"sprite" 경고 없음 확인.
```bash
cd "D:/Dice Orbit" && git add -A Assets/Scenes/BattleScene.unity Assets/Art Assets/Scripts/UI && git commit -q -m "refactor(ui-skin): 씬 라운드 참조 13개를 UiSkinImage로 — Art/Generated·UiRoundedSprite 삭제

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 12: 플레이 검증 + 문서 갱신

**Files:**
- Modify: `Docs/editor_owned_ui_pattern.md` (체크리스트 5, 적용 현황 표), `Docs/README.md` (3단계 계획 행), 스펙 상태 줄

- [x] **Step 1: 플레이 스모크 (튜토리얼 프롬프트 + 캡처)**

```
Unity_ManageEditor(Action:"Play", WaitForCompletion:true)
```
RunCommand (플레이 중):
```csharp
using UnityEngine;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        DiceOrbit.UI.Tutorial.TutorialPromptUI.Show(null, null);
        ScreenCapture.CaptureScreenshot("D:/Dice Orbit/_workspace/2026-09-25-ui-reskin/review/phase3_prompt.png");
        result.Log("captured");
    }
}
```
1~2초 뒤(캡처는 프레임 끝에 기록) `Read`로 이미지 확인 → 크림 패널·핑크/하늘 버튼·잉크 글자면 OK. `Unity_ManageEditor(Action:"Stop")`. 콘솔 에러 0.

- [x] **Step 2: 사용자 육안 확인 요청** — 노드맵·상점·이벤트·보상·정보 패널은 런 흐름이 필요하므로 사용자가 에디터에서 한 바퀴 돌며 확인. 깨진 곳은 파일 단위 커밋이라 되돌리기 쉽다.

- [x] **Step 3: 문서**

`Docs/editor_owned_ui_pattern.md` 체크리스트 5번을:
```markdown
5. 씬에 배치하는 패널/칩/버튼은 `UiSkinImage` 컴포넌트 + 파트 지정 (`Assets/Scripts/UI/UiSkinImage.cs`). 절차 생성 라운드 사각형(`UiRoundedSprite`)은 2026-09-25 철거됨. 점검: 「도구/Dice Orbit/UI 스킨 점검」
```
`Docs/README.md` 설계안·구현 계획 표의 리스킨 스펙 행 상태를 `진행 중 (1~3단계 완료)`로, 아래에 행 추가:
```markdown
| [superpowers/plans/2026-09-25-ui-reskin-phase3-code-ui-migration.md](superpowers/plans/2026-09-25-ui-reskin-phase3-code-ui-migration.md) | 코드 UI 13파일 UiSkin 마이그레이션 + UiSkinImage **구현 계획** | 완료 |
```
스펙 상단 상태 줄을 `> 상태: **1~3단계 완료 (코드 UI 13파일·씬 라운드 참조 20개 전환, UiRoundedSprite·TutorialSkin 철거), 4단계(씬 드롭인) 대기**`로.

- [x] **Step 4: 커밋 + 보고**

```bash
cd "D:/Dice Orbit" && sed -i 's/^- \[ \] \*\*Step/- [x] **Step/' Docs/superpowers/plans/2026-09-25-ui-reskin-phase3-code-ui-migration.md && git add Docs && git commit -q -m "docs(ui-skin): 리스킨 3단계 완료 — 에디터 소유 UI 패턴·README·스펙 상태 갱신

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>" && git log --oneline -14
```
보고: 브랜치, 커밋 목록, 사용자 확인이 필요한 화면 목록, 4단계 계획 작성 여부.
