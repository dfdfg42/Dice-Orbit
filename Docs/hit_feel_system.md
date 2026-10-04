# 타격감 시스템 — "한 방의 여섯 겹"

- 최종 갱신: 2026-10-03 (타격감 리워크)
- 설계 스펙: [superpowers/specs/2026-10-03-hit-feel-rework-design.md](superpowers/specs/2026-10-03-hit-feel-rework-design.md)
- 코드: `Assets/Scripts/Visuals/HitFeel/`, `Assets/Scripts/Visuals/Vfx/{CameraShaker,ImpactFeedback,VfxService}.cs`
- 수치: `Assets/Resources/Combat/HitFeelProfile.asset` (코드에는 수치가 없다)

한 번의 타격이 **임팩트 VFX · 흰 실루엣 플래시 · 히트스톱 · 밀림(넉백+찌그러짐+기울기) · 카메라 트라우마 · 타격음** 여섯 겹으로 동시에 터지고, 숫자 팝업이 같은 등급으로 뜬다. 세기는 타격 등급 하나가 정한다.

---

## 1. 흐름

```
CombatPipeline.ApplyAction (AttackContext)
  ├─ Unit.TakeDamage / TakeDirectDamage ── 숫자 팝업 (등급별 크기·색·펀치)
  └─ HitDirector.ReportHit(source, target, 실제 피해, 처치 여부, 직접 손실, vfxCue)
        ├─ HitTierClassifier.Classify → 등급
        ├─ HitFeelProfile.Get(등급) → HitTierFeel (여섯 겹의 수치)
        ├─ ① VfxService.PlayImpact(cue, target, impactScale)   (막힘이면 "막음" 글자 추가)
        ├─ ② UnitHitReactor.Get(target).PlayHit(feel, 방향)     플래시 + 밀림
        ├─ ③ ImpactFeedback.HitStop(feel.hitStop)              겹치면 긴 쪽 하나
        ├─ ④ CameraShaker.AddTrauma(feel.trauma)               누적
        └─ ⑤ AudioManager.PlaySFXAtPoint(feel.sfx, 피치 ±6%)
```

연출 조건은 `ApplyAction` 한 곳: **맞기 전에 살아 있었고, 피해가 들어갔거나(IsEffected) 들어갔어야 할 공격(amount > 0)이 방어도에 막혔을 때**. 그래서 방어도로 전부 막힌 공격도 "막음"으로 연출된다.

때리는 쪽:

| 호출 지점 | 호출 | 내용 |
|---|---|---|
| `AutoAttackSystem.LaunchBasicHit`, `ComboActiveSkill.ApplyStageHit`, `CharacterActiveTemplate.ApplyToTarget` | `HitDirector.ReportAttackLaunched(attacker, 대상 위치)` | 대상 쪽 반동(0.35 / 0.25s) + 휘두르는 소리 |
| `CombatManager.ExecuteMonsterTurnRoutine` (위협 타일이 있는 행동만) | `yield return HitDirector.MonsterWindup(monster, 타일 중심)` → `HitDirector.MonsterStrike(...)` → `monster.ExecuteIntent()` | 0.16s 움츠림 → 내리찍기 + 소리. 피해는 내리찍는 순간 |
| `Monster` 사망 처리 | `HitDirector.MonsterDeathSlice(monster)` | 참격이 가로지르고 두 조각으로 갈라져 사라진다 — §4.1 (2026-10-05, 그 전에는 커지며 사라졌다) |

## 2. 타격 등급 (`HitTier`)

판정 우선순위: **Kill → Blocked → Tick → 세기 점수**.

| 등급 | 조건 |
|---|---|
| `Kill` | 이 타격으로 쓰러졌다 (피해가 작아도, 직접 손실이어도) |
| `Blocked` | 실제 피해 0 |
| `Tick` | 직접 체력 손실 (`IsDirectHpLoss` — 중독 등) |
| `Heavy` / `Medium` / `Light` | 점수 = ½·보간(피해/최대체력, 5%→35%) + ½·보간(피해, 4→30). ≥0.67 Heavy, ≥0.34 Medium |

경계값은 프로필의 `thresholds`(`HitTierThresholds`)에 있다. 비율과 절대값을 반씩 섞어 "체력 20에 2"가 Heavy가 되지 않고 "체력 100에 25"가 Heavy가 된다.

## 3. 프로필 (`HitFeelProfile`)

`Resources/Combat/HitFeelProfile` 한 장. `HitFeelProfile.Current`가 로드한다 — **없으면 예외**, 등급 항목이 빠져도 `Get`이 예외 (조용한 기본값 없음).

