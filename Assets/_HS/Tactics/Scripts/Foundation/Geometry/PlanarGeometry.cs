using UnityEngine;

namespace HS.Tactics.Foundation.Geometry
{
    /// <summary>
    /// 수평면 위에서 필요한 겹침과 가림 판정을 모아 둔다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 물리 엔진을 쓰지 않는가.</b> 여기에 필요한 판정은 셋뿐이다 — 원과 원, 원과 회전한 사각형,
    /// 선분과 회전한 사각형. 마찰도 질량도 관절도 연속 충돌도 쓰지 않으므로 물리 엔진을 들여올 규모가 아니다.
    /// 짧은 순수 함수로 두면 시계도 씬도 없이 검증할 수 있고, 결과가 실행 환경에 따라 달라지지 않는다.
    /// </para>
    /// <para>
    /// <b>겹침을 푸는 방향은 언제나 정해져 있다.</b> 두 원의 중심이 정확히 같아 방향을 정할 수 없는 순간에도
    /// 미리 정해 둔 축으로 민다. 그 자리에서 임의로 고르면 실행할 때마다 다른 결과가 나온다.
    /// </para>
    /// </remarks>
    public static class PlanarGeometry
    {
        /// <summary>
        /// 두 원의 중심이 겹쳐 방향을 정할 수 없을 때 밀어낼 방향이다.
        /// 어느 쪽이든 상관없지만 <b>언제나 같아야</b> 하므로 고정해 둔다.
        /// </summary>
        private static readonly PlanarPosition DegenerateSeparationAxis = new(1f, 0f);

        /// <summary>
        /// 두 점이나 방향이 사실상 같거나 움직이지 않는 것으로 볼 거리(미터)이다.
        /// 임의 좌표를 뺀 결과에는 부동소수 잔차가 남을 수 있으므로 Mathf.Epsilon보다 큰 문턱값을 쓴다.
        /// 이 값은 미터 단위의 전장 크기와 유닛 반지름에 비해 작다.
        /// </summary>
        private const float NegligibleDistance = 1e-4f;

        /// <summary>
        /// <see cref="NegligibleDistance"/>를 제곱 크기(<c>SqrMagnitude</c>) 비교에 쓰기 위한 값이다.
        /// </summary>
        private const float NegligibleSqrDistance = NegligibleDistance * NegligibleDistance;

        /// <summary>두 원이 겹치는지 확인한다. 스치듯 닿기만 하면 겹친 것으로 보지 않는다.</summary>
        /// <param name="first">첫째 원이다.</param>
        /// <param name="second">둘째 원이다.</param>
        /// <returns>겹치면 true이다.</returns>
        public static bool Overlaps(PlanarCircle first, PlanarCircle second)
        {
            var radiusSum = first.Radius + second.Radius;
            return PlanarPosition.SqrDistance(first.Center, second.Center) < radiusSum * radiusSum;
        }

        /// <summary>
        /// 겹친 두 원을 떼어 놓기 위해 첫째 원을 밀어야 할 이동량을 구한다.
        /// 겹치지 않았으면 이동량이 없다.
        /// </summary>
        /// <remarks>
        /// 둘째 원은 움직이지 않는다고 본다. 양쪽을 함께 밀어야 하면 호출부가 이동량을 나눠 쓴다.
        /// </remarks>
        /// <param name="first">밀어낼 원이다.</param>
        /// <param name="second">기준이 되는 원이다.</param>
        /// <returns>첫째 원에 더할 이동량이다.</returns>
        public static PlanarPosition ResolveOverlap(PlanarCircle first, PlanarCircle second)
        {
            var delta = first.Center - second.Center;
            var radiusSum = first.Radius + second.Radius;
            var sqrDistance = delta.SqrMagnitude;
            if (sqrDistance >= radiusSum * radiusSum)
            {
                return PlanarPosition.Zero;
            }

            if (sqrDistance <= NegligibleSqrDistance)
            {
                // 중심이 겹쳐 방향을 정할 수 없다. 임의로 고르면 결과가 흔들리므로 정해 둔 축으로 민다.
                return DegenerateSeparationAxis * radiusSum;
            }

            var distance = Mathf.Sqrt(sqrDistance);
            return delta * ((radiusSum - distance) / distance);
        }

