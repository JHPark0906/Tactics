using HS.Tactics.Foundation.Geometry;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>선분이 원에 들어가는 지점과 법선을 구하는 계산을 검증한다.</summary>
    /// <remarks>
    /// 유닛간 걸음 자르기가 이 계산에 기댄다 — 사각형용 <c>TryGetEntry</c>와 같은 자리에서 같은 뜻으로
    /// 쓰이므로, 값은 여기서도 손으로 먼저 풀어 맞춘 것이다.
    /// </remarks>
    public sealed class PlanarGeometryCircleEntryTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void EntersAlongAStraightApproachAtTheExpectedFraction()
        {
            var circle = new PlanarCircle(PlanarPosition.Zero, 2f);
            var found = PlanarGeometry.TryGetEntry(
                circle, new PlanarPosition(-5f, 0f), new PlanarPosition(5f, 0f),
                out var fraction, out var normal);

            Assert.That(found, Is.True);
            Assert.That(fraction, Is.EqualTo(0.3f).Within(Tolerance), "손 계산: (-5,0)에서 (0,0) 중심 반지름 2인 원의 왼쪽(-2,0)에 닿는 비율이다.");
            AssertPosition(normal, -1f, 0f);
        }

        [Test]
        public void EntersDiagonallyWithANonAxisAlignedNormal()
        {
            var circle = new PlanarCircle(PlanarPosition.Zero, 1f);
            var found = PlanarGeometry.TryGetEntry(
                circle, new PlanarPosition(-10f, -10f), new PlanarPosition(0f, 0f),
                out var fraction, out var normal);

            Assert.That(found, Is.True);
            Assert.That(fraction, Is.EqualTo(0.9292893f).Within(Tolerance));
            AssertPosition(normal, -0.7071068f, -0.7071068f);
        }

        [Test]
        public void EntersAnOffCenterCircleAtItsOwnBoundary()
        {
            // 중심이 원점이 아닌 원에서도 toCenter/entryNormal 계산이 중심을 제대로 빼는지 본다.
            var circle = new PlanarCircle(new PlanarPosition(3f, 4f), 1f);
            var found = PlanarGeometry.TryGetEntry(
                circle, new PlanarPosition(3f, -6f), new PlanarPosition(3f, 10f),
                out var fraction, out var normal);

            Assert.That(found, Is.True);
            Assert.That(fraction, Is.EqualTo(0.5625f).Within(Tolerance), "손 계산: z=-6에서 출발해 중심(3,4) 반지름 1인 원의 아래쪽(3,3)에 닿는 비율이다.");
            AssertPosition(normal, 0f, -1f);
        }

        [Test]
        public void AParallelSegmentThatMissesReturnsFalse()
        {
            var circle = new PlanarCircle(PlanarPosition.Zero, 2f);
            var found = PlanarGeometry.TryGetEntry(
                circle, new PlanarPosition(-5f, 5f), new PlanarPosition(5f, 5f), out _, out _);

            Assert.That(found, Is.False, "z=5인 직선은 반지름 2인 원의 중심에서 5만큼 떨어져 있어 만나지 않는다.");
        }

        [Test]
        public void AStartAlreadyInsideReportsZeroFractionAndNoNormal()
        {
            // 사각형 쪽 TryGetEntry와 같은 퇴화 규칙이다: 시작이 이미 안이면 어느 면으로 들어왔는지 정할 수 없다.
            var circle = new PlanarCircle(PlanarPosition.Zero, 1f);
            var found = PlanarGeometry.TryGetEntry(
                circle, new PlanarPosition(0.5f, 0f), new PlanarPosition(10f, 10f),
                out var fraction, out var normal);

            Assert.That(found, Is.True, "이미 안에 있는 것도 '만난다'로 본다.");
            Assert.That(fraction, Is.Zero);
            Assert.That(normal, Is.EqualTo(PlanarPosition.Zero));
        }

        [Test]
        public void StartingExactlyOnTheBoundaryCountsAsAlreadyInside()
        {
            var circle = new PlanarCircle(PlanarPosition.Zero, 1f);
            var found = PlanarGeometry.TryGetEntry(
                circle, new PlanarPosition(1f, 0f), new PlanarPosition(10f, 0f), out var fraction, out var normal);

            Assert.That(found, Is.True);
            Assert.That(fraction, Is.Zero);
            Assert.That(normal, Is.EqualTo(PlanarPosition.Zero));
        }

        [Test]
        public void NoMovementOutsideTheCircleNeverEnters()
        {
            var circle = new PlanarCircle(PlanarPosition.Zero, 1f);
            var found = PlanarGeometry.TryGetEntry(
                circle, new PlanarPosition(5f, 0f), new PlanarPosition(5f, 0f), out _, out _);

            Assert.That(found, Is.False, "제자리인데 이미 밖이면 영영 만나지 않는다.");
        }

        [Test]
        public void ACircleBehindTheTravelDirectionIsNotEntered()
        {
            // 원이 이동 방향의 뒤쪽에 있는 경우다 — 직선을 무한히 늘리면 만나지만, 이번 스텝은 반대쪽으로 간다.
            var circle = new PlanarCircle(new PlanarPosition(-5f, 0f), 1f);
            var found = PlanarGeometry.TryGetEntry(
                circle, PlanarPosition.Zero, new PlanarPosition(5f, 0f), out _, out _);

            Assert.That(found, Is.False);
        }

        private static void AssertPosition(PlanarPosition actual, float expectedX, float expectedZ)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.Z, Is.EqualTo(expectedZ).Within(Tolerance));
        }
    }
}
