using System.Collections.Generic;
using UnityEngine;

namespace HS.Tactics.Foundation.Geometry
{
    /// <summary>
    /// 한 스텝의 이동을 원형 장애물이 아니라 부풀린 직사각형 하나로 자르고, 한 번 미끄러진 결과이다.
    /// </summary>
    public readonly struct ObstacleClampedStep
    {
        /// <summary>이번 스텝이 실제로 도착한 평면 위치이다.</summary>
        public PlanarPosition Position { get; }

        /// <summary>
        /// 장애물에 막혀 원래 가려던 자리(<c>to</c>)에 못 미쳤으면 참이다.
        /// 미끄러져서 원래 자리에 그대로 도착했어도(드문 경우) 막힌 것으로 셈한다 — 부딪혔다는
        /// 사실 자체가 뜻이 있어서다.
        /// </summary>
        public bool WasBlocked { get; }

        /// <summary>결과를 만든다.</summary>
        /// <param name="position">이번 스텝이 실제로 도착한 위치이다.</param>
        /// <param name="wasBlocked">장애물에 막혔으면 참이다.</param>
        public ObstacleClampedStep(PlanarPosition position, bool wasBlocked)
        {
            Position = position;
            WasBlocked = wasBlocked;
        }
    }

    /// <summary>
    /// 원형 유닛이 직사각형 장애물을 지나쳐 가지 않도록 한 스텝의 이동을 자르고, 막힌 면을 따라
    /// 한 번 미끄러진다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>밀어내기가 아니다.</b> 여기서 미는 것은 유닛 자신의 이동 방향뿐이다. 장애물이나 다른 유닛을
    /// 밀어서 옮기지 않는다. 겹친 것을 빼내는 <see cref="PlanarGeometry.ResolveOverlap(PlanarCircle, PlanarRectangle)"/>와도
    /// 다르다 — 그것은 이미 겹친 상태를 푸는 것이고, 여기서는 다가오는 이동 자체를 접선으로 꺾는다.
    /// </para>
    /// <para>
    /// <b>정확한 민코프스키 합이 아니다.</b> 원형 유닛이 직사각형을 피해 가는 정확한 경계는 모서리가
    /// 둥글게 깎인 도형이다. 여기서는 반지름만큼 부풀린 <b>각진</b> 사각형으로 근사한다 — 네 모서리에서
    /// 최대 <c>반지름×(√2−1)</c>(약 0.41×반지름)만큼 더 넓게 막는다. <b>이 오차는 안전한 방향으로
    /// 치우친다</b> — 실제로 지나갈 수 있는 대각선 코너 자리를 아주 드물게 막을 뿐, 장애물을 뚫고
    /// 지나가는 쪽으로는 절대 치우치지 않는다. 정확한 라운드 사각형 교차를 새로 만드는 비용이
    /// 이 오차보다 크다고 보고 이대로 간다.
    /// </para>
    /// <para>
    /// <b>미끄러짐은 한 번만 시도한다.</b> 막히면 부딪힌 면의 접선 방향으로 남은 거리만큼 다시 이동을
    /// 시도하고, 그 재시도가 또 막히면 거기서 멈춘다. 여러 장애물에 반사를 거듭하지 않는다 — 좁은
    /// 틈에 여럿이 동시에 막는 상황은 규칙의 귀결이지 고쳐야 할 결함이 아니다.
    /// </para>
    /// <para>
    /// <b>다른 유닛도 같은 자르기에 함께 겨룬다.</b> <paramref name="obstacles"/>(사각형)와 다른 유닛의 원을
    /// 두 번의 독립된 패스로 나누지 않는다 — 나누면 나중에 도는 쪽이 먼저 쪽이 이미 지나친 자리를 못 잡아
    /// 진짜 더 가까운 막힘을 관통할 위험이 있다. 그래서 한 <c>nearestFraction</c> 비교 안에서 사각형과 원을
    /// 같이 겨루게 한다. 원-원 사이의 부풀리기(<see cref="InflateCircle"/>)는 사각형과 달리 <b>근사가 아니라
    /// 정확한 민코프스키 합</b>이다 — 두 반지름을 더하기만 하면 된다.
    /// </para>
    /// </remarks>
    public static class PlanarObstacleAvoidance
    {
        /// <summary>
        /// 자른 자리를 법선 쪽으로 밀어내는 거리(미터)이다.
        /// </summary>
        /// <remarks>
        /// 자른 자리는 그 장애물의 경계 위에 정확히 있다. 그대로 다음 판정(미끄러짐 재시도, 또는 다음
        /// 스텝)의 시작점으로 쓰면 <b>같은 장애물에 다시 "닿았다"로 잡혀 미끄러짐이 죽는다</b> — 시야
        /// 그래프의 꼭짓점 자기차단과 같은 문제다. 유닛 반지름과 무관한 계산상의 여유일 뿐이다.
        /// </remarks>
        private const float BoundaryNudge = 0.01f;

