using System.Collections.Generic;

namespace HS.Tactics.Foundation.Geometry
{
    /// <summary>
    /// 꺾임점 목록을 따라 주어진 거리만큼 나아간 결과이다.
    /// </summary>
    public readonly struct PathAdvanceResult
    {
        /// <summary>나아간 뒤의 평면 위치이다.</summary>
        public PlanarPosition Position { get; }

        /// <summary>다음에 향할 꺾임점의 자리이며, 목록 끝을 넘으면 더 갈 곳이 없다는 뜻이다.</summary>
        public int CornerIndex { get; }

        /// <summary>마지막 꺾임점까지 모두 지났는지 여부이다.</summary>
        public bool ReachedEnd { get; }

        /// <summary>나아간 결과를 만든다.</summary>
        /// <param name="position">나아간 뒤의 평면 위치이다.</param>
        /// <param name="cornerIndex">다음에 향할 꺾임점의 자리이다.</param>
        /// <param name="reachedEnd">마지막 꺾임점까지 지났는지 여부이다.</param>
        public PathAdvanceResult(PlanarPosition position, int cornerIndex, bool reachedEnd)
        {
            Position = position;
            CornerIndex = cornerIndex;
            ReachedEnd = reachedEnd;
        }
    }

    /// <summary>
    /// 꺾임점을 이어 만든 경로를 평면 위에서 따라간다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>경로를 얻는 일과 따라가는 일은 다르다.</b> 경로 계산에는 내비게이션 데이터가 필요하지만,
    /// 그렇게 얻은 꺾임점을 따라 정해진 거리만큼 나아가는 것은 <b>순수한 계산</b>이다.
    /// 그래서 여기서는 꺾임점 목록을 인자로 받아 내비게이션과 떼어 놓았고,
    /// 씬도 에이전트도 없이 확인할 수 있다.
    /// </para>
    /// <para>
    /// <b>조용히 틀리기 좋은 자리가 많다.</b> 한 스텝에 꺾임점을 여러 개 지나는 경우, 마지막 꺾임점을
    /// 지나고도 이동량이 남는 경우, 같은 자리에 꺾임점이 겹쳐 있는 경우가 그렇다.
    /// 이런 것들은 화면에서 "가끔 이상한 길로 간다"로만 드러나 원인을 찾기 어려우므로 여기서 못 박는다.
    /// </para>
    /// </remarks>
    public static class PlanarPathFollower
    {
        /// <summary>
        /// 꺾임점 목록을 따라 지정한 거리만큼 나아간다.
        /// </summary>
        /// <remarks>
        /// 이동량이 남았는데 꺾임점이 다 떨어지면 남은 몫은 버린다. 경로 끝을 넘어 계속 나아가면
        /// 갈 수 없는 곳으로 들어가기 때문이다.
        /// </remarks>
        /// <param name="start">출발할 평면 위치이다.</param>
        /// <param name="corners">따라갈 꺾임점 목록이며 비어 있으면 나아가지 않는다.</param>
        /// <param name="startCornerIndex">지금 향하고 있는 꺾임점의 자리이다.</param>
        /// <param name="distance">이번에 나아갈 거리이며 0 이하이면 나아가지 않는다.</param>
        /// <returns>나아간 결과이다.</returns>
        public static PathAdvanceResult Advance(
            PlanarPosition start,
            IReadOnlyList<PlanarPosition> corners,
            int startCornerIndex,
            float distance)
        {
            var cornerCount = corners?.Count ?? 0;
            var cornerIndex = startCornerIndex < 0 ? 0 : startCornerIndex;
            if (cornerCount == 0 || cornerIndex >= cornerCount)
            {
                return new PathAdvanceResult(start, cornerIndex, true);
            }

            if (distance <= 0f)
            {
                return new PathAdvanceResult(start, cornerIndex, false);
            }

            var current = start;
            var remaining = distance;

            // 한 스텝 안에 꺾임점을 여러 개 지날 수 있으므로 이동량이 다 떨어질 때까지 이어서 나아간다.
            while (remaining > 0f && cornerIndex < cornerCount)
            {
                var corner = corners[cornerIndex];
                var toCorner = corner - current;
                var toCornerDistance = toCorner.Magnitude;
                if (toCornerDistance <= remaining)
                {
                    current = corner;
                    remaining -= toCornerDistance;
                    cornerIndex++;
                    continue;
                }

                current += toCorner.Normalized * remaining;
                remaining = 0f;
            }

            return new PathAdvanceResult(current, cornerIndex, cornerIndex >= cornerCount);
        }

        /// <summary>
        /// 지금 자리에서 경로 끝까지 남은 거리를 경로를 따라 잰다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>목적지까지의 직선 거리와 다르다.</b> 경로가 꺾여 있으면 직선 거리가 언제나 더 짧으므로,
        /// 그것으로 감속 시점이나 도착 시점을 정하면 <b>실제보다 일찍</b> 판단한다.
        /// 남은 거리를 묻는 계산은 이 값을 써야 한다.
        /// </para>
        /// <para>
        /// <b>0은 두 가지를 뜻하므로 부르는 쪽이 갈라야 한다.</b> 길 끝에 닿았을 때도 0이고,
        /// <b>꺾임점 목록이 아직 비어 있을 때도 0</b>이다. 뒤쪽은 "다 왔다"가 아니라 "아직 모른다"인데
        /// 그대로 도착 판정에 넣으면 <b>경로를 받기도 전에 도착했다고 말한다.</b>
        /// 목록이 비었는지는 부르는 쪽이 먼저 확인한다.
        /// </para>
        /// </remarks>
        /// <param name="start">지금 자리이다.</param>
        /// <param name="corners">따라가는 꺾임점 목록이다.</param>
        /// <param name="startCornerIndex">지금 향하고 있는 꺾임점의 자리이다.</param>
        /// <returns>경로를 따라 남은 거리(미터)이며 갈 곳이 없으면 0이다.</returns>
        public static float RemainingDistance(
            PlanarPosition start,
            IReadOnlyList<PlanarPosition> corners,
            int startCornerIndex)
        {
            var cornerCount = corners?.Count ?? 0;
            var cornerIndex = startCornerIndex < 0 ? 0 : startCornerIndex;
            if (cornerCount == 0 || cornerIndex >= cornerCount)
            {
                return 0f;
            }

            var total = PlanarPosition.Distance(start, corners[cornerIndex]);
            for (var index = cornerIndex; index < cornerCount - 1; index++)
            {
                total += PlanarPosition.Distance(corners[index], corners[index + 1]);
            }

            return total;
        }
    }
}
