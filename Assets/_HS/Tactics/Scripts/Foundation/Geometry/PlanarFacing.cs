using UnityEngine;

namespace HS.Tactics.Foundation.Geometry
{
    /// <summary>
    /// 바라보는 방향을 목표 방향으로 정해진 각도만큼만 돌린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이것은 조향이 아니다.</b> 어디로 갈지는 경로가 정하고, 여기서는 <b>이미 정해진 이동 방향을
    /// 바라보게만</b> 한다. 경로 계산에는 관여하지 않으므로 가는 길이 달라지지 않는다.
    /// </para>
    /// <para>
    /// <b>그런데 연출만은 아니다.</b> 사격은 대상이 정면에 있을 때만 나가므로, 바라보는 방향이 틀리면
    /// <b>사거리 안의 적에게도 조준을 마칠 때까지 쏘지 못한다.</b> 회전은 판단에 들어간다. 표적을 고르는
    /// 일은 반경만 보므로 여기에 닿지 않는다.
    /// </para>
    /// <para>
    /// <b>한 번에 다 돌지 않는다.</b> 각속도 한계 안에서만 돌므로 방향이 갑자기 튀지 않는다.
    /// </para>
    /// </remarks>
    public static class PlanarFacing
    {
        /// <summary>이보다 짧은 방향 벡터는 방향이 없는 것으로 본다.</summary>
        private const float MinimumSqrMagnitude = 1e-8f;

        /// <summary>
        /// 지금 바라보는 방향을 목표 방향 쪽으로 정해진 각도만큼만 돌린다.
        /// </summary>
        /// <param name="current">지금 바라보는 방향이며 길이가 0이면 목표를 그대로 본다.</param>
        /// <param name="desired">바라보려는 방향이며 길이가 0이면 지금 방향을 유지한다.</param>
        /// <param name="maxDegrees">이번에 돌 수 있는 최대 각도이며 0 이하이면 돌지 않는다.</param>
        /// <returns>돌고 난 뒤의 단위 방향이다.</returns>
        public static PlanarPosition Advance(PlanarPosition current, PlanarPosition desired, float maxDegrees)
        {
            if (desired.SqrMagnitude <= MinimumSqrMagnitude)
            {
                return current;
            }

            var target = desired.Normalized;
            if (current.SqrMagnitude <= MinimumSqrMagnitude)
            {
                // 아직 바라보는 방향이 없으면 돌 것도 없다. 목표를 그대로 본다.
                return target;
            }

            var from = current.Normalized;
            if (maxDegrees <= 0f)
            {
                return from;
            }

            var angle = SignedAngleDegrees(from, target);
            if (Mathf.Abs(angle) <= maxDegrees)
            {
                return target;
            }

            return Rotate(from, angle > 0f ? maxDegrees : -maxDegrees);
        }

        /// <summary>
        /// 한 방향에서 다른 방향까지의 부호 있는 각도를 구한다.
        /// </summary>
        /// <param name="from">기준 방향이다.</param>
        /// <param name="to">목표 방향이다.</param>
        /// <returns>-180에서 180 사이의 각도(도)이다.</returns>
        public static float SignedAngleDegrees(PlanarPosition from, PlanarPosition to)
        {
            var dot = from.X * to.X + from.Z * to.Z;
            var cross = from.X * to.Z - from.Z * to.X;
            return Mathf.Atan2(cross, dot) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 방향을 지정한 각도만큼 돌린다.
        /// </summary>
        /// <param name="direction">돌릴 방향이다.</param>
        /// <param name="degrees">돌릴 각도(도)이다.</param>
        /// <returns>돌고 난 뒤의 방향이다.</returns>
        public static PlanarPosition Rotate(PlanarPosition direction, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new PlanarPosition(
                direction.X * cos - direction.Z * sin,
                direction.X * sin + direction.Z * cos);
        }
    }
}
