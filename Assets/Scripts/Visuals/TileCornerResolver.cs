using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 타일 윗면 4코너 월드 좌표 계산 유틸 (단일 출처).
    /// TileSkillPreviewManager / PassiveRangeIndicator 등이 공유한다.
    /// </summary>
    public static class TileCornerResolver
    {
        /// <summary>타일 윗면 4코너를 월드 좌표로 반환. elevation만큼 위로 띄운다.</summary>
        public static Vector3[] ResolveTopCorners(TileData tile, float elevation)
        {
            var mf = tile.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds local = mf.sharedMesh.bounds;
                Transform t  = mf.transform;
                float topY   = local.center.y + local.extents.y;
                float ex     = local.extents.x;
                float ez     = local.extents.z;
                Vector3 c    = local.center;

                Vector3[] localCorners =
                {
                    c + new Vector3(-ex, topY - c.y,  ez),
                    c + new Vector3( ex, topY - c.y,  ez),
                    c + new Vector3( ex, topY - c.y, -ez),
                    c + new Vector3(-ex, topY - c.y, -ez),
                };

                var corners = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    corners[i] = t.TransformPoint(localCorners[i]);
                    corners[i] += Vector3.up * elevation;
                }
                return corners;
            }

            // 메시가 없는 타일 폴백: 고정 크기 사각형
            Vector3 center = tile.Position + Vector3.up * elevation;
            const float h = 0.75f;
            return new[]
            {
                center + new Vector3(-h, 0,  h),
                center + new Vector3( h, 0,  h),
                center + new Vector3( h, 0, -h),
                center + new Vector3(-h, 0, -h),
            };
        }
    }
}
