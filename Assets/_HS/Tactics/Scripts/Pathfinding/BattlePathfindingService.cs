using System.Collections.Generic;
using HS.Tactics.Character.Movement;
using HS.Tactics.Cover;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Pathfinding
{
    /// <summary>
    /// 살아 있는 엄폐물과 전장 경계로 시야 그래프를 들고 있다가, 유닛의 경로 계획 요청에 답한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>씬에 하나, 스테이지마다 새로.</b> <see cref="UnitSpatialRegistry"/>와 같은 이유로 씬 스코프
    /// 컴포넌트다 — 다음 스테이지의 유닛이 이전 씬의 죽은 서비스에 묶이지 않으려면, 컨테이너 등록이
    /// Singleton이 아니라 <c>SceneComponentLocator</c> + Transient여야 한다(<c>TacticsLifetimeScope</c> 참고).
    /// </para>
    /// <para>
    /// <b>엄폐물 목록은 처음 쓸 때 한 번 모은다.</b> <see cref="CoverSensor"/>도 독자적으로 씬의 엄폐물을
    /// 모으지만 그것은 유닛마다 다른 「어디로 뛸까」 후보 목록이고, 이쪽은 전장 전체에 공통인
    /// 「장애물이 어디 있나」이다 — 성격이 달라 하나로 합치지 않는다. 전투 중 새 엄폐물이 놓이는
    /// 기능은 지금 이 프로젝트에 없으므로, 처음 모은 목록 밖의 엄폐물은 이 서비스가 모른다.
    /// </para>
    /// <para>
    /// <b>부서지면 다시 굽는다.</b> 각 엄폐물의 <see cref="CoverPoint.Destroyed"/>를 구독해 두고, 발행되면
    /// 그 자리에서 즉시 새 그래프를 짓는다. 전장 규모가 작아 매번 다시 굽는 비용은 문제되지 않는다고 보고
    /// 여럿이 한 틱에 같이 부서져도 그때마다 다시 굽는 것으로 충분하다 — 묶어서 한 번만 굽는 최적화는
    /// 지금 필요하지 않다.
    /// </para>
    /// <para>
    /// <b>이미 걷고 있는 유닛은 다시 계획시키지 않는다.</b> <see cref="PlanarCharacterMover"/>는 받은 꺾임점을
    /// 자기 안에 복사해 들고 걸을 뿐 이 서비스나 그래프를 실시간으로 참조하지 않는다. 장애물이 하나
    /// 없어지는 것은 지나갈 수 있는 자리를 넓힐 뿐 좁히지 않으므로, 옛 꺾임점은 다시 굽고 나서도 여전히
    /// 막히지 않은 길이다 — 최적은 아닐 수 있어도 틀리지 않는다. 새 그래프는 다음 <c>MoveTo</c>·정체로 인한
    /// 재계획에서만 쓰인다.
    /// </para>
    /// <para>
    /// <b><see cref="LiveObstacles"/>도 같은 이유로 다시 새 목록을 내주지 않고 제자리에서 채운다.</b>
    /// <see cref="PlanarCharacterMover.SetObstacles"/>는 한 번만 불려 그 목록 참조를 그대로 붙든다. 다시
    /// 구울 때마다 새 목록 인스턴스를 주면 이미 붙든 참조는 낡은 채로 남으므로, 이 서비스는 언제나 같은
    /// 목록 인스턴스를 비우고 다시 채운다 — 그러면 걸음 자르기도 부서진 장애물을 더는 피하지 않는다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattlePathfindingService : MonoBehaviour
    {
        private readonly List<CoverPoint> _coverPoints = new();
        private readonly List<PlanarRectangle> _liveObstacles = new();
        private readonly List<Vector3> _travelCornersBuffer = new();
        private readonly Dictionary<float, ClearanceGraph> _clearanceGraphs = new();
        private ObstacleVisibilityGraph _graph;
        private BattleBounds _bounds;
        private bool _isGraphBuilt;
        private bool _hasWarnedAboutMissingBounds;

        /// <summary>
        /// 지금 살아 있는 엄폐물의 영역 목록이다. 걸음 자르기가 이 목록 참조를 그대로 붙들어 쓰므로,
        /// 다시 구울 때도 같은 목록 인스턴스를 비우고 다시 채운다.
        /// </summary>
        public IReadOnlyList<PlanarRectangle> LiveObstacles => _liveObstacles;

        /// <summary>
        /// <see cref="PathPlanner"/> 계약에 맞는 경로 계획이다. 유닛 조립이 이동기에 그대로 끼운다.
        /// </summary>
        /// <remarks>
        /// <b>목적지가 살아 있는 엄폐물 하나뿐이면 그 장애물에는 안 막힌다.</b> 엄폐물은 스스로도
        /// 장애물이면서 유닛이 다가가려는 자리이기도 하다 — 자기 자신에게 막혔다고 하면 아무도
        /// 그 엄폐물에 갈 수 없다. 목적지가 이 서비스가 아는 장애물 중 <b>정확히 하나</b>에 걸릴 때만
        /// 그 장애물을 빼고 계획한다. 둘 이상에 걸리면(겹친 엄폐물) 어느 쪽이 목적지의 주인인지 가릴
        /// 수 없으므로 막힌 것으로 본다. 이 서비스의 장애물은 전부 엄폐물이므로(<see cref="_liveObstacles"/>),
        /// 이 완화가 엄폐물이 아닌 다른 종류의 진짜 막힌 목적지를 실수로 통과시킬 일은 없다.
        /// </remarks>
        /// <param name="from">출발 월드 좌표이다.</param>
        /// <param name="destination">목적지 월드 좌표이다.</param>
        /// <param name="corners">꺾임점을 채울 목록이며, 부르기 전에 비워져 있지 않을 수 있다.</param>
        /// <returns>경로를 찾았으면 참이다.</returns>
        public bool Plan(Vector3 from, Vector3 destination, List<Vector3> corners)
        {
            return Plan(from, destination, corners, 0f);
        }

        /// <summary>유닛 반지름을 확보한 경로를 찾는다. 엄폐물 중심은 충돌 영역 밖의 접근 지점으로 바꾼다.</summary>
        public bool Plan(Vector3 from, Vector3 destination, List<Vector3> corners, float radius)
        {
            corners.Clear();
            if (!EnsureGraph())
            {
                return false;
            }

            var start = PlanarPosition.FromWorld(from);
            var goal = PlanarPosition.FromWorld(destination);
            radius = Mathf.Max(0f, radius);
            if (radius > 0f)
            {
                var clearance = GetClearanceGraph(radius);
                if (!clearance.IsValid)
                {
                    return false;
                }

                if (TryFindSoleContainingObstacle(goal, out var cover))
                {
                    return PlanCoverApproach(clearance, start, cover, from.y, corners);
                }

                if (!clearance.Graph.TryFindPath(clearance.Bounds, start, goal, out var clearancePath))
                {
                    return false;
                }

                CopyCorners(clearancePath, from.y, corners);
                return corners.Count > 0;
            }

            PlanarRectangle? excludeFromGoal = TryFindSoleContainingObstacle(goal, out var goalObstacle)
                ? goalObstacle
                : null;
            if (!_graph.TryFindPath(_bounds.Bounds, start, goal, out var path, excludeFromGoal))
            {
                return false;
            }

            // 이 프로젝트에 다층 지형 개념이 없어(바닥은 평면으로 본다) 높이는 출발점의 것을 그대로 쓴다.
            // PlanarCharacterMover.AdoptCorners도 꺾임점 사이를 보간하지 않고 인덱스 하나의 높이만
            // 쓰므로, 그 이상의 신뢰도를 여기서 새로 만들 필요가 없다.
            var height = from.y;
            foreach (var corner in path)
            {
                corners.Add(corner.ToWorld(height));
            }

            return corners.Count > 0;
        }

        /// <summary>
        /// 원하는 자리가 전장 경계 안이고 장애물 밖인지 판정한다.
        /// 유효하면 요청한 좌표를 그대로 반환하고, 아니면 실패한다. 주변의 다른 좌표로 보정하지 않는다.
        /// </summary>
        public bool TryResolveReachable(Vector3 desiredPosition, out Vector3 resolvedPosition)
        {
            return TryResolveReachable(desiredPosition, out resolvedPosition, 0f);
        }

        /// <summary>이 반지름을 가진 유닛 전체가 경계 안, 장애물 밖에 놓이는지 검사한다.</summary>
        public bool TryResolveReachable(Vector3 desiredPosition, out Vector3 resolvedPosition, float radius)
        {
            resolvedPosition = desiredPosition;
            if (!EnsureGraph())
            {
                return false;
            }

            var planar = PlanarPosition.FromWorld(desiredPosition);
            radius = Mathf.Max(0f, radius);
            var clearance = radius > 0f ? GetClearanceGraph(radius) : null;
            if (clearance != null && !clearance.IsValid)
            {
                return false;
            }

            if (!PlanarGeometry.Contains(clearance?.Bounds ?? _bounds.Bounds, planar))
            {
                return false;
            }

            foreach (var obstacle in clearance?.Obstacles ?? _liveObstacles)
            {
                if (PlanarGeometry.Contains(obstacle, planar))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// <c>CoverTravelDistance</c> 계약에 맞는 이동 거리 측정이다. 이 서비스의 그래프로 두 자리
        /// 사이의 실제 경로 길이를 잰다.
        /// </summary>
        /// <param name="from">출발 월드 좌표이다.</param>
        /// <param name="to">도착 월드 좌표이다.</param>
        /// <param name="distance">잰 이동 거리이며, 갈 수 없으면 0이다.</param>
        /// <returns>끝까지 갈 수 있으면 참이다.</returns>
        public bool TryMeasureTravelDistance(Vector3 from, Vector3 to, out float distance)
        {
            return TryMeasureTravelDistance(from, to, out distance, 0f);
        }

        /// <summary>이 유닛의 실제 접근 지점까지 걷는 거리를 잰다.</summary>
        public bool TryMeasureTravelDistance(Vector3 from, Vector3 to, out float distance, float radius)
        {
            distance = 0f;
            if (!Plan(from, to, _travelCornersBuffer, radius))
            {
                return false;
            }

            for (var index = 1; index < _travelCornersBuffer.Count; index++)
            {
                distance += Vector3.Distance(_travelCornersBuffer[index - 1], _travelCornersBuffer[index]);
            }

            return true;
        }

        /// <summary>그래프가 없으면 짓는다. 전장 경계가 씬에 없으면 지을 수 없다.</summary>
        /// <returns>그래프가 준비되어 있으면(방금 지었든 이미 있었든) 참이다.</returns>
        private bool EnsureGraph()
        {
            if (_isGraphBuilt)
            {
                return _bounds != null;
            }

            _bounds = FindFirstObjectByType<BattleBounds>();
            _coverPoints.AddRange(FindObjectsByType<CoverPoint>(FindObjectsSortMode.None));
            foreach (var coverPoint in _coverPoints)
            {
                coverPoint.Destroyed += RebuildGraph;
            }

            _isGraphBuilt = true;
            if (_bounds == null)
            {
                if (!_hasWarnedAboutMissingBounds)
                {
                    Debug.LogWarning(
                        $"[{nameof(BattlePathfindingService)}] 씬에 {nameof(BattleBounds)}가 없어 경로를 계획할 수 없다.",
                        this);
                    _hasWarnedAboutMissingBounds = true;
                }

                return false;
            }

            RebuildGraph();
            return true;
        }

        /// <summary>지금 살아 있는 엄폐물로 그래프와 <see cref="LiveObstacles"/>를 다시 짓는다.</summary>
        private void RebuildGraph()
        {
            _clearanceGraphs.Clear();
            _liveObstacles.Clear();
            for (var i = _coverPoints.Count - 1; i >= 0; i--)
            {
                var coverPoint = _coverPoints[i];
                if (coverPoint == null)
                {
                    // 구독 해제 없이 파괴됐을 수 있다. 다음부터는 후보에서 뺀다.
                    _coverPoints.RemoveAt(i);
                    continue;
                }

                if (coverPoint.IsDestroyed)
                {
                    continue;
                }

                _liveObstacles.Add(coverPoint.Bounds);
            }

            if (_bounds != null)
            {
                _graph = new ObstacleVisibilityGraph(_liveObstacles, _bounds.Bounds);
            }
        }

        private ClearanceGraph GetClearanceGraph(float radius)
        {
            if (_clearanceGraphs.TryGetValue(radius, out var cached))
            {
                return cached;
            }

            var bounds = _bounds.Bounds;
            var extents = new PlanarPosition(bounds.HalfExtents.X - radius, bounds.HalfExtents.Z - radius);
            var insetBounds = new PlanarRectangle(bounds.Center, extents, bounds.Forward);
            var obstacles = new List<PlanarRectangle>(_liveObstacles.Count);
            foreach (var obstacle in _liveObstacles)
            {
                obstacles.Add(CoverApproachGeometry.Expand(obstacle, radius));
            }

            var result = new ClearanceGraph(insetBounds, obstacles, extents.X > 0f && extents.Z > 0f, radius);
            _clearanceGraphs.Add(radius, result);
            return result;
        }

        private static bool PlanCoverApproach(
            ClearanceGraph clearance, PlanarPosition start, PlanarRectangle cover, float height, List<Vector3> corners)
        {
            var expanded = CoverApproachGeometry.Expand(cover, clearance.Radius);
            // 가장 가까운 면의 투영과 각 면의 끝점을 함께 살핀다. 다른 엄폐물이 한쪽을 막아도
            // 남은 면으로 접근할 수 있으며, 모든 후보는 동일한 충돌 그래프를 통과해야 한다.
            var localStart = expanded.ToLocal(start);
            var x = Mathf.Clamp(localStart.X, -expanded.HalfExtents.X, expanded.HalfExtents.X);
            var z = Mathf.Clamp(localStart.Z, -expanded.HalfExtents.Z, expanded.HalfExtents.Z);
            var halfX = expanded.HalfExtents.X + CoverApproachGeometry.ClearancePadding;
            var halfZ = expanded.HalfExtents.Z + CoverApproachGeometry.ClearancePadding;
            var candidates = new[]
            {
                new PlanarPosition(-halfX, z), new PlanarPosition(halfX, z),
                new PlanarPosition(x, -halfZ), new PlanarPosition(x, halfZ),
                new PlanarPosition(-halfX, -halfZ), new PlanarPosition(-halfX, halfZ),
                new PlanarPosition(halfX, -halfZ), new PlanarPosition(halfX, halfZ)
            };
            IReadOnlyList<PlanarPosition> best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var candidate in candidates)
            {
                var destination = expanded.ToWorld(candidate);
                if (!clearance.Graph.TryFindPath(clearance.Bounds, start, destination, out var path))
                {
                    continue;
                }

                var distance = PlanarPathFollower.RemainingDistance(start, path, 1);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = path;
                }
            }

            if (best == null)
            {
                return false;
            }

            CopyCorners(best, height, corners);
            return true;
        }

        private static void CopyCorners(IReadOnlyList<PlanarPosition> path, float height, List<Vector3> corners)
        {
            foreach (var corner in path)
            {
                corners.Add(corner.ToWorld(height));
            }
        }

        private sealed class ClearanceGraph
        {
            public ClearanceGraph(PlanarRectangle bounds, List<PlanarRectangle> obstacles, bool isValid, float radius)
            {
                Bounds = bounds;
                Obstacles = obstacles;
                IsValid = isValid;
                Radius = radius;
                Graph = isValid ? new ObstacleVisibilityGraph(obstacles, bounds) : null;
            }

            public PlanarRectangle Bounds { get; }
            public List<PlanarRectangle> Obstacles { get; }
            public bool IsValid { get; }
            public float Radius { get; }
            public ObstacleVisibilityGraph Graph { get; }
        }

        /// <summary>
        /// 이 자리가 걸리는 살아 있는 장애물이 하나뿐이면 참을 돌려주고 그 장애물을 담는다.
        /// </summary>
        /// <remarks>둘 이상의 장애물에 걸리면 거짓이다 — 어느 것이 이 자리의 주인인지 가릴 수 없다.</remarks>
        /// <param name="position">확인할 평면 좌표이다.</param>
        /// <param name="obstacle">걸리는 장애물이 하나뿐이면 그 장애물이다.</param>
        /// <returns>장애물이 하나뿐이면 참이다.</returns>
        private bool TryFindSoleContainingObstacle(PlanarPosition position, out PlanarRectangle obstacle)
        {
            obstacle = default;
            var found = false;
            foreach (var candidate in _liveObstacles)
            {
                if (!PlanarGeometry.Contains(candidate, position))
                {
                    continue;
                }

                if (found)
                {
                    obstacle = default;
                    return false;
                }

                obstacle = candidate;
                found = true;
            }

            return found;
        }
    }
}
