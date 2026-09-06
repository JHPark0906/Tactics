using System.Collections.Generic;
using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 엄폐 지점 하나의 상태를 담는 순수 데이터이다.
    /// Unity 컴포넌트에 의존하지 않으므로 선택 규칙을 테스트에서 그대로 검증할 수 있다.
    /// </summary>
    public readonly struct CoverCandidate
    {
        /// <summary>엄폐 지점의 월드 좌표이다.</summary>
        public Vector3 Position { get; }

        /// <summary>다른 유닛이 쓰고 있는지 여부이다.</summary>
        public bool IsOccupied { get; }

        /// <summary>엄폐 지점 후보를 생성한다.</summary>
        /// <param name="position">엄폐 지점의 월드 좌표이다.</param>
        /// <param name="isOccupied">다른 유닛이 쓰고 있으면 true이다.</param>
        public CoverCandidate(Vector3 position, bool isOccupied)
        {
            Position = position;
            IsOccupied = isOccupied;
        }
    }

    /// <summary>
    /// 엄폐 지점 선택 규칙을 담은 순수 로직이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>선택 규칙.</b> 다음을 모두 만족하는 후보 중에서 고른다.
    /// 첫째 유닛에서 탐색 반경 안에 있고, 둘째 다른 유닛이 점유하고 있지 않으며,
    /// 셋째 그 자리에서 위협을 쏠 수 있고, 넷째 위협을 지나친 자리가 아니어야 한다.
    /// 조건을 만족하는 후보가 여럿이면 유닛에서 가장 가까운 것을 고르고,
    /// 거리가 같으면 먼저 전달된 후보를 고른다.
    /// </para>
    /// <para>
    /// <b>방향은 보지 않는다.</b> 전장이 선형이라 공격은 주로 앞에서 오므로 엄폐물이 어느 쪽을 막는지를
    /// 따지지 않는다. 엄폐의 이득은 자리에 있는 것 자체에서 나온다.
    /// </para>
    /// <para>
    /// 거리 비교는 수평면에서 수행한다. 일자형 맵에서 높이 차이는 엄폐 선택에 의미가 없고,
    /// 높이를 넣으면 같은 층의 지점이 위층 지점보다 멀게 계산될 수 있기 때문이다.
    /// </para>
    /// </remarks>
    public static class CoverSelection
    {
        /// <summary>선택된 후보가 없음을 나타내는 인덱스이다.</summary>
        public const int NoCoverIndex = -1;

        /// <summary>
        /// 직선거리로 걸러 낸 후보를 담아 재사용하는 목록이다.
        /// </summary>
        /// <remarks>
        /// 전투 로직은 한 스레드가 돌리므로 하나를 돌려 쓴다. 엄폐 고르기는 유닛마다 매 틱 일어나서
        /// 호출할 때마다 목록을 만들면 그만큼 쓰레기가 쌓인다.
        /// </remarks>
        private static readonly List<OrderedCandidate> _ordered = new();

        /// <summary>
        /// 선택 규칙에 맞는 후보 중 가장 알맞은 것의 인덱스를 찾는다.
        /// </summary>
        /// <param name="candidates">검사할 후보 목록이며 null이면 선택하지 않는다.</param>
        /// <param name="seekerPosition">엄폐를 찾는 유닛의 월드 좌표이다.</param>
        /// <param name="threatPosition">위협의 월드 좌표이다.</param>
        /// <param name="searchRadius">유닛에서 후보까지 허용하는 최대 수평 거리이다.</param>
        /// <param name="maxThreatDistance">
        /// 후보에서 위협까지 허용하는 최대 수평 거리이며, 기본값은 제한 없음이다.
        /// 사거리를 넘겨 "그 자리에서 쏠 수 있는 엄폐"만 고르게 하는 데 쓴다.
        /// </param>
        /// <returns>고른 후보의 인덱스이며 조건을 만족하는 후보가 없으면 <see cref="NoCoverIndex"/>이다.</returns>
        /// <remarks>
        /// <paramref name="maxThreatDistance"/>를 주면 위협에서 그 거리 안에 있는 후보만 고른다.
        /// 이 조건이 없으면 사거리 밖의 엄폐에 자리 잡고 앉아 아무도 쏘지 못하는 교착이 생긴다.
        /// 엄폐는 쏠 수 있을 때 의미가 있으므로, 쏠 수 없는 자리는 애초에 고르지 않는다.
        /// </remarks>
        public static int SelectBestIndex(
            IReadOnlyList<CoverCandidate> candidates,
            Vector3 seekerPosition,
            Vector3 threatPosition,
            float searchRadius,
            float maxThreatDistance = float.PositiveInfinity,
            CoverTravelDistance measureTravelDistance = null)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return NoCoverIndex;
            }

            CollectReachableByStraightLine(
                candidates, seekerPosition, threatPosition, searchRadius, maxThreatDistance);
            if (_ordered.Count == 0)
            {
                return NoCoverIndex;
            }

            if (measureTravelDistance == null)
            {
                // 이동 거리를 잴 수단이 없으면 직선거리로 고른다. 순수 규칙만 검증하는 자리가 이 경로를 쓴다.
                return _ordered[0].Index;
            }

            return SelectByTravelDistance(seekerPosition, measureTravelDistance);
        }

        /// <summary>
        /// 직선거리만으로 걸러 낼 수 있는 후보를 추려 가까운 차례로 늘어놓는다.
        /// </summary>
        /// <remarks>
        /// 반경·사거리·지나침은 좌표만으로 판정되므로 <b>이동 거리를 재기 전에</b> 먼저 건다.
        /// 이동 거리는 이 셋을 모두 통과한 후보에만 쓰인다.
        /// </remarks>
        private static void CollectReachableByStraightLine(
            IReadOnlyList<CoverCandidate> candidates,
            Vector3 seekerPosition,
            Vector3 threatPosition,
            float searchRadius,
            float maxThreatDistance)
        {
            _ordered.Clear();
            var maxDistance = Mathf.Max(0f, searchRadius);
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate.IsOccupied)
                {
                    continue;
                }

                var squaredDistance = Flatten(candidate.Position - seekerPosition).sqrMagnitude;
                if (squaredDistance > maxDistance * maxDistance)
                {
                    continue;
                }

                // 그 자리에서 위협을 쏠 수 없으면 엄폐가 아니라 정지가 되므로 후보에서 뺀다.
                if (!float.IsPositiveInfinity(maxThreatDistance))
                {
                    var threatDistanceLimit = Mathf.Max(0f, maxThreatDistance);
                    var squaredThreatDistance = Flatten(candidate.Position - threatPosition).sqrMagnitude;
                    if (squaredThreatDistance > threatDistanceLimit * threatDistanceLimit)
                    {
                        continue;
                    }
                }

                // 적을 지나친 자리는 엄폐가 아니라 돌격이다.
                if (IsBeyondThreat(candidate.Position, seekerPosition, threatPosition))
                {
                    continue;
                }

                _ordered.Add(new OrderedCandidate(index, candidate.Position, Mathf.Sqrt(squaredDistance)));
            }

            // 직선거리가 같으면 먼저 전달된 후보가 앞선다. 물리 질의 차례에 결과가 흔들리지 않게 한다.
            _ordered.Sort(CompareByStraightDistance);
        }

        /// <summary>
        /// 이동 거리가 가장 짧은 후보를 고른다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 실제로 걷는 곳은 엄폐물 중심보다 가까운 외부 접근 지점일 수 있다.
        /// 따라서 중심까지의 직선거리로 뒤 후보를 버리지 않고 각 후보의 실제 접근 거리를 비교한다.
        /// </para>
        /// <para>
        /// <b>닿을 수 없는 후보는 뺀다.</b> 아주 먼 것으로 다루지 않는다 — 그렇게 하면 다른 후보가
        /// 모두 사라졌을 때 <b>갈 수 없는 자리가 뽑혀 유닛이 그 자리에서 멈춘다.</b>
        /// "못 간다"는 먼 것이 아니라 후보가 아닌 것이다.
        /// </para>
        /// </remarks>
        /// <param name="seekerPosition">엄폐를 찾는 유닛의 월드 좌표이다.</param>
        /// <param name="measureTravelDistance">두 점 사이 이동 거리를 재는 수단이다.</param>
        /// <returns>고른 후보의 인덱스이며, 갈 수 있는 후보가 없으면 <see cref="NoCoverIndex"/>이다.</returns>
        private static int SelectByTravelDistance(
            Vector3 seekerPosition,
            CoverTravelDistance measureTravelDistance)
        {
            var bestIndex = NoCoverIndex;
            var bestTravelDistance = float.PositiveInfinity;
            for (var order = 0; order < _ordered.Count; order++)
            {
                var candidate = _ordered[order];
                // 실제 접근 지점은 엄폐물 중심보다 가까울 수 있으므로 중심까지의 직선거리는
                // 이동 거리의 하한이 아니다. 뒤 후보도 재야 큰 엄폐물을 잘못 건너뛰지 않는다.
                if (!measureTravelDistance(seekerPosition, candidate.Position, out var travelDistance))
                {
                    continue;
                }

                if (travelDistance < bestTravelDistance)
                {
                    bestTravelDistance = travelDistance;
                    bestIndex = candidate.Index;
                }
            }

            return bestIndex;
        }

        /// <summary>직선거리가 가까운 차례로, 같으면 먼저 전달된 차례로 늘어놓는다.</summary>
        /// <param name="left">비교할 후보이다.</param>
        /// <param name="right">비교 대상 후보이다.</param>
        /// <returns>정렬 비교 결과이다.</returns>
        private static int CompareByStraightDistance(OrderedCandidate left, OrderedCandidate right)
        {
            var byDistance = left.StraightDistance.CompareTo(right.StraightDistance);
            return byDistance != 0 ? byDistance : left.Index.CompareTo(right.Index);
        }

        /// <summary>직선거리로 걸러 낸 후보이며 검사 순서와 실제 이동 거리 비교에 쓰인다.</summary>
        private readonly struct OrderedCandidate
        {
            internal OrderedCandidate(int index, Vector3 position, float straightDistance)
            {
                Index = index;
                Position = position;
                StraightDistance = straightDistance;
            }

            /// <summary>원래 후보 목록에서의 자리이다.</summary>
            internal int Index { get; }

            /// <summary>엄폐 지점의 월드 좌표이다.</summary>
            internal Vector3 Position { get; }

            /// <summary>유닛에서 엄폐물 중심까지의 직선거리이며 후보 검사 순서를 정한다.</summary>
            internal float StraightDistance { get; }
        }

        /// <summary>엄폐 지점이 위협을 지나친 곳인지 판정한다.</summary>
        /// <remarks>유닛에서 위협으로 향하는 축에 투영해 비교한다. 옆으로 벌어진 자리는 허용한다.
        /// 후보 선택은 시야나 엄폐 방향을 추가로 검사하지 않는다.</remarks>
        /// <param name="coverPosition">엄폐 후보의 월드 위치이다.</param>
        /// <param name="seekerPosition">유닛의 월드 위치이다.</param>
        /// <param name="threatPosition">위협의 월드 위치이다.</param>
        /// <returns>위협을 지나친 자리이면 true이다.</returns>
        private static bool IsBeyondThreat(
            Vector3 coverPosition,
            Vector3 seekerPosition,
            Vector3 threatPosition)
        {
            var toThreat = Flatten(threatPosition - seekerPosition);
            var threatDistance = toThreat.magnitude;
            if (threatDistance <= Mathf.Epsilon)
            {
                return false;
            }

            var alongAxis = Vector3.Dot(Flatten(coverPosition - seekerPosition), toThreat / threatDistance);
            return alongAxis > threatDistance;
        }

        /// <summary>수평면에 투영한 벡터를 반환하며 길이가 0에 가까우면 <see cref="Vector3.zero"/>이다.</summary>
        /// <param name="value">투영할 벡터이다.</param>
        /// <returns>수평면에 투영한 벡터이다.</returns>
        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude <= Mathf.Epsilon ? Vector3.zero : value;
        }
    }
}