        /// <summary>
        /// 회전한 직사각형 위에서 지정한 점에 가장 가까운 좌표를 구한다.
        /// 점이 직사각형 안이면 그 점을 그대로 돌려준다.
        /// </summary>
        /// <param name="rectangle">기준이 되는 직사각형이다.</param>
        /// <param name="point">가까운 자리를 찾을 좌표이다.</param>
        /// <returns>직사각형 위 또는 안의 가장 가까운 좌표이다.</returns>
        public static PlanarPosition ClosestPoint(PlanarRectangle rectangle, PlanarPosition point)
        {
            var local = rectangle.ToLocal(point);
            var clamped = new PlanarPosition(
                Mathf.Clamp(local.X, -rectangle.HalfExtents.X, rectangle.HalfExtents.X),
                Mathf.Clamp(local.Z, -rectangle.HalfExtents.Z, rectangle.HalfExtents.Z));
            return rectangle.ToWorld(clamped);
        }

        /// <summary>지정한 좌표가 직사각형 안에 있는지 확인한다. 변 위도 안으로 본다.</summary>
        /// <param name="rectangle">확인할 직사각형이다.</param>
        /// <param name="point">확인할 좌표이다.</param>
        /// <returns>안에 있으면 true이다.</returns>
        public static bool Contains(PlanarRectangle rectangle, PlanarPosition point)
        {
            var local = rectangle.ToLocal(point);
            return Mathf.Abs(local.X) <= rectangle.HalfExtents.X
                && Mathf.Abs(local.Z) <= rectangle.HalfExtents.Z;
        }

        /// <summary>회전한 직사각형의 네 꼭짓점을 돌려준다.</summary>
        /// <remarks>
        /// 순서는 <c>(-X,-Z) → (+X,-Z) → (+X,+Z) → (-X,+Z)</c>이며, 직사각형이 회전하지 않았을 때
        /// 시계 방향이다. 시야 그래프가 꼭짓점을 노드로 삼을 때 이 순서로 받는다.
        /// </remarks>
        /// <param name="rectangle">꼭짓점을 구할 직사각형이다.</param>
        /// <returns>네 꼭짓점의 평면 좌표이다.</returns>
        public static PlanarPosition[] GetCorners(PlanarRectangle rectangle)
        {
            var half = rectangle.HalfExtents;
            return new[]
            {
                rectangle.ToWorld(new PlanarPosition(-half.X, -half.Z)),
                rectangle.ToWorld(new PlanarPosition(half.X, -half.Z)),
                rectangle.ToWorld(new PlanarPosition(half.X, half.Z)),
                rectangle.ToWorld(new PlanarPosition(-half.X, half.Z)),
            };
        }

        /// <summary>원과 회전한 직사각형이 겹치는지 확인한다.</summary>
        /// <param name="circle">확인할 원이다.</param>
        /// <param name="rectangle">확인할 직사각형이다.</param>
        /// <returns>겹치면 true이다.</returns>
        public static bool Overlaps(PlanarCircle circle, PlanarRectangle rectangle)
        {
            if (Contains(rectangle, circle.Center))
            {
                return true;
            }

            var closest = ClosestPoint(rectangle, circle.Center);
            return PlanarPosition.SqrDistance(circle.Center, closest) < circle.Radius * circle.Radius;
        }