| 필드 | 내용 |
|---|---|
| `thresholds` | 등급 경계 |
| `tiers` (`HitTierFeel` × 6) | hitStop · trauma · flashDuration/Color · recoilDistance/Duration · squash · tiltDegrees · impactScale · popupScale/Color · sfx/sfxVolume |
| `characterLaunch` | 발사 반동 거리·시간·소리 |
| `monsterStrike` | 움츠림 시간·물러남·찌그러짐, 내리찍기 거리·시간·소리 |
| `flashMaterial` | `HitFlash.mat` (셰이더 `DiceOrbit/SpriteSolidFlash`) |
| `sfxPitchJitter`, `popupPunchScale/Duration`, `popupScatter`, `blockedPopupColor` | 공통 |
| `deathSlice` (`DeathSliceFeel`) | 몬스터 처치 연출 — 조각 재질 `DeathSlice.mat`(셰이더 `DiceOrbit/SpriteSlice`) · 참격 스프라이트 · 참격 시간/길이/굵기/각도 · 갈라짐 시간/거리/기울기 · 흰 실루엣·단면 빛 |

기본값 표는 스펙 §4. 에디터 메뉴:

- **DiceOrbit → Create HitFeel Profile** (`HitFeelSetup`) — 없으면 기본값으로 생성, 있으면 **빠진 것만** 보충(조정한 수치·배선한 소리는 그대로). 기본값으로 완전히 되돌리려면 에셋을 지우고 다시 실행.
- **DiceOrbit → Run HitFeel Self-Tests** (`HitFeelSelfTests`, 9건) — 등급 경계, 기본값 단조 증가, 재설정이 소리를 지우지 않음, 트라우마 수식, 처치 연출의 베는 선 기하, 실제 에셋의 클립·재질·참격 스프라이트 배선.

## 4. 유닛 반응 (`UnitHitReactor`)

`UnitHitReactor.Get(unit)`이 필요할 때 유닛에 붙인다 (프리팹 수정 없음).

- **변형 대상** = `Unit.SpriteRenderer`의 트랜스폼. 캐릭터는 루트, 몬스터는 런타임에 만드는 자식 `MonsterVisualRoot` (루트 SpriteRenderer는 꺼져 있다). 유닛이 렌더러를 바꾸면 다음 재생 때 다시 잡는다.
- **렌더 전용 변형**: `HitReactorApplyRunner`(LateUpdate, 실행 순서 +10000)가 위치·스케일·회전을 덧입히고, `HitReactorRestoreRunner`(Update, −10000)가 다음 프레임 맨 앞에 되돌린다. 이동 코루틴·타일 위치 계산·강조 스케일 트윈은 항상 원래 값을 본다. 변형 대상 아래의 캔버스(HP 바·이름)는 반대로 보정해 제자리에 둔다.
- **플래시**: 변형 대상 아래 자식 `HitFlash` — 같은 스프라이트를 `flashMaterial`로 정렬 순서 +1에 겹친다.
- **시간**: scaled — 히트스톱(timeScale 0) 동안 "가장 밀린 자세 + 흰 실루엣"으로 얼어 있다. 카메라 흔들림은 unscaled라 그동안에도 흔들린다.
- API: `PlayHit(feel, 방향)` · `PlayLunge(방향, 거리, 시간)` · `SetWindup(양, 방향, 물러남, 찌그러짐)`.

> 플레이 중 변형 값을 디버깅할 때: 코루틴의 `yield return null` 시점은 이미 복원된 뒤다. 덧입힌 값을 보려면 `WaitForEndOfFrame`에서 읽는다.

### 4.1 몬스터 처치 — 베어 가르기 (`DeathSliceEffect`, 2026-10-05)

몬스터가 쓰러지면 참격이 가로지르고, 그 선을 따라 두 조각으로 갈라져 미끄러지며 사라진다. 원인(공격·중독·타일)과 상관없이 모든 처치에 쓴다.

