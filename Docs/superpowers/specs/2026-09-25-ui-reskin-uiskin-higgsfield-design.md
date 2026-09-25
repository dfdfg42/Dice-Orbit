# UI 통일 리스킨 — 크림 종이 + 굵은 외곽선, UiSkin 시스템, 힉스필드 생성 (2026-09-25)

> 상태: **1~4단계 완료** (코어 세트 17장 + Circle, UiSkin·UiSkinImage, 코드 UI 13파일 전환, 씬 라운드 참조 20개 전환, 드롭인 20장, 메인메뉴·HUD 버튼 8개 SpriteSwap) — **5단계(사용자 플레이 확인·WebGL 재빌드) 대기**. 2026-09-25, 브랜치 `feature/ui-skin-dropin-20260925`(← migration ← core ← main), 누적 33.75크레딧. 4단계 실제 방식은 `plans/2026-09-25-ui-reskin-phase4-scene-dropin.md` 매핑표 참고 (§4.2와 다른 점: 턴 배너·타일 그림·모집 설명 패널 제외, HP바·주사위 면·하단 바는 PIL, 글자 버튼은 폰트 합성).
> 배경: 화면마다 패널 언어가 세 갈래(메인메뉴 = 반투명 유리 알약 / 전투 HUD = 크림 종이 플레이스홀더 / 런 화면 = 코드가 그리는 남색 펠트)로 갈라져 있다. 사용자 결정 = 전부 갈아엎되 **현재 아트 방향(파스텔 밤하늘 + 굵은 외곽선 카툰)은 유지**하고 하나로 통일한다.
> 관련: `editor_owned_ui_pattern.md`(코드 생성 UI 규약), `battle_info_panel_system.md`(크림 팔레트 원조), 메모리 `project_sprite_import`(Mipmap+Trilinear)

## 0. 결정 요약

| 질문 | 결정 |
|---|---|
| 대상 화면 | 전부 |
| 아트 방향 | 현재 방향 유지·통일 (타이틀 배경·노드 아이콘이 기준) |
| 패널 기조 | **크림 종이 + 굵은 잉크 외곽선** (보드게임 카드 감각) |
| 통합 방식 | **UiSkin ScriptableObject** — 코드 UI는 스킨을 읽고, 씬 스프라이트는 같은 파일명 드롭인 |
| 버튼 상태 | **상태별 스프라이트** (normal/hover/pressed/disabled) — 틴트 아님 |
| 생성 도구 | 힉스필드 MCP, 모델 `gpt_image_2_5` (투명 배경 + 참조 이미지) |

## 1. 스타일 가이드 — 모든 생성물의 기준

- **패널**: 크림 종이 `#FAF3E0` 바탕, 굵은 잉크 외곽선(`#4B425C` 계열, 패널 크기 대비 2~3%), 둥근 모서리, 아래쪽에 한 톤 어두운 두께선("종이 카드가 살짝 떠 있다"). 노드 아이콘의 두꺼운 외곽선 문법을 그대로 계승한다.
- **버튼**: 알약 형태. Primary = 파스텔 핑크(현재 주사위 버튼 색 계승), Secondary = 파스텔 하늘. 상태 4종은 형태로 구분한다. hover = 살짝 밝고 외곽선 두꺼워짐, pressed = 아래 두께선이 사라지며 내려앉음, disabled = 채도 뺀 회색 종이 + 점선 외곽선.
- **칩·섹션 제목**: 한 톤 진한 크림 `#E8DBC3`, 얇은 외곽선.
- **의미색 유지**: HP 적색 `#C53A3A`, 주사위 청색, 모디파이어 보라 `#6A48B8`, 패시브 세이지 `#839F8E`, 골드 포인트. `InfoPanelRows`가 이미 쓰는 값이라 스킨 팔레트로 그대로 옮긴다.
- **장식 최소**: 별·유리병 모티브는 패널 헤더 모서리와 구분선에만. 본문 영역은 비운다.
- **글꼴**: Pretendard SDF / KOTRA HOPE 그대로. **글자를 이미지에 굽지 않는다.** 예외는 §4.2의 메인메뉴 4종·선택/취소 2종으로, 씬 재배선을 피하기 위한 드롭인 유지다 (후속 과제로 TMP 분리 가능).

