# 세이브 시스템 리팩토링 — 세션 인계 문서

> **작성 시점:** 2026-07-29 / **브랜치:** `refactor/save-system-20260727`
> **읽는 대상:** 이 작업을 이어받는 다음 에이전트 세션
>
> 이 문서는 "그때의 상태" 기록물이다. 코드와 어긋나면 **코드가 정답**이다.
> 진행 상황은 항상 `git log`와 아래 원장 파일로 먼저 확인할 것.

---

## 0. 30초 요약

`RunSaveService`와 `GameFlowManager`로 갈라진 저장/복원을 **매니저 자가 저장**
(`IRunSaveParticipant`)으로 통합하고, 에셋 참조를 이름 문자열에서 불변 `saveId`로 바꾸는 작업이다.

**14개 태스크 중 0~3번 완료.** 지금까지는 전부 *순수 추가*(필드·신규 파일)라 기존 동작을 건드리지
않았다. 태스크 4부터 계약이 들어가고, **태스크 7이 파괴적 변경의 시작점**이다.

작업 방식은 `superpowers:subagent-driven-development` — 태스크마다 새 구현자 에이전트를 띄우고,
끝나면 별도 리뷰어를 띄우고, 지적이 나오면 수정 루프를 돈다.

---

## 1. 먼저 읽을 것

| 문서 | 역할 |
|---|---|
| [2026-07-27-save-system-refactor-design.md](../specs/2026-07-27-save-system-refactor-design.md) | **설계 원본.** 계획과 어긋나면 이쪽이 정답 |
| [2026-07-28-save-system-refactor.md](2026-07-28-save-system-refactor.md) | **구현 계획.** 14개 태스크의 전문 |
| `.superpowers/sdd/2026-07-28-save-system-refactor/progress.md` | **원장.** 태스크별 커밋·리뷰 지적·이연 항목 |

원장이 있는 `.superpowers/`는 **git-ignore 대상**이라 `git clean -fdx`로 날아간다.
날아갔다면 `git log`와 이 문서로 복구할 것.

같은 폴더에 `task-N-brief.md`(에이전트에게 준 요구사항), `task-N-report.md`(에이전트 작업 기록),
`review-<a>..<b>.diff`(리뷰 패키지)가 쌓여 있다.

---

## 2. 실행 환경 — 확인된 사실

CLAUDE.md의 내용을 실측으로 검증한 결과다. **다시 조사하지 말 것.**

- **Unity 에디터는 이 VM에서 실행 불가.** 리눅스 에디터 바이너리는 x86-64(ELF `e_machine` 0x3e)이고,
  Unity 6000.3.8f1의 ARM64 리눅스 빌드는 배포되지 않는다(다운로드 엔드포인트 404).
  Unity 공식 문서에는 CPU 아키텍처 언급이 아예 없어 문서로는 판정할 수 없다.
- **남은 선택지는 에뮬레이션뿐**(qemu-user-static / box64). sudo·라이선스 활성화·10~20배 속도 저하가
  걸려 2026-07-29 시점 판단은 "시도하지 않음"이다.
- **참조 어셈블리는 `~/unity-refs`에 이미 구축돼 있다.** 매니지드 DLL은 순수 IL이라 ARM64에서도
  참조된다. `Tools/setup-compile-refs.sh`를 **다시 실행하지 말 것** — 4.5GB를 내려받는다.

---

## 3. 검증 수단 — `Tools/compile-check.sh`

**이것이 유일한 자동 게이트다.** 인자 없이 실행, 1초 미만.

```bash
./Tools/compile-check.sh    # 성공: "COMPILE OK (에디터 맥락 + 플레이어 빌드)" + exit 0
```

Unity의 실제 컴파일 맥락 **둘**을 각각 재현한다.

| 패스 | 대상 | `UNITY_EDITOR` | 참조 |
|---|---|---|---|
| 1. 에디터 맥락 | `Assembly-CSharp` + `Assembly-CSharp-Editor` | **켬** | `UnityEngine.*` + `UnityEditor.*` |
| 2. 플레이어 빌드 | `Assembly-CSharp`만 | **끔** | `UnityEngine.*`만 |

### 잡는 것

문법 오류, 타입 불일치, **중복 클래스 정의(CS0101)**, **모호한 참조(CS0104)**,
어셈블리 경계를 넘는 `internal` 접근, `#if UNITY_EDITOR` 밖으로 새어나온 에디터 API 사용(CS0234),
`unsafe` 코드(CS0227 — `ProjectSettings.asset`이 `allowUnsafeCode: 0`이므로).

### 잡지 못하는 것

씬·프리팹의 직렬화 참조, `.meta` GUID 정합성, `OnValidate`/`AssetPostprocessor` 수명주기,
런타임 로직. **컴파일 통과를 "동작한다"고 보고하지 말 것.**