        /// <summary>
        /// 겹친 원을 직사각형 밖으로 밀어내기 위한 이동량을 구한다. 겹치지 않았으면 이동량이 없다.
        /// </summary>
        /// <remarks>
        /// 원의 중심이 직사각형 안에 들어가 버리면 가장 가까운 점이 자기 자신이라 방향을 얻을 수 없다.
        /// 그때는 <b>가장 얕게 빠져나갈 수 있는 변</b>으로 밀어낸다. 그래야 유닛이 엄폐물을 가로질러
        /// 반대편으로 튕겨 나가지 않는다.
        /// </remarks>
        /// <param name="circle">밀어낼 원이다.</param>
        /// <param name="rectangle">기준이 되는 직사각형이다.</param>
        /// <returns>원에 더할 이동량이다.</returns>
        public static PlanarPosition ResolveOverlap(PlanarCircle circle, PlanarRectangle rectangle)
        {
            var local = rectangle.ToLocal(circle.Center);
            var halfExtents = rectangle.HalfExtents;
            var isInside = Mathf.Abs(local.X) <= halfExtents.X && Mathf.Abs(local.Z) <= halfExtents.Z;
            if (isInside)
            {
                var pushX = halfExtents.X - Mathf.Abs(local.X) + circle.Radius;
                var pushZ = halfExtents.Z - Mathf.Abs(local.Z) + circle.Radius;
                var localPush = pushX <= pushZ
                    ? new PlanarPosition(Mathf.Sign(local.X == 0f ? 1f : local.X) * pushX, 0f)
                    : new PlanarPosition(0f, Mathf.Sign(local.Z == 0f ? 1f : local.Z) * pushZ);
                return rectangle.Right * localPush.X + rectangle.Forward * localPush.Z;
            }

            var clamped = new PlanarPosition(
                Mathf.Clamp(local.X, -halfExtents.X, halfExtents.X),
                Mathf.Clamp(local.Z, -halfExtents.Z, halfExtents.Z));
            var localDelta = local - clamped;
            var sqrDistance = localDelta.SqrMagnitude;
            if (sqrDistance >= circle.Radius * circle.Radius)
            {
                return PlanarPosition.Zero;
            }

            var distance = Mathf.Sqrt(sqrDistance);
            var localPushOut = localDelta * ((circle.Radius - distance) / distance);
            return rectangle.Right * localPushOut.X + rectangle.Forward * localPushOut.Z;
        }

        /// <summary>
        /// 선분이 회전한 직사각형을 지나는지 확인한다. 시야가 엄폐물에 가리는지 판정하는 데 쓴다.
        /// </summary>
        /// <remarks>
        /// 직사각형의 국소 좌표로 옮겨 축에 정렬된 상자로 만든 뒤, 각 축이 허용하는 구간을 좁혀 나간다.
        /// 구간이 비면 지나지 않는 것이다. 제곱근을 쓰지 않으므로 값이 환경에 따라 흔들릴 여지가 적다.
        /// </remarks>
        /// <param name="rectangle">가림 판정에 쓸 직사각형이다.</param>
        /// <param name="from">선분의 시작 좌표이다.</param>
        /// <param name="to">선분의 끝 좌표이다.</param>
        /// <returns>선분이 직사각형과 만나면 true이다.</returns>
        public static bool IntersectsSegment(PlanarRectangle rectangle, PlanarPosition from, PlanarPosition to)
        {
            var localFrom = rectangle.ToLocal(from);
            var localTo = rectangle.ToLocal(to);
            var direction = localTo - localFrom;
            var halfExtents = rectangle.HalfExtents;

            var entry = 0f;
            var exit = 1f;
            var entryAxisIsX = false;
            var hasEntryAxis = false;
            if (!NarrowRange(localFrom.X, direction.X, halfExtents.X, true, ref entry, ref exit, ref entryAxisIsX, ref hasEntryAxis))
            {
                return false;
            }

            return NarrowRange(localFrom.Z, direction.Z, halfExtents.Z, false, ref entry, ref exit, ref entryAxisIsX, ref hasEntryAxis);
        }

