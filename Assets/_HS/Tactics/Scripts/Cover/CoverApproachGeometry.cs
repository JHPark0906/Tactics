using HS.Tactics.Foundation.Geometry;
using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>엄폐 접근 경로와 도착 판정이 공유하는 유닛 중심의 충돌 영역이다.</summary>
    public static class CoverApproachGeometry
    {
        // 이동기의 충돌 경계 밀어내기(0.01m)보다 넉넉한 외부 목적지를 잡는다.
        public const float ClearancePadding = 0.02f;

        public static PlanarRectangle Expand(PlanarRectangle obstacle, float radius)
        {
            radius = Mathf.Max(0f, radius);
            return new PlanarRectangle(obstacle.Center,
                new PlanarPosition(obstacle.HalfExtents.X + radius, obstacle.HalfExtents.Z + radius),
                obstacle.Forward);
        }

        public static float DistanceOutside(PlanarRectangle obstacle, PlanarPosition position, float radius)
        {
            var expanded = Expand(obstacle, radius);
            var local = expanded.ToLocal(position);
            var offset = new PlanarPosition(
                Mathf.Max(0f, Mathf.Abs(local.X) - expanded.HalfExtents.X),
                Mathf.Max(0f, Mathf.Abs(local.Z) - expanded.HalfExtents.Z));
            return offset.Magnitude;
        }
    }
}
