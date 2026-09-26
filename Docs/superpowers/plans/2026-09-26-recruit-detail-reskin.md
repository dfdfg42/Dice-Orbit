# 모집 상세 화면 + 배경 리스킨 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 모집 화면의 배경을 힉스필드로 만든 "밤의 마녀 연구실 선반"으로 바꾸고, 유리병을 고른 뒤 나오는 상세 화면을 어두운 띠에서 크림 종이 캐릭터 시트로 재구성한다.

**Architecture:** 배경은 새 스프라이트 파일 + `RecuritUI/BackGround` 참조 교체. 상세는 씬 `DetailRoot` 아래를 RunCommand로 재생성(Panel/Body 세로 흐름 + 버튼 + 일러스트)하고 `CharacterSelectionUI` 슬롯을 재배선한다. 코드는 그늘 제거·패널 이름·패시브 색만 손댄다. 스펙: `Docs/superpowers/specs/2026-09-26-recruit-detail-reskin-design.md`.

**Tech Stack:** Unity 6000.3.8f1, Unity MCP RunCommand(리플렉션·DeleteAsset 금지), 힉스필드 MCP(gpt_image_2_5), PIL, 자가 테스트 `UiSkinSelfTests.RunAll()`.

## Global Constraints

- 폴백 금지 / 구조 > 레거시 / 신규 스프라이트 Mipmap+Trilinear / 칩·버튼 배경 Image는 `ignoreLayout` 자식(체크리스트 7).
- 레이아웃 수치는 스펙 §2 그대로: Panel (40,−20) 980×620, Body 인셋 40/40/40/110, 이름 36·HP 28·섹션 칩 26·본문 22, 버튼 220×64 at (150,−255)/(400,−255), LDIllust (720,20) 560×748, SelectedBottleAnchor (−331,116).
- 캔버스: ScaleWithScreenSize 1920×1080, match 0.5.
- 씬 편집 전 `isPlaying == false`, `GetActiveScene().isDirty` 확인. 커밋 제외 파일 규칙 동일.
- 힉스필드 예산 3.25(초안 0.5 + 최종 2.75). 배치 2건 이하.

---

### Task 1: 배경 생성 + 임포트 + 캔버스 스케일러

**Files:**
- Create: `Assets/Sprites/캐릭터 선택화면/recruit_bg.png`
- Modify (RunCommand): `Assets/Scenes/BattleScene.unity` — `RecuritUI` CanvasScaler, `BackGround` sprite

- [x] **Step 1: 참조 업로드** — `Assets/Sprites/상점/상점 임시배경.png`를 `media_upload` → curl PUT → `media_confirm`. media_id를 `REF_SHOP_BG`로 기록.
- [x] **Step 2: 초안 2장 (low·1k·16:9)** — 사용자 승인 체크포인트.

프롬프트:
```
Background illustration for a character recruitment screen in a cute pastel cartoon roguelike: the inside of a witch's laboratory at night, the same room and palette as the reference shop background (warm candle light, cream, lavender, sky blue, pink accents, thick dark ink outlines, flat pastel colors). Composition: a long empty wooden shelf runs across the middle third of the image where three large potion bottles will be placed later — keep that shelf and the space above it completely empty and uncluttered. Above and below the shelf: jars, books, hanging herbs, a round window with the night sky and stars, soft glowing candles. No text, no characters, no UI elements, no bottles on the shelf.
```
`generate_image(model gpt_image_2_5, aspect_ratio 16:9, count 2, quality low, resolution 1k, background opaque, medias [REF_SHOP_BG, REF_STYLE])`. 승인 변형을 고른다.
- [x] **Step 3: 최종 (high·2k·16:9·opaque)** — 승인 변형 job을 참조에 추가. 다운로드 → `candidates/recruit_bg_raw.png` → `final/recruit_bg.png`(그대로) → `Assets/Sprites/캐릭터 선택화면/recruit_bg.png` 복사.
- [x] **Step 4: 임포트 + 배경 교체 + 스케일러 (RunCommand)**