- **몬스터와 따로 산다**: `DeathSliceEffect.Play(스프라이트 렌더러, feel)`이 쓰러지는 순간의 스프라이트·월드 자세를 베껴 씬 루트에 놓고 원래 스프라이트를 끈다. 몬스터 오브젝트는 예전처럼 `destroyDelayAfterDeath` 뒤에 파괴되지만 연출은 끝까지 간다 (HP 바·의도 말풍선은 파괴될 때까지 남는다).
- **구성**: `HalfUpper`·`HalfLower`(같은 스프라이트 + 재질 `DiceOrbit/SpriteSlice`, `_Side`만 반대 — 직선 하나로 갈라 한쪽만 그린다) + `Slash`(참격 줄기 스프라이트 `Assets/Sprites/VFX/slash_streak.png`, `Tools/make_slash_streak.py`가 그린다).
- **시계가 둘**: 참격은 realtime이라 처치 히트스톱(timeScale 0) 동안에도 그어진다. 갈라짐은 scaled라 히트스톱이 풀린 뒤에야 벌어진다 → "베였다(흰 실루엣 + 참격) → 멈칫 → 갈라진다".
- **베는 선**: 스프라이트 가운데를 지나는 비스듬한 선(`angleRange`, 좌우 기울기는 무작위). 위 조각은 선을 따라 내려가며 기울고, 아래 조각은 반대로 조금만 움직인다. 거리는 전부 스프라이트 대각선 대비 비율이라 큰 몬스터도 같은 느낌이다.
- 처치 타격의 흰 플래시·밀림은 꺼진 스프라이트에 걸리므로 보이지 않는다 — 흰 실루엣은 조각이 직접 낸다(`flashColor`, `flashRelease`). 임팩트 VFX·히트스톱·카메라 흔들림·처치음·숫자는 그대로다.
- 조각 셰이더는 렌더러의 flip을 모른다 → 뒤집힌 스프라이트는 루트의 음수 스케일로 옮긴다. 정점을 로컬 좌표로 받아야 해서 배칭을 끈다(`DisableBatching`).

## 5. 카메라·히트스톱

- `CameraShaker` — 트라우마 모델. `AddTrauma(x)`로 누적(최대 1), 초당 `decayPerSecond` 선형 감쇠, 흔들림 = `maxOffset`(0.55) × trauma² × 펄린 노이즈 + 롤 `maxRollDegrees`(1.2°). 기존 `Shake(amplitude, duration)`은 `TraumaForAmplitude`로 환산해 같은 모델에 얹는다.
- `ImpactFeedback.HitStop(seconds)` — 끝나는 실시간 시각을 기록해 **긴 쪽이 이긴다**. 끝나면 멈추기 전 timeScale로 복원.

## 6. 숫자 팝업

`Unit.TakeDamage/TakeDirectDamage`가 `PopupTier`로 같은 등급을 구해 `FloatingLabelPopup.CreateDamage(값, 위치, 등급)`에 넘긴다. 등급의 `popupScale`·`popupColor` + 나타날 때 `popupPunchScale`→1 펀치 + `popupScatter`만큼 좌우로 흩기. 막힘은 `CreateBlocked` — "막음" (`blockedPopupColor`).

## 7. 타격음

`Assets/Sounds/Combat/` 8종 — `hit_light/medium/heavy/kill/block/tick`, `attack_swing`, `monster_strike`. `Tools/synth_hit_sfx.py`(numpy + scipy)로 절차 합성한다:

```bash
python Tools/synth_hit_sfx.py Assets/Sounds/Combat
```

재생은 `AudioManager.PlaySFXAtPoint`(풀 소스, 2D). `BattleScene`에 `AudioManager` 프리팹이 놓여 있어 씬 단독 실행에서도 소리가 난다. AudioManager가 없으면 경고 1회 후 무음.

## 8. 확장 지점

- **등급별 느낌 조정** → 프로필 에셋 수치만.
- **소리 교체** → 프로필의 `sfx` 슬롯에 클립을 꽂는다 (`HitFeelSetup`은 이미 배선된 슬롯을 건드리지 않는다).
- **처치 연출 조정** → 프로필의 `deathSlice` 수치만 (참격 색·길이, 갈라지는 시간·거리). 참격 모양은 `Tools/make_slash_streak.py`를 고쳐 다시 그린다.
- **새 등급** → `HitTier`에 값 추가 + `HitTierClassifier` 분기 + `ResetTiersToDefaults` 기본값 + 자가 테스트. 프로필에 항목이 없으면 `Get`이 예외로 알려 준다.
- **스킬 전용 임팩트** → 기존대로 `AttackContext.VfxCue` (없으면 `VfxTags.Impact`). 스케일은 등급의 `impactScale`.
- **새 공격 경로** → `CombatPipeline`을 통과하면 피격 연출은 자동. 발사 반동이 필요하면 `HitDirector.ReportAttackLaunched`만 부른다.

## 9. 범위 밖 (하지 않은 것)

HP 바 연출(잔상·흔들림), 캐릭터 사망 연출(베어 가르기는 몬스터만), 스킬별 전용 타격음, 유닛 프리팹·애니메이터 수정.
