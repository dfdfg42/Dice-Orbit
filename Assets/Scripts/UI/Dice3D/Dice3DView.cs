using System.Collections;
using TMPro;
using UnityEngine;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 주사위 1개짜리 3D 미니 스테이지 — 큐브 + 6면 숫자(TMP) + 전용 카메라 + RenderTexture.
    /// DiceElement가 RawImage로 Texture를 그린다.
    ///
    /// 값은 게임 로직(DiceManager)이 이미 확정한 상태 — 여기는 무작위 텀블 후
    /// "확정 면이 카메라를 보고 숫자가 바로 서는" 회전으로 착지하는 연출만 담당한다.
    /// 면 텍스트가 동적(TMP)이라 특수 주사위의 커스텀 눈(Faces)도 그대로 지원.
    /// </summary>
    public class Dice3DView : MonoBehaviour
    {
        private const int RtSize = 256;
        // 흰 카드 없이 주사위 단독 표시라 화면을 넉넉히 채우는 거리.
        // 텀블 중에는 TumbleScale로 줄여 굴러도 모서리가 프레임에 안 잘리고, 착지하며 1.0으로 커진다.
        private const float CamDistance = 3.0f;
        private const float CamFov = 28f;
        private const float TumbleScale = 0.85f;

        // 면 순서: +Z, -Z, +Y, -Y, +X, -X
        private static readonly Vector3[] FaceNormals =
        {
            Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left,
        };

        private static Material _dieMaterial;

        private Transform die;
        private Camera cam;
        private RenderTexture rt;
        private readonly TextMeshPro[] faceTexts = new TextMeshPro[6];
        private readonly Vector3[] faceUps = new Vector3[6];   // 각 면 텍스트의 '위' 방향 (die 로컬)
        private readonly int[] faceValues = { 1, 2, 3, 4, 5, 6 };

        private Vector3 angularVelocity;         // deg/sec
        private Vector3 targetAngularVelocity;   // 굴러가며 갈아탈 다음 축
        private float axisChangeTimer;
        private bool tumbling;
        private Coroutine settleRoutine;

        public Texture Texture => rt;

        public static Dice3DView Create(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("Dice3DView");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var view = go.AddComponent<Dice3DView>();
            view.Build();
            return view;
        }

        // ── 구성 ──────────────────────────────────────────────

        private void Build()
        {
            // 주사위 본체 (흰 큐브)
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Die";
            var col = cube.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            die = cube.transform;
            die.SetParent(transform, false);
            die.localPosition = Vector3.zero;

            if (_dieMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                _dieMaterial = new Material(shader) { color = Color.white };
                if (_dieMaterial.HasProperty("_Smoothness")) _dieMaterial.SetFloat("_Smoothness", 0.35f);
            }
            cube.GetComponent<MeshRenderer>().sharedMaterial = _dieMaterial;

            // 6면 숫자 — TMP(SDF)는 셰이더가 언릿이라 조명이 어두워도 숫자는 읽힌다
            for (int i = 0; i < 6; i++)
            {
                var n = FaceNormals[i];
                Vector3 up = n == Vector3.up ? Vector3.back
                           : n == Vector3.down ? Vector3.forward
                           : Vector3.up;

                var tgo = new GameObject("Face" + i, typeof(RectTransform));
                tgo.transform.SetParent(die, false);
                tgo.transform.localPosition = n * 0.502f;
                // TMP 3D 텍스트는 Quad처럼 -Z 쪽에서 읽힌다 → 읽히는 면이 바깥(n)을 보게 -n을 forward로
                tgo.transform.localRotation = Quaternion.LookRotation(-n, up);
                faceUps[i] = tgo.transform.localRotation * Vector3.up;

                var txt = tgo.AddComponent<TextMeshPro>();
                txt.text = faceValues[i].ToString();
                txt.fontSize = 6f;
                txt.fontStyle = FontStyles.Bold;
                txt.color = new Color(0.15f, 0.14f, 0.19f);
                txt.alignment = TextAlignmentOptions.Center;
                txt.rectTransform.sizeDelta = new Vector2(1f, 1f);
                faceTexts[i] = txt;
            }

            // 전용 카메라 → RT (배경 투명, 이 셀만 비춤)
            var camGo = new GameObject("DieCam");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -CamDistance);
            camGo.transform.localRotation = Quaternion.identity;
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = CamFov;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.allowHDR = false;
            cam.useOcclusionCulling = false;

            rt = new RenderTexture(RtSize, RtSize, 16, RenderTextureFormat.ARGB32) { name = "DiceRT" };
            rt.Create();
            cam.targetTexture = rt;

            // 필 라이트 — 씬 직사광 반대쪽 면이 새까매져 텀블 중 숫자가 안 읽히는 것 방지.
            // 범위가 좁은 포인트 라이트라 본 씬(3000 유닛 밖)에는 영향 없음.
            var lightGo = new GameObject("FillLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0.9f, 1.3f, -2.2f);
            var fill = lightGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 8f;
            fill.intensity = 1.2f;
            fill.shadows = LightShadows.None;
        }

        // ── 풀 수명 ───────────────────────────────────────────

        public void OnAcquire(int[] faces, int value)
        {
            SetFaces(faces);
            SnapToValue(value);
            gameObject.SetActive(true);
        }

        public void OnRelease()
        {
            StopSettle();
            tumbling = false;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (rt != null)
            {
                if (cam != null) cam.targetTexture = null;
                rt.Release();
                if (Application.isPlaying) Destroy(rt);
                else DestroyImmediate(rt);   // 에디터 검증 경로
            }
        }

        // ── 면/값 ─────────────────────────────────────────────

        /// <summary>6면 값 설정 (특수 주사위 커스텀 눈). null이면 1~6 유지.</summary>
        public void SetFaces(int[] faces)
        {
            if (faces == null || faces.Length == 0) return;
            for (int i = 0; i < 6; i++)
            {
                faceValues[i] = faces[Mathf.Min(i, faces.Length - 1)];
                if (faceTexts[i] != null) faceTexts[i].text = faceValues[i].ToString();
            }
        }

        private int FaceIndexOf(int value)
        {
            for (int i = 0; i < 6; i++)
                if (faceValues[i] == value) return i;

            // 어떤 면에도 없는 값(효과로 보정된 값 등) — 0번 면을 그 값으로 다시 라벨링
            faceValues[0] = value;
            if (faceTexts[0] != null) faceTexts[0].text = value.ToString();
            return 0;
        }

        /// <summary>face 면이 카메라(-Z)를 보고 숫자가 바로 서는 die 로컬 회전.</summary>
        private Quaternion TargetRotation(int face)
        {
            var q0 = Quaternion.LookRotation(FaceNormals[face], faceUps[face]);
            var qt = Quaternion.LookRotation(Vector3.back, Vector3.up);
            return qt * Quaternion.Inverse(q0);
        }

        /// <summary>연출 없이 즉시 해당 값의 면을 정면으로.</summary>
        public void SnapToValue(int value)
        {
            StopSettle();
            tumbling = false;
            die.localRotation = TargetRotation(FaceIndexOf(value));
            die.localScale = Vector3.one;
        }

        // ── 연출 ─────────────────────────────────────────────

        /// <summary>무작위 텀블 시작/정지 (정지만으로는 회전을 되돌리지 않음 — Settle/Snap으로 마무리).</summary>
        public void SetTumbling(bool on)
        {
            if (on)
            {
                StopSettle();
                angularVelocity = RandomAngularVelocity();
                targetAngularVelocity = RandomAngularVelocity();
                axisChangeTimer = NextAxisChangeDelay();
                die.localScale = Vector3.one * TumbleScale;   // 구르는 동안 축소 — 모서리 잘림 방지
            }
            tumbling = on;
        }

        private static Vector3 RandomAngularVelocity()
            => Random.onUnitSphere * Random.Range(640f, 920f);   // 텀블은 시원하게 빠르게 — 감속 단계에서 죽는다

        private static float NextAxisChangeDelay()
            => Random.Range(0.12f, 0.3f);

        private void Update()
        {
            if (!tumbling) return;

            // 고정 각속도는 "한 축으로만 도는" 느낌이라, 목표 축을 주기적으로 갈아타며
            // 방향은 slerp·크기는 lerp로 흘려보낸다 (속도 유지 + 축이 계속 뒤척이는 텀블).
            axisChangeTimer -= Time.deltaTime;
            if (axisChangeTimer <= 0f)
            {
                targetAngularVelocity = RandomAngularVelocity();
                axisChangeTimer = NextAxisChangeDelay();
            }

            float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
            Vector3 dir = Vector3.Slerp(angularVelocity.normalized, targetAngularVelocity.normalized, k);
            float mag = Mathf.Lerp(angularVelocity.magnitude, targetAngularVelocity.magnitude, k);
            angularVelocity = dir * mag;

            die.localRotation = Quaternion.Euler(angularVelocity * Time.deltaTime) * die.localRotation;
        }

        /// <summary>텀블을 멈추고 확정 값 면으로 착지 — 감속 자체가 확정 면을 향해 굴러가
        /// 느려지다 정확히 그 면에서 멈춘다. duration = 전체 시간.</summary>
        public void SettleToValue(int value, float duration)
        {
            tumbling = false;
            StopSettle();
            if (!gameObject.activeInHierarchy || duration <= 0f)
            {
                SnapToValue(value);
                return;
            }
            settleRoutine = StartCoroutine(CoSettle(FaceIndexOf(value), duration));
        }

        // "목표 면까지의 델타 + 여분 N바퀴"를 easeOutSine으로 소진 — 회전이 점점 느려지며
        // 마지막 바퀴에서 확정 숫자가 자연스럽게 정면으로 올라오고, 속도 0으로 멈춘다.
        private IEnumerator CoSettle(int face, float duration)
        {
            var target = TargetRotation(face);
            var from = die.localRotation;
            float fromScale = die.localScale.x;

            var delta = target * Quaternion.Inverse(from);
            delta.ToAngleAxis(out float deltaAngle, out Vector3 axis);
            if (deltaAngle > 180f) { deltaAngle = 360f - deltaAngle; axis = -axis; }   // 최단 방향
            if (float.IsNaN(axis.x) || axis.sqrMagnitude < 0.5f) axis = Vector3.up;    // delta ≈ identity 보호

            // easeOutSine의 초기 속도(π/2·Θ/D)가 현재 텀블 속도와 비슷해지도록 여분 바퀴 수 선택 → 속도 점프 없음
            float speedNow = Mathf.Max(300f, angularVelocity.magnitude);
            float idealTotal = speedNow * duration / (Mathf.PI * 0.5f);
            int extraTurns = Mathf.Clamp(Mathf.RoundToInt((idealTotal - deltaAngle) / 360f), deltaAngle < 100f ? 1 : 0, 3);
            float totalAngle = deltaAngle + 360f * extraTurns;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.Sin(t * Mathf.PI * 0.5f);   // 빠르게 → 점점 느리게 → 정지
                die.localRotation = Quaternion.AngleAxis(totalAngle * eased, axis) * from;
                die.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, eased);
                yield return null;
            }
            die.localRotation = target;
            die.localScale = Vector3.one;
            settleRoutine = null;
        }

        private void StopSettle()
        {
            if (settleRoutine != null)
            {
                StopCoroutine(settleRoutine);
                settleRoutine = null;
            }
        }

#if UNITY_EDITOR
        /// <summary>에디터 검증용 — 즉시 1프레임 렌더.</summary>
        public void RenderNow() { if (cam != null) cam.Render(); }
#endif
    }
}
