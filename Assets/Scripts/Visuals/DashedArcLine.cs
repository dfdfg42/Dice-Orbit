using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 조준선 공용 렌더 헬퍼: 포물선 아크 + 흐르는 점선 + 입체(튜브 음영) + 꽉 찬 화살촉.
    /// 몬스터 인텐트 라인(MonsterAttackIntentManager)과 캐릭터 타게팅 라인(SkillTargetSelector)이
    /// 같은 형태 언어("조준 = 흐르는 점선 포물선")를 공유하도록 단일 출처로 추출.
    /// 폭/화살촉 크기 등 형태 상수도 여기서 소유한다(단일 소스 — 호출부는 색·경로만 넘김).
    ///
    /// 입체감(스타일라이즈드):
    ///  (1) 선은 폭 방향(V)으로 가운데가 밝고 위아래가 어두운 '튜브 음영' 텍스처 → 둥근 파이프처럼 보인다.
    ///  (2) 화살촉은 밑변이 넓고 끝이 뾰족한 '채워진 삼각형'(폭 테이퍼 LineRenderer) + 밑→끝 밝기 그라디언트.
    /// 점선 흐름은 공유 텍스처의 컬럼 순환으로 구현 (Sprites/Default가 UV 오프셋을 무시하는 문제 회피 —
    /// 구 MonsterAttackIntentManager의 검증된 방식을 승계).
    /// </summary>
    public static class DashedArcLine
    {
        /// <summary>조준선 기본 폭 (두껍게). 호출부가 폭을 지정하지 않으면 이 값을 쓴다.</summary>
        public const float DefaultWidth = 0.28f;

        private const int TexWidth = 16;    // U: 점선 (8 on / 8 off), 길이 방향 반복
        private const int TexHeight = 12;   // V: 튜브 음영 램프, 폭 방향 1회
        private static Texture2D _tex;
        private static Color[] _buf;        // 재업로드 버퍼 (index = y*TexWidth + x)
        private static float[] _bright;     // 행별 밝기 (폭 방향 튜브 램프)
        private static int _dashOffset;     // 점선 흐름 오프셋
        private static DashDriver _driver;

        private static void EnsureTexture()
        {
            if (_tex != null && _driver != null) return;

            if (_tex == null)
            {
                _tex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false)
                {
                    wrapModeU = TextureWrapMode.Repeat,   // 길이 방향 점선 반복
                    wrapModeV = TextureWrapMode.Clamp,    // 폭 방향 램프 1회
                    filterMode = FilterMode.Bilinear,     // 튜브 음영을 매끄럽게
                    name = "DashedArcTex"
                };

                // 폭 방향 밝기 램프: 가운데(v=0.5) 밝고 위아래 가장자리로 갈수록 어둡게 → 둥근 튜브.
                _bright = new float[TexHeight];
                for (int y = 0; y < TexHeight; y++)
                {
                    float v = (y + 0.5f) / TexHeight;           // 0..1
                    float e = Mathf.Abs(v - 0.5f) * 2f;          // 0(중앙)..1(가장자리)
                    _bright[y] = Mathf.Lerp(1f, 0.42f, e * e);   // 가장자리로 갈수록 어둡게
                }

                _buf = new Color[TexWidth * TexHeight];
                RebuildBuffer();
                _tex.SetPixels(_buf);
                _tex.Apply(false, false);
            }

            if (_driver == null)
                _driver = new GameObject("[DashedArcDriver]").AddComponent<DashDriver>();
        }

        /// <summary>현재 점선 오프셋으로 텍스처 버퍼 재구성 (점선 alpha × 튜브 밝기 rgb).</summary>
        private static void RebuildBuffer()
        {
            for (int y = 0; y < TexHeight; y++)
            {
                float b = _bright[y];
                for (int x = 0; x < TexWidth; x++)
                {
                    int sx = (x + _dashOffset) % TexWidth;
                    float a = sx < TexWidth / 2 ? 1f : 0f;       // 8 on / 8 off
                    _buf[y * TexWidth + x] = new Color(b, b, b, a);
                }
            }
        }

        /// <summary>공유 점선 텍스처를 주기적으로 순환시켜 "흐르는" 느낌을 만든다.</summary>
        private sealed class DashDriver : MonoBehaviour
        {
            public float shiftInterval = 0.05f;
            private float _timer;

            private void Update()
            {
                if (_tex == null || _buf == null) return;

                _timer += Time.deltaTime;
                if (_timer < shiftInterval) return;
                _timer = 0f;

                _dashOffset = (_dashOffset + 1) % TexWidth;
                RebuildBuffer();
                _tex.SetPixels(_buf);
                _tex.Apply(false, false);
            }

            private void OnDestroy()
            {
                if (_driver != this) return;
                _driver = null;
                if (_tex != null) { Destroy(_tex); _tex = null; _buf = null; _bright = null; }
            }
        }

        // ── 생성 ─────────────────────────────────────────────────

        /// <summary>점선 튜브 아크용 LineRenderer 생성.</summary>
        public static LineRenderer CreateArc(Transform parent, float width = DefaultWidth)
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
            lr.numCornerVertices = 4;   // 두꺼운 아크 굴곡을 매끄럽게
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            lr.material = new Material(shader) { mainTexture = _tex };
            lr.sortingOrder = 100;
            lr.enabled = false;
            return lr;
        }

        /// <summary>채워진 삼각형 화살촉용 LineRenderer 생성 (밑변→끝 폭 테이퍼, 무광 단색).</summary>
        public static LineRenderer CreateArrow(Transform parent, float width = DefaultWidth)
        {
            var go = new GameObject("_ArrowHead");
            if (parent != null) go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));  // 텍스처 없음 = 단색
            lr.sortingOrder = 101;
            lr.enabled = false;
            return lr;
        }

        // ── 갱신 ─────────────────────────────────────────────────

        /// <summary>
        /// 포물선 아크 + 채워진 화살촉을 한 번에 갱신.
        /// 아크는 양끝 페이드 그라디언트 + 폭 방향 튜브 음영, 화살촉은 밑(어둡게)→끝(하이라이트) 그라디언트.
        /// </summary>
        public static void SetArcWithArrow(LineRenderer arc, LineRenderer arrow,
            Vector3 start, Vector3 end, Color color,
            int segments = 24, float heightMultiplier = 0.2f, float minHeight = 0.4f, float maxHeight = 2f,
            float dashWorldLength = 0.45f, float fadeEdgeRatio = 0.14f, float arrowSize = 0.55f)
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

            // 페이드 그라디언트 (양끝 부드럽게)
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

            // 점선 반복 밀도 (길이 방향만 타일, 폭 방향 램프는 1회 유지)
            if (arc.material != null && arc.material.mainTexture != null)
                arc.material.mainTextureScale = new Vector2(Mathf.Max(1f, distance / Mathf.Max(0.05f, dashWorldLength)), 1f);

            // 채워진 삼각형 화살촉: 끝점 접선 방향으로 밑변(넓음)→끝(뾰족)
            if (arrow != null)
            {
                Vector3 tangent = end - prev;
                if (tangent.sqrMagnitude < 1e-6f) tangent = end - start;
                tangent.Normalize();

                Vector3 back = end - tangent * arrowSize;
                arrow.positionCount = 2;
                arrow.startWidth = arrowSize * 1.15f;   // 밑변 폭 (선보다 넓게 = 또렷한 화살표)
                arrow.endWidth = 0.001f;                // 끝 = 뾰족한 점
                arrow.SetPosition(0, back);
                arrow.SetPosition(1, end);

                // 밑(어둡게) → 끝(하이라이트) 밝기 그라디언트 = 입체 화살촉
                var baseCol = new Color(color.r * 0.65f, color.g * 0.65f, color.b * 0.65f, 1f);
                var tipCol  = Color.Lerp(new Color(color.r, color.g, color.b, 1f), Color.white, 0.35f);
                var arrowGradient = new Gradient();
                arrowGradient.SetKeys(
                    new[] { new GradientColorKey(baseCol, 0f), new GradientColorKey(tipCol, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                arrow.colorGradient = arrowGradient;
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