        /// <summary>
        /// 선분이 회전한 직사각형에 들어가는 지점과 그 자리의 바깥쪽 법선을 구한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="IntersectsSegment"/>와 같은 계산이지만 어디서 들어가는지까지 답한다. 이동이
        /// 장애물에 막혔을 때 걸음을 그 자리에서 자르고, 막은 면을 따라 미끄러질 방향을 정하는 데 쓴다.
        /// </para>
        /// <para>
        /// 🔴 <b>시작이 이미 직사각형 안이면 들어가는 비율은 0이고 법선은 <see cref="PlanarPosition.Zero"/>이다.</b>
        /// 어느 면으로 들어왔는지 정할 수 없는 자리이기 때문이다. 꼭짓점을 밀어내는 여유나 부동소수 오차로
        /// 시작이 경계에 살짝 걸쳐 있는 퇴화한 경우가 이렇게 나온다 — 막힌 것으로는 보되(반환값 true),
        /// 미끄러질 방향은 호출부가 다른 수단으로 정해야 한다.
        /// </para>
        /// </remarks>
        /// <param name="rectangle">확인할 직사각형이다.</param>
        /// <param name="from">선분의 시작 좌표이다.</param>
        /// <param name="to">선분의 끝 좌표이다.</param>
        /// <param name="entryFraction">
        /// 선분을 따라 직사각형에 들어가는 비율(0 이상 1 이하)이다. 만나지 않았으면 뜻이 없다.
        /// </param>
        /// <param name="entryNormal">
        /// 들어가는 자리의 바깥쪽 법선이다. 시작이 이미 안이거나 만나지 않았으면 뜻이 없다(퇴화한
        /// 경우는 <see cref="PlanarPosition.Zero"/>로 구분한다).
        /// </param>
        /// <returns>선분이 직사각형과 만나면(닿기만 해도) true이다.</returns>
        public static bool TryGetEntry(
            PlanarRectangle rectangle,
            PlanarPosition from,
            PlanarPosition to,
            out float entryFraction,
            out PlanarPosition entryNormal)
        {
            var localFrom = rectangle.ToLocal(from);
            var localTo = rectangle.ToLocal(to);
            var direction = localTo - localFrom;
            var halfExtents = rectangle.HalfExtents;

            var entry = 0f;
            var exit = 1f;
            var entryAxisIsX = false;
            var hasEntryAxis = false;

            if (!NarrowRange(localFrom.X, direction.X, halfExtents.X, true, ref entry, ref exit, ref entryAxisIsX, ref hasEntryAxis)
                || !NarrowRange(localFrom.Z, direction.Z, halfExtents.Z, false, ref entry, ref exit, ref entryAxisIsX, ref hasEntryAxis))
            {
                entryFraction = 0f;
                entryNormal = PlanarPosition.Zero;
                return false;
            }

            entryFraction = Mathf.Max(0f, entry);
            if (!hasEntryAxis)
            {
                // 두 축 모두 시작점을 넘어서지 못했다 — 시작이 이미 안이었다는 뜻이다.
                entryNormal = PlanarPosition.Zero;
                return true;
            }

            var localNormal = entryAxisIsX
                ? new PlanarPosition(-Mathf.Sign(direction.X), 0f)
                : new PlanarPosition(0f, -Mathf.Sign(direction.Z));
            entryNormal = rectangle.Right * localNormal.X + rectangle.Forward * localNormal.Z;
            return true;
        }