## 2. 범위 · 비목표

**바꾸는 것**
- 패널·카드·칩·버튼·툴팁·구분선·슬롯 스프라이트 전체 (코어 세트)
- HUD 아이콘: 코인, 빈 포션 슬롯, 닫기
- 전투 HUD 씬 스프라이트 12종, 메인메뉴 버튼 4종, 캐릭터 선택의 선택/취소 버튼, 상점 임시배경

**안 바꾸는 것**
- 캐릭터·몬스터·마녀 아트, 노드 아이콘 6종, 타이틀 로고, 타이틀/캐릭터 선택 배경, VFX, 튜토리얼 스킨 에셋의 레이아웃
- **레이아웃**: 위치·크기·앵커는 그대로. 껍데기만 교체한다. 레이아웃 재배치는 별도 설계.

## 3. UiSkin 아키텍처

### 3.1 에셋과 접근
- 코드: `Assets/Scripts/UI/Skin/UiSkin.cs` (`[CreateAssetMenu(menuName = "DiceOrbit/UI Skin")]`)
- 에셋: `Assets/Resources/UI/UiSkin.asset` — `TooltipKeywordDatabase`와 같은 `Resources.Load` 관행
- 접근: `UiSkin.Current` (1회 로드 캐시). **에셋이 없으면 `InvalidOperationException`.** 기본색으로 조용히 굴러가지 않는다 (폴백 금지 원칙).

### 3.2 필드
```csharp
[Header("팔레트")]
public Color Paper, PaperDeep, Ink, InkMuted, Accent, Primary, Secondary, Danger, Dice, Modifier, Passive, Scrim;

[Header("9-slice 스프라이트")]
public Sprite Panel;      // 모달·무대 패널 (상점/이벤트/보상/튜토리얼/결과)
public Sprite Card;       // 내용 카드 (보상 행, 상점 상품, 정보 패널 섹션, 툴팁 카드)
public Sprite Chip;       // HUD 칩, 섹션 제목 칩, 상태 아이콘 배경
public Sprite Tooltip;    // 말풍선 (주사위 툴팁, 커서 툴팁)
public Sprite Slot;       // 빈 슬롯 (포션 3칸, 주사위 덱 칸)
public Sprite Divider;    // 가로 구분선

[Header("버튼 (상태별 스프라이트)")]
public ButtonSpriteSet ButtonPrimary, ButtonSecondary;   // { Normal, Hover, Pressed, Disabled }

[Header("아이콘")]
public Sprite Coin, PotionSlotEmpty, Close;
```

### 3.3 적용 헬퍼
`ApplyPanel(Image)`, `ApplyCard(Image)`, `ApplyChip(Image)`, `ApplyTooltip(Image)`, `ApplySlot(Image)`, `ApplyDivider(Image)`, `ApplyButton(Button, ButtonKind)`.
- Image: sprite 지정 + `Image.Type.Sliced` + `color = Color.white`. 틴트로 색을 바꾸지 않는다. 스프라이트가 색을 가진다.
- Button: `transition = SpriteSwap`, `spriteState = {highlighted, pressed, disabled}`, `targetGraphic.sprite = Normal`.
- 헬퍼는 스킨 필드만 읽고 GameObject를 생성하지 않는다 (레이아웃 책임은 각 UI에 남긴다).

### 3.4 점검 메뉴
`Editor/UiSkinValidator.cs` — 「도구/Dice Orbit/UI 스킨 점검」. 빈 필드, 9-slice 경계가 0인 스프라이트(Sliced 대상만), 텍스처 임포트가 Sprite가 아닌 경우를 콘솔 에러로 나열한다. `SaveIdValidator`와 같은 메뉴 그룹.

