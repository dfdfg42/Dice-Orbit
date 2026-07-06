using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 조준선 공용 렌더 헬퍼: 포물선 아크 + 흐르는 점선 + 화살촉.
    /// 몬스터 인텐트 라인(MonsterAttackIntentManager)과 캐릭터 타게팅 라인(SkillTargetSelector)이
    /// 같은 형태 언어("조준 = 흐르는 점선 포물선")를 공유하도록 단일 출처로 추출.
    ///
    /// 점선 흐름은 공유 텍스처의 픽셀 순환으로 구현 (Sprites/Default가 UV 오프셋을 무시하는 문제 회피 —
    /// 구 MonsterAttackIntentManager의 검증된 방식을 승계).
    /// </summary>
    public static class DashedArcLine
    {
        private const int TexWidth = 16;
        private static Texture2D _tex;
        private static Color[] _px;
        private static DashDriver _driver;

        private static void EnsureTexture()
        {
            if (_tex != null && _driver != null) return;

            if (_tex == null)
            {
                // 8-on / 8-off 점선 패턴 (U 반복)
                _tex = new Texture2D(TexWidth, 1, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Point,
                    name = "DashedArcTex"
                };
                _px = new Color[TexWidth];
                for (int x = 0; x < TexWidth; x++)
                    _px[x] = x < TexWidth / 2 ? Color.white : new Color(1f, 1f, 1f, 0f);
                _tex.SetPixels(_px);
                _tex.Apply(false, false);
            }

            if (_driver == null)
                _driver = new GameObject("[DashedArcDriver]").AddComponent<DashDriver>();
        }

        /// <summary>공유 점선 텍스처를 주기적으로 순환시켜 "흐르는" 느낌을 만든다.</summary>
        private sealed class DashDriver : MonoBehaviour
        {
            public float shiftInterval = 0.05f;
            private float _timer;

            private void Update()
            {
                if (_tex == null || _px == null) return;

                _timer += Time.deltaTime;
                if (_timer < shiftInterval) return;
                _timer = 0f;

                Color last = _px[_px.Length - 1];
                for (int i = _px.Length - 1; i > 0; i--)
                    _px[i] = _px[i - 1];
                _px[0] = last;

                _tex.SetPixels(_px);
                _tex.Apply(false, false);
            }

            private void OnDestroy()
            {
                if (_driver != this) return;
                _driver = null;
                if (_tex != null) { Destroy(_tex); _tex = null; _px = null; }
            }
        }

        // ── 생성 ─────────────────────────────────────────────────

        /// <summary>점선 아크용 LineRenderer 생성.</summary>
        public static LineRenderer CreateArc(Transform parent, float width)
        {
            EnsureTexture();

            var go = new GameObject("_Arc");
            if (parent != null) go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.startWidth = width;
            lr.endWidth = width;
            lr.textureMode = LineTextureMode.Tile;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            lr.material = new Material(shader) { mainTexture = _tex };
            lr.sortingOrder = 100;
            lr.enabled = false;
            return lr;
        }

        /// <summary>화살촉(V자)용 LineRenderer 생성 (실선).</summary>
        public static LineRenderer CreateArrow(Transform parent, float width)
        {
            var go = new GameObject("_ArrowHead");
            if (parent != null) go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 3;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = 101;
            lr.enabled = false;
            return lr;
        }

        // ── 갱신 ─────────────────────────────────────────────────

        /// <summary>
        /// 포물선 아크 + 화살촉을 한 번에 갱신.
        /// 색은 아크 양끝 페이드 그라디언트, 화살촉은 불투명 단색.
        /// </summary>
        public static void SetArcWithArrow(LineRenderer arc, LineRenderer arrow,
            Vector3 start, Vector3 end, Color color,
            int segments = 24, float heightMultiplier = 0.2f, float minHeight = 0.4f, float maxHeight = 2f,
            float dashWorldLength = 0.45f, float fadeEdgeRatio = 0.14f, float arrowSize = 0.4f)
        {
            if (arc == null) return;

            // 포물선
            segments = Mathf.Max(4, segments);
            arc.positionCount = segments + 1;

            float distance = Vector3.Distance(start, end);
            float height = Mathf.Clamp(distance * heightMultiplier, minHeight, maxHeight);

            Vector3 prev = start;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(start, end, t);
                p.y += 4f * t * (1f - t) * height;   // 0→1→0 포물선 프로파일
                arc.SetPosition(i, p);
                if (i == segments - 1) prev = p;      // 끝점 직전 → 접선 계산용
            }

            // 페이드 그라디언트
            float edge = Mathf.Clamp01(fadeEdgeRatio);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, edge),
                    new GradientAlphaKey(color.a, 1f - edge),
                    new GradientAlphaKey(0f, 1f)
                });
            arc.colorGradient = gradient;

            // 점선 반복 밀도
            if (arc.material != null && arc.material.mainTexture != null)
                arc.material.mainTextureScale = new Vector2(Mathf.Max(1f, distance / Mathf.Max(0.05f, dashWorldLength)), 1f);

            // 화살촉: 끝점 접선 방향의 V자
            if (arrow != null)
            {
                Vector3 tangent = end - prev;
                if (tangent.sqrMagnitude < 1e-6f) tangent = end - start;
                tangent.Normalize();

                Vector3 perp = Vector3.Cross(Vector3.up, tangent);
                if (perp.sqrMagnitude < 1e-4f) perp = Vector3.Cross(Vector3.forward, tangent);
                perp.Normalize();

                Vector3 back = end - tangent * arrowSize;
                arrow.SetPosition(0, back + perp * (arrowSize * 0.55f));
                arrow.SetPosition(1, end);
                arrow.SetPosition(2, back - perp * (arrowSize * 0.55f));

                var solid = new Color(color.r, color.g, color.b, 1f);
                arrow.startColor = solid;
                arrow.endColor = solid;
            }
        }

        /// <summary>아크+화살촉 쌍 표시/숨김.</summary>
        public static void SetVisible(LineRenderer arc, LineRenderer arrow, bool visible)
        {
            if (arc != null) arc.enabled = visible;
            if (arrow != null) arrow.enabled = visible;
        }
    }
}