        /// <summary>
        /// 선분이 원에 들어가는 지점과 그 자리의 바깥쪽 법선을 구한다.
        /// </summary>
        /// <remarks>
        /// <see cref="TryGetEntry(PlanarRectangle, PlanarPosition, PlanarPosition, out float, out PlanarPosition)"/>와
        /// 같은 뜻이고 같은 퇴화 규칙(시작이 이미 안이면 비율 0, 법선 <see cref="PlanarPosition.Zero"/>)을 따른다 —
        /// 장애물을 사각형으로 자르는 것과 유닛을 원으로 자르는 것을 같은 방식으로 다루기 위해서다.
        /// </remarks>
        /// <param name="circle">확인할 원이다.</param>
        /// <param name="from">선분의 시작 좌표이다.</param>
        /// <param name="to">선분의 끝 좌표이다.</param>
        /// <param name="entryFraction">
        /// 선분을 따라 원에 들어가는 비율(0 이상 1 이하)이다. 만나지 않았으면 뜻이 없다.
        /// </param>
        /// <param name="entryNormal">
        /// 들어가는 자리의 바깥쪽 법선이다. 시작이 이미 안이거나 만나지 않았으면 뜻이 없다(퇴화한
        /// 경우는 <see cref="PlanarPosition.Zero"/>로 구분한다).
        /// </param>
        /// <returns>선분이 원과 만나면(닿기만 해도) true이다.</returns>
        public static bool TryGetEntry(
            PlanarCircle circle,
            PlanarPosition from,
            PlanarPosition to,
            out float entryFraction,
            out PlanarPosition entryNormal)
        {
            var toCenter = from - circle.Center;
            if (toCenter.SqrMagnitude <= circle.Radius * circle.Radius)
            {
                entryFraction = 0f;
                entryNormal = PlanarPosition.Zero;
                return true;
            }

            var direction = to - from;
            var a = direction.SqrMagnitude;
            if (a <= NegligibleSqrDistance)
            {
                // 움직이지 않는데 시작이 이미 밖이었다 — 영영 만나지 않는다.
                entryFraction = 0f;
                entryNormal = PlanarPosition.Zero;
                return false;
            }

            var b = 2f * PlanarPosition.Dot(toCenter, direction);
            var c = toCenter.SqrMagnitude - circle.Radius * circle.Radius;
            var discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                entryFraction = 0f;
                entryNormal = PlanarPosition.Zero;
                return false;
            }

            var entryT = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
            if (entryT < 0f || entryT > 1f)
            {
                entryFraction = 0f;
                entryNormal = PlanarPosition.Zero;
                return false;
            }

            entryFraction = entryT;
            entryNormal = (from + direction * entryT - circle.Center).Normalized;
            return true;
        }

        /// <summary>
        /// 한 축에서 선분이 상자 안에 머무는 구간을 좁힌다.
        /// </summary>
        /// <param name="origin">그 축의 시작 좌표이다.</param>
        /// <param name="direction">그 축의 방향 성분이다.</param>
        /// <param name="halfExtent">그 축의 반크기이다.</param>
        /// <param name="isXAxis">이 축이 X축이면 참이고, Z축이면 거짓이다.</param>
        /// <param name="entry">지금까지 좁혀진 구간의 시작이며 갱신된다.</param>
        /// <param name="exit">지금까지 좁혀진 구간의 끝이며 갱신된다.</param>
        /// <param name="entryAxisIsX">
        /// <paramref name="entry"/>를 마지막으로 정한 축이 X축이면 참이다. 이 축이 <paramref name="entry"/>를
        /// 갱신하면 그 값으로 바뀐다.
        /// </param>
        /// <param name="hasEntryAxis">
        /// <paramref name="entry"/>를 갱신한 축이 하나라도 있으면 참이다. 시작이 이미 상자 안이면
        /// 어느 축도 갱신하지 않아 거짓으로 남는다.
        /// </param>
        /// <returns>구간이 남아 있으면 true이다.</returns>
        private static bool NarrowRange(
            float origin, float direction, float halfExtent, bool isXAxis,
            ref float entry, ref float exit, ref bool entryAxisIsX, ref bool hasEntryAxis)
        {
            if (Mathf.Abs(direction) <= NegligibleDistance)
            {
                // 이 축으로는 움직이지 않는다. 시작부터 상자 밖이면 영영 들어가지 못한다.
                return Mathf.Abs(origin) <= halfExtent;
            }

            var inverseDirection = 1f / direction;
            var near = (-halfExtent - origin) * inverseDirection;
            var far = (halfExtent - origin) * inverseDirection;
            if (near > far)
            {
                (near, far) = (far, near);
            }

            if (near > entry)
            {
                entry = near;
                entryAxisIsX = isXAxis;
                hasEntryAxis = true;
            }

            exit = Mathf.Min(exit, far);
            return entry <= exit;
        }
    }
}