        /// <summary>
        /// 미끄러짐 판정에서 "사실상 0"으로 볼 거리(미터)이다.
        /// </summary>
        /// <remarks>
        /// 정면 충돌처럼 수학적으로는 정확히 0이어야 하는 값(접선, 남은 거리)도 실제 부동소수점
        /// 연산은 1e-6~1e-12 수준의 잔차를 남긴다. <see cref="Mathf.Epsilon"/>(표현 가능한 가장
        /// 작은 양수, 약 1.4e-45)은 그 잔차보다 수십 자릿수 작아 문턱값 구실을 못 한다 — 잔차가
        /// 그대로 "0이 아닌 값"으로 통과해, 정면 충돌인데도 그 잔차 방향으로 남은 거리만큼 실제로
        /// 미끄러진 것처럼 계산돼 버린다. 이 값은 그 잔차보다 확실히 크면서도 유닛 반지름이나
        /// <see cref="BoundaryNudge"/>(1cm)에 비하면 여전히 무의미하게 작다.
        /// </remarks>
        private const float SlideNegligibleDistance = 1e-4f;

        /// <summary>
        /// <see cref="SlideNegligibleDistance"/>를 제곱 크기(<c>SqrMagnitude</c>) 비교에 쓰기 위한 값이다.
        /// </summary>
        private const float SlideNegligibleSqrDistance = SlideNegligibleDistance * SlideNegligibleDistance;

        /// <summary>
        /// 반지름 <paramref name="radius"/>인 원이 <paramref name="from"/>에서 <paramref name="to"/>로
        /// 가려는 한 스텝을 장애물에 막혀 자르고, 필요하면 한 번 미끄러진 결과를 낸다.
        /// </summary>
        /// <param name="from">스텝을 시작하는 평면 위치이다.</param>
        /// <param name="to">막는 것이 없다면 도착할 평면 위치이다.</param>
        /// <param name="radius">유닛의 반지름(미터)이다.</param>
        /// <param name="obstacles">막는 직사각형 장애물 목록이며 비어 있거나 null이어도 된다.</param>
        /// <param name="nearbyUnits">
        /// 막을 수 있는 다른 유닛의 원 목록이며 비어 있거나 null이어도 된다. 각 원은 그 유닛 자신의
        /// 반지름 그대로다 — <paramref name="radius"/>와 더하는 것은 이 메서드가 한다.
        /// </param>
        /// <returns>이번 스텝의 실제 결과이다.</returns>
        public static ObstacleClampedStep Advance(
            PlanarPosition from,
            PlanarPosition to,
            float radius,
            IReadOnlyList<PlanarRectangle> obstacles,
            IReadOnlyList<PlanarCircle> nearbyUnits = null)
        {
            if (!TryClip(from, to, radius, obstacles, nearbyUnits, out var clippedPosition, out var normal))
            {
                return new ObstacleClampedStep(to, false);
            }

            return new ObstacleClampedStep(
                Slide(from, to, clippedPosition, normal, radius, obstacles, nearbyUnits), true);
        }