잔여 한계: 선빌드된 패키지 DLL(InputSystem/TMP/uGUI)이 `UNITY_EDITOR`를 켠 채 빌드돼 있어,
패키지의 에디터 전용 멤버는 패스 2에서도 보인다. 현재 코드에 그런 사용은 없다.

### 자동화 테스트는 없고, 만들지 않기로 사용자가 결정했다

NUnit 프로젝트가 없고 세이브 코드는 `persistentDataPath`·SO·싱글톤에 묶여 있다.
**테스트 부재를 이유로 작업을 멈추거나 사용자에게 결정을 미루지 말 것.** 리뷰어에게도
"테스트 부재 자체를 결함으로 올리지 말라"고 명시해 왔다.

---

## 4. 완료된 태스크

브랜치 시작점(merge-base): `c7a05ad`

| 커밋 | 태스크 | 리뷰 |
|---|---|---|
| `9e6ab3c` | 0 — 컴파일 검증 환경 | Critical 발견 → `7ce2236`으로 수정 |
| `78e0f25` | 1 — `saveId` 필드 3종 | ✅ 승인 |
| `10bef12` | 2 — `SaveIdCatalog` + 자동 재스캔 | ✅ 승인 (Minor 1건 이연) |
| `7ce2236` | 0 수정 — 하네스 재설계 | ✅ A~F 전부 ADDRESSED |
| `6bef83b` | 3 — v2 DTO | ⚠️ **리뷰 미실시** |

**태스크 3 리뷰가 남아 있다.** 이어받으면 이것부터 처리할 것.
리뷰 패키지: `review-package <계획파일> 7ce2236 6bef83b`

현재 `COMPILE OK` (런타임 171개 + 에디터 5개), 작업트리 깨끗.

---

## 5. 확정된 계약 — 이후 태스크가 여기에 의존한다

```
// 태스크 1
ArtifactData.SaveId        string, 읽기 전용 프로퍼티 (백킹 필드 saveId)
Potion.SaveId              string, 읽기 전용    ← 클래스명은 Potion. 계획 일부의 "PotionData"는 오류
CharacterPreset.SaveId     string, 읽기 전용
   Potion.OnValidate는 protected virtual. 나머지 둘은 private.

// 태스크 2 — 네임스페이스 DiceOrbit.Core.Run.Save
SaveIdCatalog.Get()                  없으면 null
SaveIdCatalog.FindArtifact(string)   실패 시 조용한 null (표시 이름 폴백 없음)
SaveIdCatalog.FindPotion(string)
SaveIdCatalog.FindPreset(string)
SaveIdCatalog.ResourcePath            const
SaveIdCatalog.EditorSetContents(...)  public + #if UNITY_EDITOR
SaveIdCatalog.EditorContentsEqual(...) public + #if UNITY_EDITOR
   → public인 이유: Assembly-CSharp-Editor는 별도 어셈블리라 internal에 접근 못 한다
DiceOrbit.EditorTools.SaveIdValidator.Rescan()   메뉴 「도구/Dice Orbit/세이브 ID 전체 점검」
DiceOrbit.EditorTools.SaveIdCatalogPostprocessor .asset 임포트 시 자동 Rescan

// 태스크 3 — 파일 Assets/Scripts/Core/Run/Save/RunSaveData.cs
RunSaveData        int Version=2 / RunProgressSave Progress / int Gold
                   List<ArtifactSaveData> Artifacts / List<PotionSaveData> Potions
                   List<CharacterSaveData> Party
RunProgressSave    int Seed / int CurrentNodeId=-1 / List<int> VisitedNodeIds
                   int BattlesCleared / List<string> BanishedPresetIds
ArtifactSaveData   string Id          (↔ ArtifactData.SaveId)
PotionSaveData     string Id          (↔ Potion.SaveId)
ModifierSaveData   string Id          (모디파이어 클래스 타입명)
CharacterSaveData  string PresetId    (↔ CharacterPreset.SaveId)
                   int CurrentHp / int MaxHp / int RevivalStock
                   List<ModifierSaveData> Modifiers
```

---

## 6. 전역 제약 — 모든 태스크에 암묵 적용

- **C# 9** (`netstandard2.1`). C# 10+ 문법(파일 스코프 네임스페이스, `record`, 전역 using) 금지.
- **세이브 포맷 버전 `2`.** 마이그레이션 코드·v1 별칭 테이블을 만들지 않는다. `Version != 2`는 폐기.
- **`saveId` 폴백 금지.** 빈 `saveId`를 표시 이름으로 대체하지 않는다. 복원 실패로 취급한다.
- **`.meta` 동봉.** `Assets/` 아래 파일을 추가·삭제·이동하면 `.meta`도 함께 커밋한다.
  새 폴더에는 폴더용 `.meta`(`folderAsset: yes` + `DefaultImporter`)가, `.cs`에는 `MonoImporter`가 필요하다.
  GUID는 32자리 소문자 16진수이며 `Assets/` 전체와 중복되면 안 된다.
