# Dice Orbit — 문서 인덱스

---

## 🏗 시스템 설계

핵심 게임플레이 시스템의 구조와 작동 방식.

| 문서 | 내용 |
|---|---|
| [skill_system_structure.md](skill_system_structure.md) | 전투 도메인 클래스 다이어그램, Action Pipeline 전체 구조 |
| [skill_targeting_system.md](skill_targeting_system.md) | 액티브 스킬 타겟 선택 시스템 (OneEnemy~MultiTile) |
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
| [ui_hover_tooltip_system_design.md](ui_hover_tooltip_system_design.md) | Hover 툴팁 UI 설계 (키워드·상태이상·패시브 패널) |

---

## 📐 구현 계획

아직 구현되지 않은 기능의 설계 메모.

| 문서 | 내용 | 상태 |
|---|---|---|
| [tile_attribute_visual_plan.md](tile_attribute_visual_plan.md) | 타일 속성 3D 오브젝트/VFX 부착 구조 | 미구현 |

---

## 📊 데이터

| 파일 | 내용 |
|---|---|
| [combat_pipeline_matrix.csv](combat_pipeline_matrix.csv) | 전투 파이프라인 트리거 매트릭스 |
| [skill_authoring_sheet_template.csv](skill_authoring_sheet_template.csv) | 스킬 제작 시트 템플릿 |
| [AttackDesign.md](AttackDesign.md) | 공격 의도 표시 설계 테이블 |

---

## 🗄 보관 (완료/폐기)

| 문서 | 내용 |
|---|---|
| [implementation_plan.md](implementation_plan.md) | 스킬 시스템 리팩토링 계획 — **완료** |
| [task.md](task.md) | 리팩토링 태스크 목록 — **완료** |
| [project_structure.md](project_structure.md) | 초기 프로젝트 구조 개요 |
