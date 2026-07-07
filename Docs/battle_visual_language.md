# 전투 시각 언어 사전

> 2026-07-05~07 정립. "하나의 의미 = 하나의 형태" — 같은 형태를 두 의미에 쓰지 않는다.
> 새 인디케이터를 추가할 때 이 표와 충돌하지 않는지 먼저 확인할 것.

## 의미 → 형태 표

| 의미 | 형태 | 구현 |
|---|---|---|
| 스킬 타게팅 범위 | 타일 **면 채움 + 외곽선, 동기화된 숨쉬기 펄스** | `Visuals/TileSkillPreviewManager.cs` |
| 이동 경로 | 타일마다 **셰브런(V) + 알파 웨이브** (방향으로 흐름) | `Visuals/MovePathPreview.cs` |
| 이동 목적지 | **소나 핑** (사각 루프 확장·페이드) + 타일 리프트 | `Visuals/MovePathPreview.cs` |
| 몬스터 공격 예고 타일 | 타일 **리프트** (고스트 메시 부상) + 정체성 색 파이 오버레이 | `Visuals/IntentTileLiftEffect.cs`, `MonsterTileColorOverlayManager.cs` |
| 패시브 영향 범위 (조회 시) | 타일 모서리 **ㄱ자 브래킷** | `Visuals/PassiveRangeIndicator.cs` + `IPassiveRangeProvider` |
| 조준선 (캐릭터↔몬스터 공통) | **포물선 + 흐르는 점선 + 화살촉** | `Visuals/DashedArcLine.cs` (공용 헬퍼) |
| 몬스터 정체성 | 몬스터별 고유 색 — 발밑 마커·타일 파이·조준선이 **같은 색** | `Visuals/MonsterIdentityManager.cs` |
| 스킬 못 쓰는 주사위 | 주사위 **살짝 붉게** (캐릭터 선택 중) | `UI/DiceElement.cs` (skillUnusableHint) |

색 의미(캐릭터 조준선): 유효 = 초록 / 무효 = 빨강 / 확정 = 하늘색.
몬스터 조준선 색 = 그 몬스터의 정체성 색 (타일 파이와 매칭 → "누가 누굴 노리나" 색으로 읽힘).

## 공용 부품

| 부품 | 내용 |
|---|---|
| `Visuals/TileLift.cs` | **고스트 리프트 기법**: 원본 MeshRenderer 끄고 복제 메시를 띄움 — TileData 트랜스폼은 절대 안 건드림 (게임플레이 위치 의존). 오버레이/아이콘 버블은 `SetLiftOffset`으로 함께 탑승 |
| `Visuals/TileCornerResolver.cs` | 타일 윗면 모서리 좌표 계산 (브래킷/외곽선 공용) |
| `Visuals/DashedArcLine.cs` | 조준 아크 단일 출처: 포물선 정점, 페이드 그라디언트, 점선(공유 텍스처 픽셀 순환 — Sprites/Default가 UV 오프셋 무시하는 문제 회피), V자 화살촉 |

## 레이어 규칙

- 인디케이터는 **조회/의도 표시 중에만** 존재 — 상시 표시는 HP바·상태 아이콘뿐
- 타게팅 시작 시 조회성 인디케이터(브래킷 등) 자동 숨김 (조준 화면을 어지럽히지 않기)
- 전체화면 UI 진입 시 `BattleInfoPanelUI.SetVisible(false)`가 월드 인디케이터도 일괄 정리

## 기각/철거된 형태 (재도입 금지 사유 포함)

- **코밋 트레일(빙글빙글 도는 선)** — 스킬/패시브/목적지 세 의미에 겹쳐 쓰여 혼란 → 전면 철거
- **타일 틴트 글로우(패시브 범위)** — 몬스터 공격 오버레이와 색 면이 겹침 → 브래킷으로 대체