### 3.5 마이그레이션 대상 (13파일)
| 파일 | 현재 | 전환 |
|---|---|---|
| `NodeMapUI.cs` | Felt/Card/CardEdge/Ink/Gold 상수 + 라운드 2 | 상수 삭제, HUD 칩 → Chip, 배경 → Scrim |
| `ShopUI.cs` | 상수 8 + 라운드 2 | 무대 패널 → Panel, 상품 → Card, 구매 → ButtonPrimary |
| `EventUI.cs` | 상수 7 + 라운드 2 | Panel / 선택지 → ButtonSecondary |
| `RewardUI.cs` | 상수 3 + 라운드 1 | Panel / 보상 행 → Card / 받기 → ButtonPrimary |
| `RunHudUI.cs` | 상수 7 + 라운드 1 + `Resources.Load("UI/코인")` | Chip / Slot / Coin 아이콘은 스킨에서 |
| `InfoPanel/TileInfoPanelUI.cs` | 라운드 2 | Card |
| `InfoPanel/InfoPanelRows.cs` | 크림 팔레트 10 상수 (원조) | 팔레트를 스킨으로 이관. 상수를 스킨 프록시로 남기지 않고 호출처가 직접 스킨을 읽는다 |
| `Tutorial/TutorialOverlayUI.cs` | 상수 6 | Panel / ButtonPrimary |
| `Tutorial/TutorialPromptUI.cs` | 상수 7 | Panel / ButtonPrimary·Secondary |
| `DiceHoverTooltipUI.cs` | 상수 3 + 라운드 1 | Tooltip / 효과 카드 → Card |
| `DiceElement.cs` | 라운드 1 | Chip (배지) |
| `StatusIconRow.cs` | 라운드 1 | Chip (아이콘 배경) |
| `GameResultUI.cs` | 상수 2 | Panel / ButtonPrimary |

### 3.6 씬에 박제된 라운드 참조 (조사 결과, 2026-09-25)
코드 호출처 외에 씬이 직접 잡고 있는 참조가 두 종류 있다. 둘 다 3단계에서 정리한다.

- **`UiRoundedImage` 컴포넌트 7개** (BattleScene의 RewardCanvas: MainPanel, UpgradePanel, Paper×2, GoldBar, 계속Button, 취소Button). 삭제하면 missing script가 되므로 **삭제 대신 `UiSkinImage`로 개명**한다. `.meta` GUID를 유지한 채 파일·클래스명을 바꾸고(`git mv`), `radius` 필드를 `SkinPart part` enum(Panel/Card/Chip/Tooltip/Slot/Divider/ButtonPrimary/ButtonSecondary)으로 교체한다. `Awake`와 `OnValidate`에서 스킨 스프라이트를 꽂고, 버튼 파트면 같은 오브젝트의 `Button`에 SpriteSwap 상태까지 세팅한다. 7개의 파트 지정: MainPanel·UpgradePanel → Panel, Paper → Card, GoldBar → Chip, 계속Button → ButtonPrimary, 취소Button → ButtonSecondary.
- **`Assets/Art/Generated/RoundedRect_*.asset` 스프라이트 참조 13개** (씬+프리팹의 Image가 직접 참조). 각 Image에 `UiSkinImage`를 붙여 파트를 지정한 뒤, `Art/Generated` 폴더를 통째로 삭제한다. 삭제 전 참조 수가 0인지 GUID grep으로 확인한다.

전환 완료 후 `UiRoundedSprite.cs`는 호출처 0이 되므로 **삭제**한다. `editor_owned_ui_pattern.md` 체크리스트 4·5번(팔레트 상수 계승·`UiRoundedSprite.Get`)을 UiSkin·`UiSkinImage`로 갱신한다.

