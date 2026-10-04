# 노드맵 배경 — 장식 애니메이션 · 지도 두루마리 (2026-10-04)

노드맵(맵 선택) 화면의 배경(`Assets/Sprites/배경/nodemap_bg.png`)은 탁자와 소품을 그린 통짜 그림 한 장이다 (가운데 지도 두루마리는 따로 떠 있는 스프라이트 — 아래 「지도 두루마리」 절). 그 위의 물체를 **조금씩만** 움직여 화면이 멈춰 보이지 않게 한다.

| 물체 | 움직임 | 축(pivot) |
|---|---|---|
| 촛불 | 불꽃이 불규칙하게 흔들리고 늘어난다 + 따뜻한 빛이 일렁인다 | 심지 |
| 나침반 | 바늘(별)이 느리게 떤다 (±5°) | 중심 |
| 깃펜 | 까딱인다 (±1.3°) | 잉크병 입구 |
| 찻잔 | 잔 위로 올라온 김이 하늘거린다 | 김의 밑동 |
| 고양이 | 숨 쉰다 (세로 ±0.8%) | 바닥 |
| 주사위·나침반 테·잉크병·찻잔 | 5~12초에 한 번 반짝인다 (7곳) | — |

## 원리 — 깃털 조각

배경을 물체별로 다시 그리지 않는다. 물체를 **주변 나무결과 함께** 떠내고 가장자리를 투명하게 풀어 준 조각을 원래 자리에 겹쳐 놓은 뒤, 그 조각만 움직인다.

- 조각의 불투명한 가운데(물체 + `margin`)가 밑의 원본 물체를 덮는다. **움직임이 `margin`보다 작아야** 원본이 비치지 않는다.
- 풀린 가장자리(`feather`)는 나무결끼리 섞여 이음매가 보이지 않는다.
- 그림 밖으로 나가는 부분(깃펜 끝, 고양이 오른쪽·아래)은 가장자리 픽셀을 늘려 채운다.
- 조각은 배경이 임포트될 때 줄어드는 비율(2688 → 2048)과 같은 비율로 줄여 저장한다 — 선명도가 배경과 같아야 가만히 있을 때 티가 나지 않는다.

빛(`nodemap_amb_glow.png`)과 반짝이(`nodemap_amb_sparkle.png`)는 도구가 그려서 만든다.

## 구성

| 파일 | 역할 |
|---|---|
| `Tools/make_nodemap_ambience.py` | **물체 목록의 진실 소스**(`PIECES`). 조각·빛·반짝이 스프라이트와 배치표 `ambience_layout.json`을 만든다. `--preview`는 가장 크게 움직인 자세의 합성 그림만 `_workspace`에 낸다 (이음매·비침 확인용) |
| `Assets/Sprites/배경/NodeMapAmbience/` | 조각 스프라이트 + 배치표 |
| `Assets/Scripts/Editor/NodeMapAmbienceScaffold.cs` | 메뉴 [DiceOrbit → Rebuild Node Map Ambience]. 배치표대로 `_NodeMapCanvas/Background/Ambience` 아래에 조각마다 Image + 움직임 컴포넌트를 만든다. 자리는 배경 그림에서의 비율을 앵커로 쓰므로 화면 크기가 달라져도 어긋나지 않는다 |
| `Assets/Scripts/UI/Ambience/AmbientMotion.cs` | 회전·가로세로 늘이기·알파를 파형(`Wave`: 진폭·주기·불규칙도)으로 흔든다. 축은 RectTransform의 pivot |
| `Assets/Scripts/UI/Ambience/AmbientTwinkle.cs` | 쉬다가 한 번씩 반짝인다 (커졌다 작아지며 조금 돈다) |

`Ambience`는 자식 캔버스다 — 장식이 매 프레임 움직여도 노드맵 본체(스크롤·노드)는 다시 그려지지 않는다. 두 컴포넌트 모두 unscaled 시간을 쓰고, 꺼질 때 기준 자세로 되돌린다.

## 손보기

