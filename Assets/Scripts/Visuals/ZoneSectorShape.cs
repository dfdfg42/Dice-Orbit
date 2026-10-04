using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 구역 부채꼴을 감싸는 둥근 모서리 폐곡선의 점열 (순수 기하 — PassiveZoneSelfTests).
    /// 궤도 중심이 원점인 XZ 평면, 각도는 +x에서 +z 쪽으로 잰다 (CombatZoneManager.GetZoneAngularRangeDeg와 같은 기준).
    ///
    /// 양쪽 경계는 각도가 아니라 '거리'로 안쪽에 들인다 — 각도로 들이면 안쪽은 바짝 붙고 바깥쪽은 벌어진다.
    /// 경계선에서 d만큼 떨어진 평행선이 반지름 R의 호와 만나는 각도 여유는 asin(d / R).
    /// </summary>
    public static class ZoneSectorShape
    {
        /// <summary>
        /// 폐곡선 점열을 points에 채운다 (마지막 점은 첫 점과 이어진다 — LineRenderer.loop).
        /// 순서: 바깥 호(시작→끝) → 끝 경계(바깥→안) → 안쪽 호(끝→시작) → 시작 경계(안→바깥). 모서리 넷은 2차 베지어로 굴린다.
        /// </summary>
        /// <param name="edgeInset">구역 경계에서 안쪽으로 들이는 거리</param>
        /// <param name="cornerRadius">모서리를 굴리는 길이 (0이면 각진 모서리)</param>
        /// <param name="arcStepDeg">호를 나누는 각도 간격</param>
        public static void Build(float startDeg, float endDeg, float innerRadius, float outerRadius,
            float edgeInset, float cornerRadius, float arcStepDeg, float y, List<Vector3> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (innerRadius <= 0f || outerRadius <= innerRadius)
                throw new ArgumentException($"[ZoneSectorShape] 반지름이 맞지 않다 (안 {innerRadius}, 밖 {outerRadius}).");
            if (edgeInset < 0f || edgeInset >= innerRadius)
                throw new ArgumentException($"[ZoneSectorShape] 경계 들임 {edgeInset}은 0 이상, 안쪽 반지름 {innerRadius} 미만이어야 한다.");
            if (arcStepDeg <= 0f) throw new ArgumentException("[ZoneSectorShape] 호 간격은 0보다 커야 한다.");

            float innerSlack = Mathf.Asin(edgeInset / innerRadius) * Mathf.Rad2Deg;
            float outerSlack = Mathf.Asin(edgeInset / outerRadius) * Mathf.Rad2Deg;
            float innerStart = startDeg + innerSlack, innerEnd = endDeg - innerSlack;
            float outerStart = startDeg + outerSlack, outerEnd = endDeg - outerSlack;
            if (innerEnd <= innerStart)
                throw new ArgumentException($"[ZoneSectorShape] 구역 각도({endDeg - startDeg:0.#}°)가 경계 들임에 비해 너무 좁다.");

            // 모서리를 굴릴 길이 — 변 길이의 절반을 넘지 않게 줄인다 (좁은 구역에서도 도형이 뒤집히지 않도록)
            float edgeLength = Vector3.Distance(Polar(innerStart, innerRadius, y), Polar(outerStart, outerRadius, y));
            float innerArcLength = (innerEnd - innerStart) * Mathf.Deg2Rad * innerRadius;
            float round = Mathf.Max(0f, Mathf.Min(cornerRadius, Mathf.Min(edgeLength, innerArcLength) * 0.45f));
            float innerTrim = round / innerRadius * Mathf.Rad2Deg;   // 호 위에서 round만큼 물러나는 각도
            float outerTrim = round / outerRadius * Mathf.Rad2Deg;

            Vector3 outerStartCorner = Polar(outerStart, outerRadius, y);
            Vector3 outerEndCorner = Polar(outerEnd, outerRadius, y);
            Vector3 innerEndCorner = Polar(innerEnd, innerRadius, y);
            Vector3 innerStartCorner = Polar(innerStart, innerRadius, y);

            points.Clear();

            // 바깥 호
            AddArc(points, outerStart + outerTrim, outerEnd - outerTrim, outerRadius, arcStepDeg, y);
            // 바깥-끝 모서리 → 끝 경계 → 안쪽-끝 모서리
            AddCorner(points, outerEndCorner, Toward(outerEndCorner, innerEndCorner, round), round);
            AddCorner(points, innerEndCorner, Polar(innerEnd - innerTrim, innerRadius, y), round, Toward(innerEndCorner, outerEndCorner, round));
            // 안쪽 호 (끝 → 시작)
            AddArc(points, innerEnd - innerTrim, innerStart + innerTrim, innerRadius, arcStepDeg, y);
            // 안쪽-시작 모서리 → 시작 경계 → 바깥-시작 모서리
            AddCorner(points, innerStartCorner, Toward(innerStartCorner, outerStartCorner, round), round);
            AddCorner(points, outerStartCorner, Polar(outerStart + outerTrim, outerRadius, y), round, Toward(outerStartCorner, innerStartCorner, round));

            // 폐곡선 — 끝점이 첫 점과 겹치면 뺀다 (길이 0 선분은 선 이음새를 깨뜨린다)
            if (points.Count > 1 && Same(points[points.Count - 1], points[0])) points.RemoveAt(points.Count - 1);
        }

        /// <summary>극좌표 → XZ 평면 점.</summary>
        public static Vector3 Polar(float deg, float radius, float y)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * radius, y, Mathf.Sin(rad) * radius);
        }

        private static Vector3 Toward(Vector3 from, Vector3 to, float distance)
            => Vector3.MoveTowards(from, to, distance);

        private static void AddArc(List<Vector3> points, float fromDeg, float toDeg, float radius, float stepDeg, float y)
        {
            int segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) / stepDeg));
            for (int i = 0; i <= segments; i++)
                Append(points, Polar(Mathf.Lerp(fromDeg, toDeg, (float)i / segments), radius, y));
        }

        /// <summary>
        /// 모서리 하나를 굴린다 — 직전 점(points의 마지막)에서 corner를 조절점 삼아 exit까지 2차 베지어.
        /// entry를 주면 그 점을 먼저 찍고 거기서 시작한다 (직선 변을 타고 들어오는 모서리).
        /// </summary>
        private static void AddCorner(List<Vector3> points, Vector3 corner, Vector3 exit, float round, Vector3? entry = null)
        {
            if (entry.HasValue) Append(points, entry.Value);
            if (round <= 0f) { Append(points, corner); return; }

            Vector3 from = points[points.Count - 1];
            const int steps = 5;
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                float u = 1f - t;
                Append(points, u * u * from + 2f * u * t * corner + t * t * exit);
            }
        }

        /// <summary>직전 점과 겹치지 않을 때만 더한다.</summary>
        private static void Append(List<Vector3> points, Vector3 p)
        {
            if (points.Count > 0 && Same(points[points.Count - 1], p)) return;
            points.Add(p);
        }

        private static bool Same(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-8f;
    }
}