## 4. 에셋 인벤토리

### 4.1 코어 세트 (스킨이 소유, `Assets/Sprites/UI Skin/`)
| 파일 | 생성 단위 | 9-slice | 비고 |
|---|---|---|---|
| `panel.png` | 1장 | O | 512×512 생성 → 경계 측정 후 .meta 기입 |
| `card.png` | 1장 | O | |
| `chip.png` | 1장 | O | |
| `tooltip.png` | 1장 | O | 꼬리 없음 (꼬리는 코드가 위치를 못 맞춤) |
| `slot.png` | 1장 | O | 안쪽 살짝 파인 형태 |
| `divider.png` | 1장 | 가로만 | |
| `button_primary_{normal,hover,pressed,disabled}.png` | **스트립 1장**(4상태 가로) → 로컬 분할 | O | 한 생성에서 나와야 상태 간 형태가 맞는다 |
| `button_secondary_{normal,hover,pressed,disabled}.png` | 스트립 1장 → 분할 | O | |
| `icon_coin.png`, `icon_potion_empty.png`, `icon_close.png` | 각 1장 | X | 256×256 |

생성 11회. 분할·트리밍은 파이썬(PIL 없으면 표준 라이브러리 PNG 처리 스크립트)으로 스크래치에서 수행한다.

### 4.2 씬 드롭인 (같은 파일명 덮어쓰기 → GUID 유지, 재배선 없음)
| 파일 | 크기 | 처리 |
|---|---|---|
| `UI 이미지/주사위 패널.png` | 1000×175 (9-slice) | 코어 Panel을 이 크기로 렌더 (추가 생성 없음) |
| `UI 이미지/오른쪽 패널.png` | 410×1040 (9-slice) | 동일 |
| `UI 이미지/패널 1차.png` | 1007×482 (9-slice) | 동일 |
| `UI 이미지/Rectangle 16.png` | 632×149 | 씬에서 용도 확인 후 Chip 또는 Card 렌더 |
| `UI 이미지/파티 로스터 패널.png` | 140×140 | 정사각 초상 프레임. 생성 1 |
| `UI 이미지/dice button.png` | 731×213 | ButtonPrimary normal 렌더 |
| `Info UI 이미지/체력바.png`, `체력_피.png`, `hp.png` | 153×18 / 149×10 / 255×23 | HP 트랙·채움·라벨. 생성 1 (세트) |
| `Info UI 이미지/플레이어턴_푸른색.png`, `몬스터턴_붉은색.png` | 1280×720 | 턴 배너. 생성 2 |
| `Sprites/Dice.png` | 266×266 | 주사위 눈 면. 생성 1 |
| `UI 이미지/Start Game.png` 등 메인메뉴 4종 | 394×67 등 | 글자 포함 생성 4 (영문, 모델 텍스트 렌더 사용) |
| `UI 이미지/선택.png`, `취소.png` | 268×102 | 한글 포함. ButtonPrimary/Secondary 렌더 + Pretendard로 파이썬 합성 (모델 한글 렌더 비신뢰) |
| `상점/상점 임시배경.png` | 1536×1024 | 마녀 실험실 선반 배경. 생성 1 |
| `Sprites/Rectangle 41.png`, `new act 2/3.png`, `new cardNormal.png` | 각각 다름 | 씬에서 용도 확인 후 결정 (4단계 시작 시 조사) |

드롭인 추가 생성 약 10회. 임포트 설정: Sprite(2D and UI), Mesh Type Full Rect(Sliced용), **Mipmap + Trilinear**.

## 5. 힉스필드 생성 워크플로