- **움직임 수치를 바꾼다** → 씬에서 해당 조각의 `AmbientMotion`/`AmbientTwinkle`을 고친다 (에디터 소유 패턴 — 한 번 만든 뒤에는 씬이 수치를 소유한다). 진폭을 키울 때는 조각의 `margin`을 넘지 않는지 `--preview`로 확인한다.
- **물체를 더한다** → `PIECES`에 한 줄(모양 `polygon`/`ellipse`/`rect`, `margin`, `feather`, `pivot`, `motion`)을 더하고 도구를 돌린 뒤, 씬의 `Background/Ambience`를 지우고 메뉴를 다시 실행한다. 기존 조각의 `.meta`(GUID)는 유지된다.
- **배경 그림을 바꾼다** → 좌표가 전부 달라진다. `PIECES`를 새 그림에 맞춰 다시 잡아야 한다. 배경을 비우거나(펠트색) 다른 그림으로 바꾸면서 `Ambience`를 그대로 두면 옛 물체 조각이 떠 보인다.
- 다른 화면에 쓰려면 `AmbientMotion`/`AmbientTwinkle`은 그대로 쓸 수 있다 (UI 전용 — RectTransform·Graphic 기준). 조각 도구와 스캐폴드는 노드맵 배경 전용이다.

## 지도 두루마리

가운데의 지도 종이는 원래 배경 그림에 같이 그려져 있었다(네 모서리가 따로 말린 종이). 사용자 피드백("스크롤 이미지 모양이 이상하다")으로 **위·아래에 나무 봉과 손잡이가 달린 두루마리**로 바꾸면서 배경에서 떼어 냈다.

```
_NodeMapCanvas
  Background            탁자와 소품 (옛 두루마리 자리는 나무결로 메웠다) + Ambience
  MapScroll             두루마리 한 장 (그림자 포함) — 지도 내용 아래
  ScrollView            종이 안쪽에 맞춘다. 위아래로 봉 밑까지 조금 들어간다
  RollTop · RollBottom  봉 부분만 한 번 더, 지도 내용 위에 — 노드가 봉 밑으로 말려 들어가 보인다
  TitleChip             막 이름 (위쪽 봉 위에 얹힌다)
```

| 파일 | 역할 |
|---|---|
| `Tools/make_nodemap_scroll.py <그림.png>` | 배경에서 옛 두루마리를 찾아(가운데의 푸른 덩어리) 오른쪽 빈 나무결을 거울처럼 이어 붙여 메우고, 새 그림을 높이에 맞춰 가운데 놓아 스프라이트 3장(전체 + 위 봉 + 아래 봉)과 배치표 `scroll_layout.json`을 낸다. 탁자 위 그림자도 여기서 붙인다. `--preview`는 합성 그림만 낸다. 지울 두루마리가 없으면 그 단계는 건너뛴다 |
| `Assets/Sprites/배경/NodeMapScroll/` | 두루마리 스프라이트 + 배치표 |
| `Assets/Scripts/Editor/NodeMapScrollScaffold.cs` | 메뉴 [DiceOrbit → Rebuild Node Map Scroll]. 배치표대로 세 조각을 놓고 `ScrollView`를 종이 안쪽(+ 봉 높이의 45%만큼 봉 밑)으로 맞춘다. 조정할 수치가 없어(전부 그림에서 나온다) 다시 실행하면 그대로 다시 맞춘다 |

- **두루마리 그림을 바꾼다** → 투명 배경 그림 한 장을 도구에 넘기고 메뉴를 다시 실행한다. 봉·롤이 없는 평평한 종이면 롤 조각은 만들지 않고 씬의 `RollTop`·`RollBottom`도 치운다. (봉은 "종이 몸통보다 옆으로 튀어나온 행"으로 찾는다.)
- 원본 그림은 저장소에 두지 않는다 — 생성 로그 #41의 job id.
- 장식 조각(`PIECES`)은 두루마리와 겹치지 않는 자리에 있어 두루마리를 바꿔도 다시 뜰 필요가 없다.