- **손으로 편집 금지:** `.unity`, `.prefab`, `.asset`. 필요하면 §8 핸드오프 목록에 적는다.
- **JsonUtility 제약:** public 필드 또는 `[SerializeField]` private 필드만 직렬화된다.
  `Dictionary`·인터페이스·다형성 컬렉션을 쓰지 않는다.
- **네임스페이스:** 신규 세이브 코드는 `DiceOrbit.Core.Run.Save`.
- **브랜치:** `refactor/save-system-20260727`. `main`에 직접 커밋하지 않는다.
- **커밋:** 정수환 `<aibf0815@gmail.com>`, 메시지는 한국어.

---

## 7. 이연한 지적 — 최종 리뷰에서 재검토할 것

- **Minor** `SaveIdCatalogPostprocessor`가 타입 무관하게 모든 `.asset` 변경에 반응해 풀스캔.
  브리프 원안 그대로이며 현재 `.asset` 81개 규모에선 무해. 확장 시 타입 필터 고려.
- **Minor** 태스크 1 구현자 리포트가 "`Potion` 서브클래스는 `HealPotion` 하나"라고 했으나
  `PotionManager.cs:162`에 `private class RuntimePotion : Potion`이 더 있다. 자체 `OnValidate`가
  없어 `protected virtual` 설계에 문제는 없고, `CreateDefault` 호출부는 전부 주석 처리된 죽은
  코드다. **태스크 7·9에서 이 죽은 코드를 확인할 것.**
- 계획 문서의 File Structure가 `PotionData`라고 적은 곳이 있다. 실제 클래스명은 `Potion`.

---

## 8. 사용자가 Windows Unity에서 해야 할 일 (누적)

1. `Assets/Resources/`에 `SaveIdCatalog.asset` 생성 (`Create > DiceOrbit > SaveIdCatalog`).
   **경로가 정확해야** `Resources.Load`가 찾는다.
2. 메뉴 `도구 > Dice Orbit > 세이브 ID 전체 점검` 실행 (최초 스캔)
3. 기존 에셋들의 `saveId`가 자동으로 채워지는지, 중복 에러가 없는지 콘솔 확인
4. `AssetPostprocessor`가 무한 루프 없이 도는지 (정적 분석으로 확정 불가)

**태스크 7 이후 추가될 것:** 유물 에셋 5종 저작. 스캐폴딩을 지우면 획득 후보가 0개가 된다.

---

## 9. 다음 할 일

1. **태스크 3 리뷰** (미실시)
2. 태스크 4 — 참가자 계약 (`RestoreReport` · `RunRestoreContext` · `IRunSaveParticipant`)
3. 태스크 5~6 — `RunSaveFile`, `ModifierRegistry` ID 조회
4. **태스크 7 — 분기점.** 디버그 스캐폴딩 삭제 + `ClearAll()`.
   여기부터 파괴적 변경이다. 유물 획득 후보가 0개가 되므로, **진입 전에 사용자에게 알릴 것.**
5. 태스크 8~10 — 매니저 5종 참가자 전환
6. 태스크 11~13 — 서비스, 배선 교체, 문서
7. 최종 전체 브랜치 리뷰

태스크 12 주의: `using` 추가를 구 파일 삭제보다 **먼저** 하면 `RunSaveService`가 두 네임스페이스에
존재해 `CS0104`가 난다. 계획에 순서가 명시돼 있다.

---

## 10. 이 세션에서 얻은 교훈 — 반복하지 말 것

**검증 도구를 먼저 의심하라.** `compile-check.sh`에 결함이 둘 있었고 둘 다 실측으로 드러났다.
(a) 소스 목록이 비면 Roslyn이 `CS2008` 경고만 내고 exit 0 → **아무것도 컴파일하지 않고 통과**.
(b) 런타임을 `UNITY_EDITOR` 없이 컴파일한 뒤 에디터 어셈블리를 링크하는, 실제로 없는 조합.
(b) 때문에 태스크 2 구현자가 **도구 대신 제품 코드를 고쳤다**(`#if UNITY_EDITOR` 가드 제거).
지금은 둘 다 고쳐졌지만, 구현자가 "브리프 코드가 컴파일이 안 된다"고 하면 **도구를 먼저 볼 것.**

**작업트리를 임시 수정하는 리뷰어와 구현자를 동시에 돌리지 말 것.** 태스크 3 구현 중 재리뷰어가
`MainMenuUI.cs`를 잠시 변경해 혼선이 있었다(결과 영향은 없었다).

**에이전트 보고를 그대로 믿지 말 것.** 태스크 1 구현자의 서브클래스 조사가 불완전했고,
리뷰어가 잡았다. 중요한 주장은 컨트롤러가 직접 확인했다.

**계획에 적혀 있다고 옳은 것은 아니다.** 태스크 0의 스크립트는 계획 문서에 전문이 박혀 있었고
구현자는 그대로 옮겼지만, 계획 자체가 틀렸다.
