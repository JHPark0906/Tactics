using HS.Tactics.Foundation.Geometry;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>선분이 직사각형에 들어가는 지점과 법선을 구하는 계산을 검증한다.</summary>
    /// <remarks>
    /// 걸음 자르기가 이 계산에 기댄다 — 진입 비율로 어디서 멈출지, 법선으로 어느 방향으로
    /// 미끄러질지를 정한다. 그래서 <c>true/false</c>만이 아니라 그 두 값의 정확한 자리를 본다.
    /// </remarks>
    public sealed class PlanarGeometryEntryTests
    {
        private const float Tolerance = 0.0001f;
        private static readonly PlanarRectangle AxisAligned =
            new(PlanarPosition.Zero, new PlanarPosition(1f, 1f), new PlanarPosition(0f, 1f));

        [Test]
        public void EntersThroughTheLeftFaceWhenMovingInPositiveX()
        {
            var found = PlanarGeometry.TryGetEntry(
                AxisAligned, new PlanarPosition(-3f, 0f), new PlanarPosition(3f, 0f),
                out var fraction, out var normal);

            Assert.That(found, Is.True);
            Assert.That(fraction, Is.EqualTo(2f / 6f).Within(Tolerance), "왼쪽 면(x=-1)에 닿는 지점이다.");
            AssertNormal(normal, -1f, 0f);
        }

        [Test]
        public void EntersThroughTheRightFaceWhenMovingInNegativeX()
        {
            var found = PlanarGeometry.TryGetEntry(
                AxisAligned, new PlanarPosition(3f, 0f), new PlanarPosition(-3f, 0f),
                out var fraction, out var normal);

            Assert.That(found, Is.True);
            Assert.That(fraction, Is.EqualTo(2f / 6f).Within(Tolerance));
            AssertNormal(normal, 1f, 0f);
        }

        [Test]
        public void EntersThroughTheBottomFaceWhenMovingInPositiveZ()
        {
            var found = PlanarGeometry.TryGetEntry(
                AxisAligned, new PlanarPosition(0f, -3f), new PlanarPosition(0f, 3f),
                out var fraction, out var normal);

            Assert.That(found, Is.True);
            AssertNormal(normal, 0f, -1f);
        }

        [Test]
        public void AParallelSegmentThatMissesReturnsFalse()
        {
            var found = PlanarGeometry.TryGetEntry(
                AxisAligned, new PlanarPosition(-3f, 5f), new PlanarPosition(3f, 5f),
                out _, out _);

            Assert.That(found, Is.False, "z=5는 직사각형(반크기 1) 밖이라 만나지 않는다.");
        }

        [Test]
        public void AStartAlreadyInsideReportsZeroFractionAndNoNormal()
        {
            // 시작이 경계에 걸쳐 있으면 어느 면으로 들어왔는지 정할 수 없는 퇴화 경우이다.
            var found = PlanarGeometry.TryGetEntry(
                AxisAligned, PlanarPosition.Zero, new PlanarPosition(5f, 5f),
                out var fraction, out var normal);

            Assert.That(found, Is.True, "이미 안에 있는 것도 '만난다'로 본다.");
            Assert.That(fraction, Is.Zero, "이미 안에 있으므로 들어가는 데 걸리는 비율이 없다.");
            Assert.That(normal, Is.EqualTo(PlanarPosition.Zero), "어느 면으로 들어왔는지 정할 수 없다.");
        }

        [Test]
        public void IntersectsSegmentAgreesWithTryGetEntryOnWhetherTheyMeet()
        {
            // 같은 슬랩 계산을 공유하므로 만난다/안 만난다는 항상 일치해야 한다.
            var crossing = PlanarGeometry.IntersectsSegment(
                AxisAligned, new PlanarPosition(-3f, 0f), new PlanarPosition(3f, 0f));
            var missing = PlanarGeometry.IntersectsSegment(
                AxisAligned, new PlanarPosition(-3f, 5f), new PlanarPosition(3f, 5f));

            Assert.That(crossing, Is.True);
            Assert.That(missing, Is.False);
        }

        private static void AssertNormal(PlanarPosition actual, float expectedX, float expectedZ)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.Z, Is.EqualTo(expectedZ).Within(Tolerance));
        }
    }
}
