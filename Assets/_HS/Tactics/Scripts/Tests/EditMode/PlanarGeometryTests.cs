using HS.Tactics.Foundation.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>수평면 판정 세 가지와 겹침 해소가 약속대로 동작하는지 검증한다.</summary>
    /// <remarks>
    /// 이 함수들은 시계도 씬도 쓰지 않는 순수 계산이라 여기서 전부 덮을 수 있다.
    /// 특히 <b>방향을 정할 수 없는 순간에도 결과가 정해져 있는지</b>를 확인한다.
    /// 그 자리에서 임의로 고르면 같은 입력에서 다른 결과가 나온다.
    /// </remarks>
    public sealed class PlanarGeometryTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void WorldConversionDropsAndRestoresHeight()
        {
            var planar = PlanarPosition.FromWorld(new Vector3(3f, 99f, -4f));

            Assert.That(planar.X, Is.EqualTo(3f));
            Assert.That(planar.Z, Is.EqualTo(-4f));
            Assert.That(planar.ToWorld(1.4f), Is.EqualTo(new Vector3(3f, 1.4f, -4f)));
            Assert.That(planar.ToWorld().y, Is.Zero, "높이를 지정하지 않으면 0이다.");
        }

        [Test]
        public void MagnitudeAndDistanceIgnoreHeightEntirely()
        {
            var from = PlanarPosition.FromWorld(new Vector3(0f, 100f, 0f));
            var to = PlanarPosition.FromWorld(new Vector3(3f, -100f, 4f));

            Assert.That(PlanarPosition.Distance(from, to), Is.EqualTo(5f).Within(Tolerance));
            Assert.That(PlanarPosition.SqrDistance(from, to), Is.EqualTo(25f).Within(Tolerance));
        }

        [Test]
        public void NormalizingAZeroVectorGivesZeroInsteadOfNaN()
        {
            Assert.That(PlanarPosition.Zero.Normalized, Is.EqualTo(PlanarPosition.Zero));
        }

        [Test]
        public void PerpendicularTurnsForwardIntoRight()
        {
            var forward = new PlanarPosition(0f, 1f);

            var right = forward.PerpendicularClockwise();

            Assert.That(right.X, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(right.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void CirclesOverlapOnlyWhenTheyActuallyIntersect()
        {
            var first = new PlanarCircle(new PlanarPosition(0f, 0f), 1f);

            Assert.That(PlanarGeometry.Overlaps(first, new PlanarCircle(new PlanarPosition(1.5f, 0f), 1f)), Is.True);
            Assert.That(PlanarGeometry.Overlaps(first, new PlanarCircle(new PlanarPosition(3f, 0f), 1f)), Is.False);
            Assert.That(
                PlanarGeometry.Overlaps(first, new PlanarCircle(new PlanarPosition(2f, 0f), 1f)),
                Is.False,
                "스치듯 닿기만 한 것은 겹친 것으로 보지 않는다.");
        }

        [Test]
        public void ResolvingCircleOverlapPushesJustFarEnoughApart()
        {
            var moving = new PlanarCircle(new PlanarPosition(1f, 0f), 1f);
            var fixedCircle = new PlanarCircle(new PlanarPosition(0f, 0f), 1f);

            var push = PlanarGeometry.ResolveOverlap(moving, fixedCircle);
            var resolved = new PlanarCircle(moving.Center + push, moving.Radius);

            Assert.That(push.X, Is.EqualTo(1f).Within(Tolerance), "겹친 만큼만 밀어야 한다.");
            Assert.That(push.Z, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(PlanarGeometry.Overlaps(resolved, fixedCircle), Is.False);
        }

        [Test]
        public void SeparatedCirclesAreNotPushed()
        {
            var first = new PlanarCircle(new PlanarPosition(5f, 0f), 1f);
            var second = new PlanarCircle(new PlanarPosition(0f, 0f), 1f);

            Assert.That(PlanarGeometry.ResolveOverlap(first, second), Is.EqualTo(PlanarPosition.Zero));
        }

        [Test]
        public void CirclesAtTheExactSamePlaceAreSeparatedInAFixedDirection()
        {
            var first = new PlanarCircle(new PlanarPosition(2f, 2f), 1f);
            var second = new PlanarCircle(new PlanarPosition(2f, 2f), 1.5f);

            var push = PlanarGeometry.ResolveOverlap(first, second);

            Assert.That(
                push.X,
                Is.EqualTo(2.5f).Within(Tolerance),
                "방향을 정할 수 없을 때 임의로 고르면 같은 입력에서 다른 결과가 나온다.");
            Assert.That(push.Z, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(
                PlanarGeometry.Overlaps(new PlanarCircle(first.Center + push, first.Radius), second),
                Is.False);
        }

        [Test]
        public void RectangleLocalFrameFollowsItsForward()
        {
            var rectangle = new PlanarRectangle(
                new PlanarPosition(0f, 0f),
                new PlanarPosition(1f, 2f),
                new PlanarPosition(1f, 0f));

            var local = rectangle.ToLocal(new PlanarPosition(3f, 0f));

            Assert.That(local.Z, Is.EqualTo(3f).Within(Tolerance), "정면으로 3만큼 떨어져 있다.");
            Assert.That(local.X, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void LocalAndWorldConversionsAreInverses()
        {
            var rectangle = new PlanarRectangle(
                new PlanarPosition(5f, -3f),
                new PlanarPosition(2f, 1f),
                new PlanarPosition(1f, 1f));
            var point = new PlanarPosition(7f, 2f);

            var roundTripped = rectangle.ToWorld(rectangle.ToLocal(point));

            Assert.That(PlanarPosition.Distance(roundTripped, point), Is.LessThan(Tolerance));
        }

        [Test]
        public void RectangleWithoutForwardFallsBackToAFixedAxis()
        {
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(1f, 1f),
                PlanarPosition.Zero);

            Assert.That(rectangle.Forward.Z, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void RotatedRectangleContainsPointsAlongItsOwnAxes()
        {
            // 정면이 대각선인 사각형이다. 축에 정렬된 판정이었다면 결과가 달라진다.
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(1f, 3f),
                new PlanarPosition(1f, 1f).Normalized);

            var alongForward = rectangle.ToWorld(new PlanarPosition(0f, 2.5f));
            var beyondSide = rectangle.ToWorld(new PlanarPosition(1.5f, 0f));

            Assert.That(PlanarGeometry.Contains(rectangle, alongForward), Is.True);
            Assert.That(PlanarGeometry.Contains(rectangle, beyondSide), Is.False);
        }

        [Test]
        public void ClosestPointClampsOntoTheRectangleEdge()
        {
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(1f, 2f),
                new PlanarPosition(0f, 1f));

            var closest = PlanarGeometry.ClosestPoint(rectangle, new PlanarPosition(10f, 0f));

            Assert.That(closest.X, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(closest.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void CircleOverlapsRectangleOnlyWhenItReachesTheEdge()
        {
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(1f, 1f),
                new PlanarPosition(0f, 1f));

            Assert.That(
                PlanarGeometry.Overlaps(new PlanarCircle(new PlanarPosition(1.5f, 0f), 1f), rectangle),
                Is.True);
            Assert.That(
                PlanarGeometry.Overlaps(new PlanarCircle(new PlanarPosition(3f, 0f), 1f), rectangle),
                Is.False);
        }

        [Test]
        public void CircleInsideTheRectangleCountsAsOverlapping()
        {
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(3f, 3f),
                new PlanarPosition(0f, 1f));

            Assert.That(
                PlanarGeometry.Overlaps(new PlanarCircle(PlanarPosition.Zero, 0.5f), rectangle),
                Is.True);
        }

        [Test]
        public void ResolvingRectangleOverlapPushesTheCircleClear()
        {
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(1f, 1f),
                new PlanarPosition(0f, 1f));
            var circle = new PlanarCircle(new PlanarPosition(1.5f, 0f), 1f);

            var push = PlanarGeometry.ResolveOverlap(circle, rectangle);
            var resolved = new PlanarCircle(circle.Center + push, circle.Radius);

            Assert.That(push.X, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(PlanarGeometry.Overlaps(resolved, rectangle), Is.False);
        }

        [Test]
        public void CircleTrappedInsideLeavesThroughTheNearestFace()
        {
            // 안쪽 깊숙이 들어간 원을 반대편으로 튕겨 내면 유닛이 엄폐물을 가로지른다.
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(1f, 4f),
                new PlanarPosition(0f, 1f));
            var circle = new PlanarCircle(new PlanarPosition(0.6f, 0f), 0.5f);

            var push = PlanarGeometry.ResolveOverlap(circle, rectangle);
            var resolved = new PlanarCircle(circle.Center + push, circle.Radius);

            Assert.That(push.X, Is.GreaterThan(0f), "가까운 쪽 변으로 나가야 한다.");
            Assert.That(push.Z, Is.EqualTo(0f).Within(Tolerance), "먼 쪽 축으로 밀면 엄폐물을 세로로 가로지른다.");
            Assert.That(PlanarGeometry.Overlaps(resolved, rectangle), Is.False);
        }

        [Test]
        public void SegmentThroughTheRectangleIsBlocked()
        {
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(2f, 0.5f),
                new PlanarPosition(0f, 1f));

            Assert.That(
                PlanarGeometry.IntersectsSegment(
                    rectangle,
                    new PlanarPosition(0f, -5f),
                    new PlanarPosition(0f, 5f)),
                Is.True);
        }

        [Test]
        public void SegmentPassingBesideTheRectangleIsNotBlocked()
        {
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(2f, 0.5f),
                new PlanarPosition(0f, 1f));

            Assert.That(
                PlanarGeometry.IntersectsSegment(
                    rectangle,
                    new PlanarPosition(5f, -5f),
                    new PlanarPosition(5f, 5f)),
                Is.False);
        }

        [Test]
        public void SegmentEndingBeforeTheRectangleIsNotBlocked()
        {
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(2f, 0.5f),
                new PlanarPosition(0f, 1f));

            Assert.That(
                PlanarGeometry.IntersectsSegment(
                    rectangle,
                    new PlanarPosition(0f, -10f),
                    new PlanarPosition(0f, -5f)),
                Is.False,
                "선분이 닿지 않는 곳에서 끝나면 가리지 못한다.");
        }

        [Test]
        public void SegmentStartingInsideTheRectangleIsBlocked()
        {
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(2f, 2f),
                new PlanarPosition(0f, 1f));

            Assert.That(
                PlanarGeometry.IntersectsSegment(rectangle, PlanarPosition.Zero, new PlanarPosition(10f, 0f)),
                Is.True);
        }

        [Test]
        public void SegmentIsBlockedByARotatedRectangleAlongItsOwnAxes()
        {
            // 정면이 대각선인 얇은 벽이다. 축에 정렬된 판정이었다면 통과시킨다.
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(4f, 0.2f),
                new PlanarPosition(1f, 1f).Normalized);

            Assert.That(
                PlanarGeometry.IntersectsSegment(
                    rectangle,
                    new PlanarPosition(2f, -2f),
                    new PlanarPosition(-2f, 2f)),
                Is.True,
                "벽을 가로지르는 선분은 막혀야 한다.");
            Assert.That(
                PlanarGeometry.IntersectsSegment(
                    rectangle,
                    new PlanarPosition(5f, 5f),
                    new PlanarPosition(8f, 8f)),
                Is.False,
                "벽에서 멀리 떨어진 선분은 막히지 않는다.");
        }

        [Test]
        public void DegenerateSegmentIsBlockedOnlyWhenItStartsInside()
        {
            var rectangle = new PlanarRectangle(
                PlanarPosition.Zero,
                new PlanarPosition(1f, 1f),
                new PlanarPosition(0f, 1f));
            var inside = new PlanarPosition(0.5f, 0.5f);
            var outside = new PlanarPosition(5f, 5f);

            Assert.That(PlanarGeometry.IntersectsSegment(rectangle, inside, inside), Is.True);
            Assert.That(PlanarGeometry.IntersectsSegment(rectangle, outside, outside), Is.False);
        }
    }
}
