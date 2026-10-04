using System.Collections.Generic;
using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 유닛 한 개의 타격 반응 — 흰 실루엣 플래시, 밀림·찌그러짐·기울기, 공격 반동, 움츠림 (타격감 리워크 2026-10-03).
    /// 몬스터 처치는 여기가 아니라 <see cref="DeathSliceEffect"/>가 따로 연출한다.
    ///
    /// 변형 대상은 유닛이 알려 주는 "보이는 스프라이트"(<see cref="Unit.SpriteRenderer"/>)의 트랜스폼이다 —
    /// 몬스터는 런타임에 만든 MonsterVisualRoot(자식), 캐릭터는 루트.
    ///
    /// 렌더 전용 변형: 캐릭터처럼 스프라이트가 루트에 붙어 있으면 그 트랜스폼을 실제로 움직일 때 이동 코루틴·타일 위치 계산과 충돌한다.
    /// 그래서 변형은 프레임 맨 끝(<see cref="HitReactorApplyRunner"/>의 LateUpdate)에 덧입히고,
    /// 다음 프레임 맨 앞(<see cref="HitReactorRestoreRunner"/>의 Update)에 되돌린다 — 게임 로직은 언제나 원래 위치를 본다.
    /// 변형 대상 아래에 캔버스(HP 바·이름)가 있으면 같은 순간 반대로 보정해 제자리에 둔다.
    ///
    /// 시간은 scaled — 히트스톱(timeScale 0) 동안 "가장 밀린 자세 + 흰 실루엣"으로 얼어 있다.
    /// 필요할 때 <see cref="Get"/>이 유닛에 붙인다 (프리팹 수정 불필요).
    /// </summary>
    [DisallowMultipleComponent]
    public class UnitHitReactor : MonoBehaviour
    {
        private Unit _unit;
        private SpriteRenderer _renderer;   // 보이는 스프라이트 (Unit.SpriteRenderer)
        private Transform _visual;          // 변형 대상 = _renderer의 트랜스폼
        private SpriteRenderer _flash;
        private readonly List<Transform> _canvases = new List<Transform>();

        // ── 진행 중인 효과 (duration 0 = 꺼짐) ──
        private float _hitTime, _hitDuration, _hitDistance, _hitTilt;
        private Vector3 _hitDir;
        private Vector2 _hitSquash = Vector2.one;

        private float _flashTime, _flashDuration;
        private Color _flashColor = Color.white;

        private float _lungeTime, _lungeDuration, _lungeDistance;
        private Vector3 _lungeDir;

        private float _windup;                    // 0~1 — HitDirector가 매 프레임 밀어 넣는다
        private Vector3 _windupDir;
        private float _windupPullback;
        private Vector2 _windupSquash = Vector2.one;

        // ── 적용 중 캐시 ──
        private bool _applied;
        private Vector3 _basePos, _baseScale;
        private Quaternion _baseRot;
        private readonly List<Vector3> _canvasLocalPos = new List<Vector3>();
        private readonly List<Quaternion> _canvasLocalRot = new List<Quaternion>();
        private readonly List<Vector3> _canvasLocalScale = new List<Vector3>();

        public static UnitHitReactor Get(Unit unit)
        {
            if (unit == null) throw new System.ArgumentNullException(nameof(unit));
            var reactor = unit.GetComponent<UnitHitReactor>();
            if (reactor == null) reactor = unit.gameObject.AddComponent<UnitHitReactor>();
            reactor._unit = unit;
            return reactor;
        }

        /// <summary>
        /// 유닛의 보이는 스프라이트를 (다시) 잡는다. 유닛이 렌더러를 바꿨으면 캐시·플래시를 새로 만든다.
        /// 렌더러가 없으면 false — 연출할 대상이 없다.
        /// </summary>
        private bool ResolveVisual()
        {
            var renderer = _unit != null ? _unit.SpriteRenderer : null;
            if (renderer == null) return false;
            if (renderer == _renderer) return true;

            RestoreVisual();
            _renderer = renderer;
            _visual = renderer.transform;
            _canvases.Clear();
            foreach (Transform child in _visual)
                if (child.GetComponent<Canvas>() != null) _canvases.Add(child);
            if (_flash != null) { Destroy(_flash.gameObject); _flash = null; }
            return true;
        }

        private void OnDisable() => RestoreVisual();   // 비활성·파괴 직전에 덧입힌 변형이 남지 않게

        // ═══════════════════════════════════════════════════════
        // 재생 API (HitDirector가 부른다)
        // ═══════════════════════════════════════════════════════

        /// <summary>피격 — 플래시 + 방향 밀림 + 찌그러짐 + 기울기.</summary>
        public void PlayHit(HitTierFeel feel, Vector3 worldDirection)
        {
            if (!ResolveVisual()) return;
            _flashDuration = Mathf.Max(0f, feel.flashDuration);
            _flashTime = 0f;
            _flashColor = feel.flashColor;

            _hitDuration = Mathf.Max(0.01f, feel.recoilDuration);
            _hitTime = 0f;
            _hitDistance = feel.recoilDistance;
            _hitSquash = feel.squash;
            _hitDir = worldDirection;
            // 기울기는 맞은 방향으로 — 화면 오른쪽으로 밀리면 시계 방향
            float side = Camera.main != null ? Vector3.Dot(worldDirection, Camera.main.transform.right) : 0f;
            _hitTilt = feel.tiltDegrees * (side >= 0f ? -1f : 1f);

            HitReactorRunners.Register(this);
        }

        /// <summary>공격 반동 — 살짝 물러났다가 대상 쪽으로 내밀고 돌아온다.</summary>
        public void PlayLunge(Vector3 worldDirection, float distance, float duration)
        {
            if (distance <= 0f || duration <= 0f || !ResolveVisual()) return;
            _lungeDir = worldDirection;
            _lungeDistance = distance;
            _lungeDuration = duration;
            _lungeTime = 0f;
            HitReactorRunners.Register(this);
        }

        /// <summary>움츠림 자세를 amount(0~1)만큼 유지한다. 0을 주면 해제.</summary>
        public void SetWindup(float amount, Vector3 worldDirection, float pullback, Vector2 squash)
        {
            if (!ResolveVisual()) return;
            _windup = Mathf.Clamp01(amount);
            _windupDir = worldDirection;
            _windupPullback = pullback;
            _windupSquash = squash;
            if (_windup > 0f) HitReactorRunners.Register(this);
        }

        // ═══════════════════════════════════════════════════════
        // 러너가 부른다
        // ═══════════════════════════════════════════════════════

        /// <summary>타이머를 진행한다. 아직 보여 줄 것이 남아 있으면 true.</summary>
        internal bool Advance(float deltaTime)
        {
            if (_hitDuration > 0f)   { _hitTime += deltaTime;   if (_hitTime >= _hitDuration) _hitDuration = 0f; }
            if (_flashDuration > 0f) { _flashTime += deltaTime; if (_flashTime >= _flashDuration) _flashDuration = 0f; }
            if (_lungeDuration > 0f) { _lungeTime += deltaTime; if (_lungeTime >= _lungeDuration) _lungeDuration = 0f; }

            return _hitDuration > 0f || _flashDuration > 0f || _lungeDuration > 0f || _windup > 0f;
        }

        /// <summary>프레임 맨 끝 — 변형을 덧입힌다.</summary>
        internal void ApplyVisual()
        {
            if (_applied || _renderer == null || _visual == null) return;

            Vector3 offset = Vector3.zero;
            Vector2 scaleMul = Vector2.one;
            float tilt = 0f;

            if (_hitDuration > 0f)
            {
                float k = Mathf.Clamp01(_hitTime / _hitDuration);
                offset += _hitDir * (_hitDistance * (1f - EaseOutCubic(k)));          // 맞는 순간 최대로 밀려 있다가 돌아온다
                scaleMul = Vector2.Scale(scaleMul, Vector2.LerpUnclamped(_hitSquash, Vector2.one, EaseOutBack(k)));
                tilt += _hitTilt * (1f - k) * (1f - k);
            }

            if (_lungeDuration > 0f)
                offset += _lungeDir * (_lungeDistance * LungeCurve(Mathf.Clamp01(_lungeTime / _lungeDuration)));

            if (_windup > 0f)
            {
                offset -= _windupDir * (_windupPullback * _windup);
                scaleMul = Vector2.Scale(scaleMul, Vector2.Lerp(Vector2.one, _windupSquash, _windup));
            }

            UpdateFlash();

            bool transformed = offset != Vector3.zero || scaleMul != Vector2.one || tilt != 0f;
            if (!transformed) return;

            var t = _visual;
            _basePos = t.position;
            _baseRot = t.rotation;
            _baseScale = t.localScale;

            // 캔버스는 지금의 월드 자세를 기억해 두었다가 그대로 유지한다
            _canvasLocalPos.Clear(); _canvasLocalRot.Clear(); _canvasLocalScale.Clear();
            var worldPos = ListPool.Positions; var worldRot = ListPool.Rotations;
            worldPos.Clear(); worldRot.Clear();
            for (int i = 0; i < _canvases.Count; i++)
            {
                var c = _canvases[i];
                if (c == null) { _canvasLocalPos.Add(default); _canvasLocalRot.Add(default); _canvasLocalScale.Add(default); worldPos.Add(default); worldRot.Add(default); continue; }
                _canvasLocalPos.Add(c.localPosition); _canvasLocalRot.Add(c.localRotation); _canvasLocalScale.Add(c.localScale);
                worldPos.Add(c.position); worldRot.Add(c.rotation);
            }

            float sx = scaleMul.x, sy = scaleMul.y;
            t.position = _basePos + offset;
            t.localScale = new Vector3(_baseScale.x * sx, _baseScale.y * sy, _baseScale.z);
            if (tilt != 0f) t.rotation = _baseRot * Quaternion.Euler(0f, 0f, tilt);

            for (int i = 0; i < _canvases.Count; i++)
            {
                var c = _canvases[i];
                if (c == null) continue;
                var ls = _canvasLocalScale[i];
                c.localScale = new Vector3(sx != 0f ? ls.x / sx : ls.x, sy != 0f ? ls.y / sy : ls.y, ls.z);
                c.SetPositionAndRotation(worldPos[i], worldRot[i]);
            }

            _applied = true;
        }

        /// <summary>다음 프레임 맨 앞 — 덧입힌 변형을 되돌린다.</summary>
        internal void RestoreVisual()
        {
            if (!_applied) return;
            _applied = false;
            if (_visual == null) return;   // 보이는 스프라이트가 먼저 파괴됐다

            var t = _visual;
            t.position = _basePos;
            t.rotation = _baseRot;
            t.localScale = _baseScale;

            for (int i = 0; i < _canvases.Count; i++)
            {
                var c = _canvases[i];
                if (c == null) continue;
                c.localPosition = _canvasLocalPos[i];
                c.localRotation = _canvasLocalRot[i];
                c.localScale = _canvasLocalScale[i];
            }
        }

        /// <summary>효과가 전부 끝났을 때 — 플래시를 확실히 끈다.</summary>
        internal void OnFinished()
        {
            if (_flash != null) _flash.enabled = false;
        }

        // ═══════════════════════════════════════════════════════
        // 내부
        // ═══════════════════════════════════════════════════════

        private void UpdateFlash()
        {
            if (_flashDuration <= 0f)
            {
                if (_flash != null && _flash.enabled) _flash.enabled = false;
                return;
            }

            if (_flash == null) CreateFlash();

            float k = Mathf.Clamp01(_flashTime / _flashDuration);
            float alpha = k < 0.4f ? 1f : 1f - (k - 0.4f) / 0.6f;   // 앞 40%는 완전한 흰 실루엣, 그 뒤로 풀린다

            _flash.sprite = _renderer.sprite;
            _flash.flipX = _renderer.flipX;
            _flash.flipY = _renderer.flipY;
            _flash.sortingLayerID = _renderer.sortingLayerID;
            _flash.sortingOrder = _renderer.sortingOrder + 1;
            _flash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, alpha * _flashColor.a);
            _flash.enabled = _renderer.enabled;
        }

        private void CreateFlash()
        {
            var material = HitFeelProfile.Current.flashMaterial;
            if (material == null)
                throw new System.InvalidOperationException("[UnitHitReactor] HitFeelProfile.flashMaterial이 비어 있다 — 메뉴 [DiceOrbit/Create HitFeel Profile]로 다시 만들 것.");

            var go = new GameObject("HitFlash");
            go.layer = _renderer.gameObject.layer;
            go.transform.SetParent(_renderer.transform, false);
            _flash = go.AddComponent<SpriteRenderer>();
            _flash.sharedMaterial = material;
            _flash.enabled = false;
        }

        /// <summary>반동 곡선: 0~0.2 뒤로(−0.3) → 0.2~0.45 앞으로(+1) → 0.45~1 제자리.</summary>
        private static float LungeCurve(float k)
        {
            if (k < 0.2f) return -0.3f * (k / 0.2f);
            if (k < 0.45f) return Mathf.Lerp(-0.3f, 1f, EaseOutCubic((k - 0.2f) / 0.25f));
            float r = (k - 0.45f) / 0.55f;
            return 1f - r * r * (3f - 2f * r);
        }

        private static float EaseOutCubic(float k) => 1f - Mathf.Pow(1f - k, 3f);

        private static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
        }

        /// <summary>프레임당 재사용하는 임시 목록 (한 번에 한 리액터만 쓴다 — 메인 스레드).</summary>
        private static class ListPool
        {
            public static readonly List<Vector3> Positions = new List<Vector3>();
            public static readonly List<Quaternion> Rotations = new List<Quaternion>();
        }
    }
}