```csharp
using UnityEngine; using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine.UI;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult r)
    {
        if (EditorApplication.isPlaying) { r.LogError("플레이 모드"); return; }
        AssetDatabase.Refresh();
        const string path = "Assets/Sprites/캐릭터 선택화면/recruit_bg.png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { r.LogError("importer 없음"); return; }
        imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = true; imp.filterMode = FilterMode.Trilinear; imp.maxTextureSize = 2048;
        imp.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        var scene = EditorSceneManager.GetActiveScene();
        var ui = Object.FindFirstObjectByType<DiceOrbit.UI.CharacterSelectionUI>(FindObjectsInactive.Include);
        var canvas = ui.GetComponentInParent<Canvas>(true);
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var bg = canvas.transform.Find("BackGround").GetComponent<Image>();
        bg.sprite = sprite; bg.type = Image.Type.Simple; bg.preserveAspect = false;
        EditorUtility.SetDirty(scaler); EditorUtility.SetDirty(bg);
        EditorSceneManager.MarkSceneDirty(scene);
        r.Log($"bg={bg.sprite.name} {sprite.rect.size} scaler={scaler.uiScaleMode} saved={EditorSceneManager.SaveScene(scene)}");
    }
}
```
- [x] **Step 5: 커밋** — png(+meta) + 씬.

### Task 2: CharacterSelectionUI 코드 정리

**Files:** Modify `Assets/Scripts/UI/CharacterSelectionUI.cs`

- [x] **Step 1**: `Start()`의 `_leftPanel = detailRoot.transform.Find("LeftDescriptionPanel")` → `Find("Panel")`, `EnsureTopShade()` 호출·메서드·`_topShade`·`topShadeStrength`·`[Header("Shade …")]` 삭제. 슬롯 검사 추가:
```csharp
            if (detailRoot == null || _leftPanel == null || detailNameText == null || detailActiveText == null || detailPassiveText == null || ldConfirmButton == null || cancelButton == null)
                Debug.LogError("[CharacterSelectionUI] 상세 슬롯이 비어 있습니다 — 씬 RecuritUI/DetailRoot/Panel 아래를 배선하세요.", this);
```
- [x] **Step 2**: `BuildPassiveSummary`의 `sb.Append("<b>[").Append(name).Append("]</b>")` → `sb.Append("<b><color=#").Append(ColorUtility.ToHtmlStringRGB(DiceOrbit.UI.Skin.UiSkin.Current.Passive)).Append(">").Append(name).Append("</color></b>")`.
- [x] **Step 3**: Refresh → 콘솔 에러 0 → 커밋.

### Task 3: DetailRoot 재구성 (RunCommand) + 슬롯 재배선

**Files:** Modify (RunCommand) `Assets/Scenes/BattleScene.unity`

- [x] **Step 1**: RunCommand — 폰트 로드(`Assets/Resources/Fonts/KOTRA HOPE SDF.asset`, `Assets/TextMesh Pro/Fonts/에이투지체-4Regular SDF.asset`), `DetailRoot` 자식 전부 삭제(옛 LeftDescriptionPanel·라벨 이미지·버튼·LDIllust), `DetailRoot.anchoredPosition=(0,0)`, 스펙 §2 구조 생성(정보 패널 재구성 명령과 같은 헬퍼: NewRect/VLayout/HLayout/NewChip(Bg ignoreLayout)/NewTMP), 버튼 = Image + Button + UiSkinImage(ButtonPrimary/Secondary) + 라벨 TMP, LDIllust = Image(preserveAspect, 기존 `alche window.png`를 기본), `SelectedBottleAnchor.anchoredPosition=(−331,116)`. `SerializedObject(CharacterSelectionUI)`로 detailNameText/detailStatsText/detailPassiveText/detailActiveText/detailDescriptionText(null)/cancelButton/ldIllustrationImage/ldConfirmButton 배선. 저장.
- [x] **Step 2**: `dump_subtree.py … RecuritUI`로 구조 확인, 콘솔 에러 0.

### Task 4: Play 검증 + 문서 + 커밋

- [x] **Step 1**: Play → 2초 후 `recruit_01_bg.png` 캡처 → 첫 `CharacterCard`의 Button `onClick.Invoke()` → 1.2초 후 `recruit_02_detail.png` → stop. `Read`로 확인: 배경·크림 시트·병·일러스트·버튼 위치, 콘솔 에러 0.
- [x] **Step 2**: 전후 시트(`sel_01`/`sel_02` vs 새 캡처) → `SendUserFile`.
- [x] **Step 3**: 문서 — 스펙 상태 `구현 완료`, README 행(스펙·계획), 생성 로그 행(+`REF_SHOP_BG`), 메모리. 커밋(씬+코드, 문서 각각).
