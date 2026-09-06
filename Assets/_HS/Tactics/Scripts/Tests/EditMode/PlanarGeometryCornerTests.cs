using HS.Tactics.Foundation.Geometry;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>직사각형의 꼭짓점 열거가 약속한 순서와 자리를 지키는지 검증한다.</summary>
    /// <remarks>
    /// 시야 그래프가 이 꼭짓점을 그래프의 노드로 삼으므로, 순서가 흔들리면 그 위에 쌓은 것도 흔들린다.
    /// 회전한 직사각형에서도 같은 관계가 지켜지는지를 확인한다.
    /// </remarks>
    public sealed class PlanarGeometryCornerTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void AnAxisAlignedRectangleGivesCornersInOrder()
        {
            var rectangle = new PlanarRectangle(
                new PlanarPosition(0f, 0f), new PlanarPosition(2f, 1f), new PlanarPosition(0f, 1f));

            var corners = PlanarGeometry.GetCorners(rectangle);

            Assert.That(corners.Length, Is.EqualTo(4));
            AssertPosition(corners[0], -2f, -1f, "첫 꼭짓점은 (-X,-Z)이다.");
            AssertPosition(corners[1], 2f, -1f, "둘째 꼭짓점은 (+X,-Z)이다.");
            AssertPosition(corners[2], 2f, 1f, "셋째 꼭짓점은 (+X,+Z)이다.");
            AssertPosition(corners[3], -2f, 1f, "넷째 꼭짓점은 (-X,+Z)이다.");
        }

        [Test]
        public void ARotatedRectangleGivesCornersInTheSameLocalOrder()
        {
            // forward가 (1,0)이면 Right는 PerpendicularClockwise(1,0)=(0,-1)이다.
            // 로컬 (-2,-1) → 중심 + Right*(-2) + Forward*(-1) = (5,5) + (0,2) + (-1,0) = (4,7).
            var rectangle = new PlanarRectangle(
                new PlanarPosition(5f, 5f), new PlanarPosition(2f, 1f), new PlanarPosition(1f, 0f));

            var corners = PlanarGeometry.GetCorners(rectangle);

            AssertPosition(corners[0], 4f, 7f, "회전해도 로컬 (-X,-Z) 자리가 첫 꼭짓점이다.");
        }

        [Test]
        public void EveryCornerLiesExactlyOnTheBoundary()
        {
            var rectangle = new PlanarRectangle(
                new PlanarPosition(3f, -2f), new PlanarPosition(1.5f, 0.5f), new PlanarPosition(0f, 1f));

            foreach (var corner in PlanarGeometry.GetCorners(rectangle))
            {
                Assert.That(PlanarGeometry.Contains(rectangle, corner), Is.True, "꼭짓점은 직사각형 안(경계 포함)에 있어야 한다.");

                var local = rectangle.ToLocal(corner);
                var onXEdge = UnityEngine.Mathf.Abs(UnityEngine.Mathf.Abs(local.X) - rectangle.HalfExtents.X) <= Tolerance;
                var onZEdge = UnityEngine.Mathf.Abs(UnityEngine.Mathf.Abs(local.Z) - rectangle.HalfExtents.Z) <= Tolerance;
                Assert.That(onXEdge && onZEdge, Is.True, "꼭짓점은 두 변이 만나는 자리여야 한다.");
            }
        }

        private static void AssertPosition(PlanarPosition actual, float expectedX, float expectedZ, string message)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(Tolerance), message);
            Assert.That(actual.Z, Is.EqualTo(expectedZ).Within(Tolerance), message);
        }
    }
}
