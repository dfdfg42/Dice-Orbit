using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 몬스터 처치 연출 — 참격이 몬스터를 가로지르고, 몬스터가 그 선을 따라 두 조각으로 갈라져 미끄러지며 사라진다 (2026-10-05).
    ///
    /// 몬스터 오브젝트와 따로 산다: 쓰러지는 순간의 스프라이트·자세를 베껴 씬 루트에 놓고, 원래 스프라이트는 끈다.
    /// 몬스터가 곧 파괴돼도 연출은 끝까지 간다.
    ///
    ///   Root (쓰러진 순간의 월드 자세·스케일)
    ///     HalfUpper · HalfLower   같은 스프라이트, 재질 DiceOrbit/SpriteSlice — _Side만 반대
    ///     Slash                   참격 줄기
    ///
    /// 시계가 둘이다. 참격은 realtime — 처치 히트스톱(timeScale 0) 동안에도 그어진다.
    /// 갈라짐은 scaled — 히트스톱이 풀린 뒤에야 벌어진다. 그래서 "베였다 → 멈칫 → 갈라진다" 순서가 된다.
    /// 수치는 전부 <see cref="DeathSliceFeel"/>(HitFeelProfile.deathSlice).
    /// </summary>
    public class DeathSliceEffect : MonoBehaviour
    {
        private static readonly int CutPointId = Shader.PropertyToID("_CutPoint");
        private static readonly int CutNormalId = Shader.PropertyToID("_CutNormal");
        private static readonly int SideId = Shader.PropertyToID("_Side");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int EdgeGlowId = Shader.PropertyToID("_EdgeGlow");

        private DeathSliceFeel _feel;
        private SpriteRenderer _upper, _lower, _slash;
        private MaterialPropertyBlock _block;

        private Vector2 _center;        // 베는 선이 지나는 점 (스프라이트 로컬)
        private Vector2 _direction;     // 베는 선 방향
        private Vector2 _normal;        // 위 조각 쪽 법선
        private Vector2 _slide;         // 위 조각이 미끄러져 내려가는 방향
        private float _diagonal;        // 스프라이트 대각선 길이 — 거리 수치의 기준
        private Color _baseColor;
        private Vector3 _slashScale;
        private float _slashLength;

        private float _clock;           // scaled — 갈라짐
        private float _slashClock;      // realtime — 참격

        /// <summary>
        /// source가 지금 보여 주는 스프라이트를 베어 가른다. source는 꺼진다.
        /// 스프라이트가 없으면 보여 줄 것이 없어 null. 재질·참격 스프라이트가 비어 있으면 예외.
        /// </summary>
        public static DeathSliceEffect Play(SpriteRenderer source, DeathSliceFeel feel)
        {
            if (feel == null) throw new System.ArgumentNullException(nameof(feel));
            if (source == null || source.sprite == null) return null;
            if (feel.sliceMaterial == null || feel.slashSprite == null)
                throw new System.InvalidOperationException("[DeathSliceEffect] HitFeelProfile.deathSlice의 sliceMaterial·slashSprite가 비어 있다 — 메뉴 [DiceOrbit/Create HitFeel Profile]로 채울 것.");

            var root = new GameObject("DeathSlice");
            root.layer = source.gameObject.layer;
            var sourceTransform = source.transform;
            Vector3 scale = sourceTransform.lossyScale;
            if (source.flipX) scale.x = -scale.x;   // 뒤집기는 스케일로 옮긴다 (조각 셰이더는 렌더러의 flip을 모른다)
            if (source.flipY) scale.y = -scale.y;
            root.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
            root.transform.localScale = scale;

            var effect = root.AddComponent<DeathSliceEffect>();
            effect.Build(source, feel);
            source.enabled = false;
            return effect;
        }

        /// <summary>베는 선의 방향과 법선 (스프라이트 로컬). 법선은 언제나 위쪽 조각을 가리킨다.</summary>
        public static void CutFrame(float angleDegrees, out Vector2 direction, out Vector2 normal)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            normal = new Vector2(-direction.y, direction.x);
            if (normal.y < 0f) normal = -normal;
        }

        /// <summary>위 조각이 미끄러지는 방향 — 베는 선을 따라 내려가는 쪽.</summary>
        public static Vector2 SlideDirection(Vector2 cutDirection)
            => cutDirection.y <= 0f ? cutDirection : -cutDirection;

        private void Build(SpriteRenderer source, DeathSliceFeel feel)
        {
            _feel = feel;
            _block = new MaterialPropertyBlock();
            _baseColor = source.color;

            var bounds = source.sprite.bounds;
            _center = bounds.center;
            _diagonal = ((Vector2)bounds.size).magnitude;

            float angle = Random.Range(feel.angleRange.x, feel.angleRange.y) * (Random.value < 0.5f ? 1f : -1f);
            CutFrame(angle, out _direction, out _normal);
            _slide = SlideDirection(_direction);

            _upper = CreateHalf("HalfUpper", source, 1f);
            _lower = CreateHalf("HalfLower", source, -1f);

            var slashGo = new GameObject("Slash");
            slashGo.layer = gameObject.layer;
            slashGo.transform.SetParent(transform, false);
            slashGo.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg);
            _slash = slashGo.AddComponent<SpriteRenderer>();
            _slash.sprite = feel.slashSprite;
            _slash.color = feel.slashColor;
            _slash.sortingLayerID = source.sortingLayerID;
            _slash.sortingOrder = source.sortingOrder + 2;

            Vector2 slashSize = feel.slashSprite.bounds.size;
            _slashLength = feel.slashLength * _diagonal;
            _slashScale = new Vector3(_slashLength / slashSize.x, feel.slashThickness * _diagonal / slashSize.y, 1f);

            Apply();
        }

        private SpriteRenderer CreateHalf(string halfName, SpriteRenderer source, float side)
        {
            var go = new GameObject(halfName);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);

            var half = go.AddComponent<SpriteRenderer>();
            half.sprite = source.sprite;
            half.color = source.color;
            half.sortingLayerID = source.sortingLayerID;
            half.sortingOrder = source.sortingOrder;
            half.sharedMaterial = _feel.sliceMaterial;

            half.GetPropertyBlock(_block);
            _block.SetVector(CutPointId, _center);
            _block.SetVector(CutNormalId, _normal);
            _block.SetFloat(SideId, side);
            _block.SetColor(FlashColorId, _feel.flashColor);
            _block.SetFloat(EdgeWidthId, _feel.edgeWidth * _diagonal);
            half.SetPropertyBlock(_block);
            return half;
        }

        private void Update()
        {
            _slashClock += Time.unscaledDeltaTime;
            _clock += Time.deltaTime;

            if (!Apply()) Destroy(gameObject);
        }

        /// <summary>지금 시각의 모습을 입힌다. 보여 줄 것이 남아 있으면 true.</summary>
        private bool Apply()
        {
            bool slashAlive = ApplySlash();

            float split = _feel.splitDuration > 0f ? Mathf.Clamp01((_clock - _feel.holdDuration) / _feel.splitDuration) : 1f;
            ApplyHalf(_upper, 1f, split);
            ApplyHalf(_lower, -1f, split);
            return slashAlive || split < 1f;
        }

        /// <summary>참격: 한쪽 끝에서 반대쪽 끝까지 그어진 뒤, 가늘어지며 사라진다.</summary>
        private bool ApplySlash()
        {
            float sweep = _feel.slashSweep > 0f ? Mathf.Clamp01(_slashClock / _feel.slashSweep) : 1f;
            float fade = _feel.slashFade > 0f ? Mathf.Clamp01((_slashClock - _feel.slashSweep) / _feel.slashFade) : 1f;
            if (fade >= 1f)
            {
                _slash.enabled = false;
                return false;
            }

            float drawn = EaseOutCubic(sweep);
            // 시작 끝을 붙잡고 늘어난다 (스프라이트 피벗은 가운데)
            Vector2 start = _center - _direction * (_slashLength * 0.5f);
            _slash.transform.localPosition = start + _direction * (_slashLength * 0.5f * drawn);
            _slash.transform.localScale = new Vector3(_slashScale.x * drawn, _slashScale.y * (1f - fade), 1f);

            var color = _feel.slashColor;
            color.a *= 1f - fade * fade;
            _slash.color = color;
            return true;
        }

        private void ApplyHalf(SpriteRenderer half, float side, float split)
        {
            float eased = EaseOutCubic(split);
            float reach = side > 0f ? 1f : -_feel.lowerRatio;   // 아래 조각은 반대로, 조금만
            Vector2 offset = _slide * (_feel.slideDistance * _diagonal * eased * reach)
                           + _normal * (_feel.gapDistance * _diagonal * eased * side);
            float tilt = _feel.tiltDegrees * eased * reach * (_slide.x >= 0f ? -1f : 1f);   // 미끄러지는 쪽으로 기운다

            // 베는 점을 축으로 돌린다
            var rotation = Quaternion.Euler(0f, 0f, tilt);
            half.transform.localRotation = rotation;
            half.transform.localPosition = (Vector3)_center - rotation * (Vector3)_center + (Vector3)offset;

            var color = _baseColor;
            color.a *= 1f - Mathf.Clamp01((split - _feel.fadeStart) / Mathf.Max(0.0001f, 1f - _feel.fadeStart));
            half.color = color;

            half.GetPropertyBlock(_block);
            _block.SetFloat(FlashId, 1f - Mathf.Clamp01(split / Mathf.Max(0.0001f, _feel.flashRelease)));   // 갈라지기 전에는 흰 실루엣
            _block.SetFloat(EdgeGlowId, 1f - split);
            half.SetPropertyBlock(_block);
        }

        private static float EaseOutCubic(float k) => 1f - Mathf.Pow(1f - k, 3f);
    }
}