- **모델**: `gpt_image_2_5`. 탐색 = `quality: low, resolution: 1k` (0.25크레딧). 최종 = `quality: high, resolution: 2k, background: transparent` (2.75크레딧).
- **참조 이미지(항상 첨부, role `image_references`)**: `노드/node_battle.png`(외곽선 문법), 승인된 스타일 타일(1단계 산출). 필요 시 `메인화면/main menu/titleScreenBG.png`(팔레트).
- **프롬프트 템플릿**: "2D 게임 UI 부품 한 개, 정중앙, 글자 없음, 투명 배경, 크림 종이 채움, 굵은 잉크색 외곽선, 둥근 모서리, 아래 두께선, 파스텔 카툰, 참조 이미지와 같은 선 굵기" + 부품별 한 줄.
- **후처리**: 결과 다운로드 → 스크래치 → 트리밍/분할/리사이즈 → 투명도 실패 시 `remove_background` → 프로젝트 경로로 복사 → .meta 갱신.
- **승인 규칙**: 사용자가 승인한 결과만 `Assets/`에 들어간다. 후보는 스크래치에 보관. 크레딧 소진 30에서 중간 보고. 총 상한 60.
- **재현성**: 생성마다 프롬프트·파라미터·job id를 `Docs/superpowers/plans/2026-09-25-ui-reskin-generation-log.md`에 기록한다 (재생성·추가 부품 때 같은 조건 사용).

## 6. 단계

| 단계 | 내용 | 산출 | 검증 |
|---|---|---|---|
| 1 스타일 타일 | 코어 부품을 한 장에 담은 시트를 저화질로 2~3장 → 사용자 선택 | 승인 시트 1장 (이후 참조 이미지) | 사용자 승인 |
| 2 코어 세트 + UiSkin | 11회 최종 생성, 분할·9-slice, `UiSkin.cs`/에셋/점검 메뉴 | 스킨 에셋 완성 | 점검 메뉴 에러 0, 콘솔 에러 0 |
| 3 코드 UI 마이그레이션 | 13파일 전환, 헬퍼·상수 삭제, 문서 갱신 | 런 화면·정보 패널·튜토리얼이 스킨으로 그려짐 | 콘솔 에러 0 + 각 화면 캡처 |
| 4 씬 드롭인 | §4.2 실행 + 용도 미확인 4종 조사 | 전투 HUD·메인메뉴·선택·상점 교체 | 콘솔 에러 0 + 캡처 |
| 5 마무리 | 생성 로그 정리, 스크래치 정리. WebGL 재빌드는 사용자 | 없음 | 사용자 플레이 확인 |

각 단계는 별도 브랜치·PR. 3단계는 파일 단위로 커밋해 되돌리기 쉽게 한다.

## 7. 리스크와 대응

- **스타일 드리프트**: 생성마다 형태·선 굵기가 흔들린다 → 승인 시트를 참조로 고정 + 버튼은 스트립 1장 생성.
- **한글 텍스트 렌더**: 선택/취소는 모델에 맡기지 않고 파이썬 합성.
- **9-slice 경계 오판**: 코너 장식이 늘어나는 영역에 걸치면 찢어진다 → 점검 메뉴가 경계 0을 잡고, 캡처로 육안 확인.
- **씬 직렬화 변경**: 드롭인(4단계)은 GUID 유지라 씬 변경 없음. 3단계는 코드 외에 §3.6의 씬 편집(컴포넌트 7개 파트 지정, Image 13개 재지정)이 있으므로 BattleScene diff가 생긴다 → Unity MCP로 편집하고 캡처로 확인, 한 커밋으로 묶는다. `_XxxCanvas`로 박제된 레이아웃은 재생성이 필요하다 → 각 UI의 「기본 레이아웃 생성」을 다시 실행하는 항목을 3단계 계획에 포함.
- **크레딧**: 상한 60, 중간 보고 30.

## 8. 미결 (구현 중 결정)

- `Rectangle 16/41`, `new act 2/3`, `new cardNormal`의 씬 용도 (4단계 착수 시 씬 조사)
- 메인메뉴·선택/취소 버튼의 TMP 분리. 이번 범위 밖, 후속 과제