        /// <summary>
        /// 미끄러짐을 한 번 시도한다. 부딪힌 면의 법선을 모르거나(퇴화한 시작), 접선이 없거나
        /// (정면 충돌), 남은 거리가 없으면 자른 자리에서 그대로 멈춘다.
        /// </summary>
        private static PlanarPosition Slide(
            PlanarPosition originalFrom,
            PlanarPosition originalTo,
            PlanarPosition clippedPosition,
            PlanarPosition normal,
            float radius,
            IReadOnlyList<PlanarRectangle> obstacles,
            IReadOnlyList<PlanarCircle> nearbyUnits)
        {
            if (normal.SqrMagnitude <= SlideNegligibleSqrDistance)
            {
                return clippedPosition;
            }

            var attempted = originalTo - originalFrom;
            var remaining = attempted.Magnitude - PlanarPosition.Distance(originalFrom, clippedPosition);
            if (remaining <= SlideNegligibleDistance)
            {
                return clippedPosition;
            }

            var tangent = attempted - normal * PlanarPosition.Dot(attempted, normal);
            if (tangent.SqrMagnitude <= SlideNegligibleSqrDistance)
            {
                return clippedPosition;
            }

            var slideTarget = clippedPosition + tangent.Normalized * remaining;

            // 미끄러지는 이동도 다시 막힐 수 있다. 이번에는 자르기만 하고 더 미끄러지지 않는다 —
            // 그래야 여러 장애물이 겹친 구석에서 반사를 거듭하지 않는다.
            return TryClip(clippedPosition, slideTarget, radius, obstacles, nearbyUnits, out var secondPosition, out _)
                ? secondPosition
                : slideTarget;
        }

        /// <summary>
        /// 반지름만큼 부풀린 장애물과 다른 유닛의 원을 함께 겨뤄, 가장 먼저 막는 것의 진입 지점으로
        /// 이동을 자른다.
        /// </summary>
        /// <returns>어느 쪽에라도 막혔으면 참이다.</returns>
        private static bool TryClip(
            PlanarPosition from,
            PlanarPosition to,
            float radius,
            IReadOnlyList<PlanarRectangle> obstacles,
            IReadOnlyList<PlanarCircle> nearbyUnits,
            out PlanarPosition position,
            out PlanarPosition normal)
        {
            var nearestFraction = float.MaxValue;
            var nearestNormal = PlanarPosition.Zero;
            var blocked = false;

            if (obstacles != null)
            {
                foreach (var obstacle in obstacles)
                {
                    var inflated = Inflate(obstacle, radius);
                    if (!PlanarGeometry.TryGetEntry(inflated, from, to, out var fraction, out var entryNormal)
                        || fraction >= nearestFraction)
                    {
                        continue;
                    }

                    nearestFraction = fraction;
                    nearestNormal = entryNormal;
                    blocked = true;
                }
            }

            if (nearbyUnits != null)
            {
                foreach (var unit in nearbyUnits)
                {
                    var inflated = InflateCircle(unit, radius);
                    if (!PlanarGeometry.TryGetEntry(inflated, from, to, out var fraction, out var entryNormal)
                        || fraction >= nearestFraction)
                    {
                        continue;
                    }

                    nearestFraction = fraction;
                    nearestNormal = entryNormal;
                    blocked = true;
                }
            }

            if (!blocked)
            {
                position = to;
                normal = PlanarPosition.Zero;
                return false;
            }

            var clipped = from + (to - from) * nearestFraction;
            // 진입 지점 그대로 두지 않고 법선 쪽으로 살짝 밀어낸다 — 위 BoundaryNudge 참고.
            // 법선을 모르는 퇴화한 경우(시작이 이미 안)는 밀어낼 방향이 없으니 그대로 둔다.
            position = nearestNormal.SqrMagnitude > Mathf.Epsilon
                ? clipped + nearestNormal * BoundaryNudge
                : clipped;
            normal = nearestNormal;
            return true;
        }

        /// <summary>장애물을 유닛 반지름만큼 부풀린 사각형을 만든다. 각진 근사이며 안전한 방향으로 치우친다.</summary>
        private static PlanarRectangle Inflate(PlanarRectangle obstacle, float radius)
        {
            return new PlanarRectangle(
                obstacle.Center,
                new PlanarPosition(obstacle.HalfExtents.X + radius, obstacle.HalfExtents.Z + radius),
                obstacle.Forward);
        }

        /// <summary>
        /// 다른 유닛의 원을 자신의 반지름만큼 부풀린다. 두 원의 민코프스키 합은 반지름을 더한 것과
        /// 정확히 같으므로, 사각형과 달리 근사가 아니다.
        /// </summary>
        private static PlanarCircle InflateCircle(PlanarCircle unit, float radius)
        {
            return new PlanarCircle(unit.Center, unit.Radius + radius);
        }
    }
}
