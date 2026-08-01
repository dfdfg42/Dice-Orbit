# Dice Orbit — 문서 인덱스

---

## 🏗 시스템 설계 (현재 구조)

핵심 게임플레이 시스템이 **현재 어떻게 동작하는지** 설명하는 문서.

| 문서 | 내용 |
|---|---|
| [project_structure.md](project_structure.md) | 스크립트 폴더 구조 + 전투 아키텍처 개요 |
| [run_structure_system.md](run_structure_system.md) | **런 구조 (노드맵)** — 맵 생성/경제/파티/점감 부활/유물/포션/이벤트/상점 (2026-07) |
| [skill_system_structure.md](skill_system_structure.md) | 전투 도메인 클래스 다이어그램, 스킬/패시브/컨텍스트 구조 |
| [skill_targeting_system.md](skill_targeting_system.md) | 액티브 스킬 타겟 선택 시스템 (OneEnemy~MultiTile) |
| [combat_reactor_dispatch.md](combat_reactor_dispatch.md) | ICombatReactor DIM 타입별 디스패치 (OnAttack/OnHeal/OnMove/OnTurnEvent) |
| [modifireSystem.md](modifireSystem.md) | 스킬 런타임 갱신(Modifier) 시스템 — Signature/Generic, 휘발성 컨텍스트 |
| [combat_floating_notification_system.md](combat_floating_notification_system.md) | 패시브/상태이상 발동 시 플로팅 알림 버블 |
| [Pipeline.md](Pipeline.md) | 전투 파이프라인 플로차트 (mermaid) |
| [TurnSystem.md](TurnSystem.md) | 턴 시스템 플로차트 (mermaid) |

---

## 📖 구현 가이드

새 스킬·패시브·VFX를 추가할 때 참고하는 레퍼런스.

| 문서 | 내용 |
|---|---|
| [combat_pipeline_authoring_guide.md](combat_pipeline_authoring_guide.md) | 스킬/패시브/상태이상/이펙트 확장 실전 가이드 |
| [vfx_system_design.md](vfx_system_design.md) | 캐릭터 스킬 VFX 구조 및 CombatVfxProfile 사용법 |
| [animator_setup_guide.md](animator_setup_guide.md) | 캐릭터/몬스터 Animator 파라미터 설정 가이드 |

---

## 🎨 UI / 비주얼

| 문서 | 내용 |
|---|---|
| [battle_info_panel_system.md](battle_info_panel_system.md) | **전투 정보 시스템** — 우측 정보 패널·타일 패널·커서 요약 툴팁·키워드 링크 호버 (2026-07) |
| [battle_visual_language.md](battle_visual_language.md) | **전투 시각 언어 사전** — 의미→형태 표 (펄스/셰브런/리프트/브래킷/조준 아크), 공용 부품 |
| [editor_owned_ui_pattern.md](editor_owned_ui_pattern.md) | **에디터 소유 UI 패턴** — [기본 레이아웃 생성]+폴백+프리팹 슬롯+SubclassPicker, 새 UI 체크리스트 |

---

## 📐 설계안 · 구현 계획 (기록)

특정 변경을 위한 **시점 기록물**(설계안 / 구현 계획). 현재 코드가 아니라 "그때의 계획"을 담고 있으므로, 각 문서 상단의 갱신 노트를 함께 볼 것. 현재 동작은 위 *시스템 설계* 문서를 참고.

| 문서 | 내용 | 상태 |
|---|---|---|
| [combat_context_action_merge_design.md](combat_context_action_merge_design.md) | CombatContext/CombatAction 병합 + 서브클래스 데이터모델 **설계안** | 구현됨 (리액터는 이후 DIM으로 발전 → combat_reactor_dispatch.md) |
| [combat_context_action_merge_plan.md](combat_context_action_merge_plan.md) | 위 병합의 단계별 **구현 계획** | 완료 |
| [implementation_plan.md](implementation_plan.md) | 스킬 시스템 Active/Passive 분리 리팩토링 계획 | 완료 (SkillAsset안 → CharacterActiveSkill로 대체) |
| [AttackDesign.md](AttackDesign.md) | 공격 의도 표시 설계 매트릭스 | 대부분 구현됨 (→ skill_targeting_system.md) |
| [tile_attribute_visual_plan.md](tile_attribute_visual_plan.md) | 타일 속성 3D 오브젝트/VFX 부착 구조 제안 | 미채택 (→ TileAttributeBubbleManager) |
| [ui_hover_tooltip_system_design.md](ui_hover_tooltip_system_design.md) | 구 Hover 툴팁 UI 설계 (키워드·상태이상 패널) | 대체됨 (→ battle_info_panel_system.md — 경량 ShowPinned만 존치) |
| [superpowers/specs/2026-07-05-battle-info-panel-design.md](superpowers/specs/2026-07-05-battle-info-panel-design.md) | 전투 정보 패널 **설계안** | 구현됨 (→ battle_info_panel_system.md) |
| [superpowers/specs/2026-07-07-run-structure-node-map-design.md](superpowers/specs/2026-07-07-run-structure-node-map-design.md) | 런 구조·노드맵 **기획 스펙** (의도의 진실 소스) | 구현됨 (→ run_structure_system.md) |
| [superpowers/plans/2026-07-28-save-system-refactor.md](superpowers/plans/2026-07-28-save-system-refactor.md) | 세이브 시스템 리팩토링 **구현 계획** — 참가자 방식 전환 + saveId 도입 | 구현됨 (→ project_structure.md) |

---

## 📊 데이터

| 파일 | 내용 |
|---|---|
| [combat_pipeline_matrix.csv](combat_pipeline_matrix.csv) | 전투 파이프라인 트리거 매트릭스 |
| [skill_authoring_sheet_template.csv](skill_authoring_sheet_template.csv) | 스킬 제작 시트 템플릿 |

---

## 🗄 보관 (감사·리포트)

| 문서 | 내용 |
|---|---|
| [deadcode_audit_2026-07.md](deadcode_audit_2026-07.md) | Assets/Scripts 레거시/데드코드 감사 리포트 (2026-07) |
