using System.Collections.Generic;
using HS.Tactics.Foundation.Geometry;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 장애물에 막힌 이동이 잘리고 필요하면 한 번 미끄러지는지 검증한다.
    /// 진입점을 법선 쪽으로 밀어 다음 이동이 같은 경계에 막히지 않으며, 미끄러짐 결과는 좌표로 확인한다.
    /// 여러 유닛의 충돌과 밀어낸 위치가 다른 장애물에 겹치는 배치는 이 파일의 범위 밖이다.
    /// </summary>
    public sealed class PlanarObstacleAvoidanceTests
    {
        private const float Tolerance = 0.001f;
        private static readonly PlanarRectangle WideObstacle =
            new(PlanarPosition.Zero, new PlanarPosition(2f, 2f), new PlanarPosition(0f, 1f));
        private static readonly PlanarRectangle NarrowTallObstacle =
            new(PlanarPosition.Zero, new PlanarPosition(1f, 3f), new PlanarPosition(0f, 1f));

        [Test]
        public void WithNoObstaclesTheStepReachesTheTargetUnblocked()
        {
            var result = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(-10f, 0f), new PlanarPosition(10f, 0f), 0.5f, new List<PlanarRectangle>());

            Assert.That(result.WasBlocked, Is.False);
            AssertPosition(result.Position, 10f, 0f);
        }

        [Test]
        public void ANullObstacleListBehavesLikeAnEmptyOne()
        {
            var result = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(-1f, 0f), new PlanarPosition(1f, 0f), 0.5f, null);

            Assert.That(result.WasBlocked, Is.False);
        }

        [Test]
        public void AHeadOnApproachStopsAtTheInflatedFaceWithoutSliding()
        {
            // 반지름 0.5 로 부풀리면 반크기가 (2.5,2.5)가 된다. 곧장 +X로 다가가므로
            // 접선 성분이 0이다 — 미끄러질 방향이 없어 그 자리에서 멈춰야 한다.
            var result = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(-10f, 0f), new PlanarPosition(10f, 0f), 0.5f,
                new List<PlanarRectangle> { WideObstacle });

            Assert.That(result.WasBlocked, Is.True);
            // 진입 지점(x=-2.5)에서 법선(-1,0) 쪽으로 한 번 더 밀려난 자리다.
            AssertPosition(result.Position, -2.51f, 0f);
        }

        [Test]
        public void AnAngledApproachSlidesAlongTheFaceAndMakesFurtherProgress()
        {
            var result = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(-10f, 0f), new PlanarPosition(10f, 4f), 0f,
                new List<PlanarRectangle> { NarrowTallObstacle });

            Assert.That(result.WasBlocked, Is.True);
            // 손으로 미리 계산한 값이다: 진입 지점(-1,1.8)에서 법선(-1,0) 쪽으로 밀려난 뒤,
            // 남은 거리(약 11.23)만큼 접선(0,1) 방향으로 미끄러진다.
            AssertPosition(result.Position, -1.01f, 13.0276f);

            var startToClipOnly = PlanarPosition.Distance(
                new PlanarPosition(-10f, 0f), new PlanarPosition(-1f, 1.8f));
            var startToActual = PlanarPosition.Distance(new PlanarPosition(-10f, 0f), result.Position);
            Assert.That(startToActual, Is.GreaterThan(startToClipOnly), "미끄러지지 않았다면 자른 자리에 머물러야 한다.");
        }

        [Test]
        public void SlidingIntoASecondObstacleStopsPartwayInsteadOfBouncingAgain()
        {
            // 미끄러지는 길목(대략 x=-1 부근, z가 커지는 방향)에 두 번째 장애물을 놓는다.
            // 재시도가 다시 막히면 거기서 멈춰야 한다 — 세 번째 시도로 또 튕기지 않는다.
            var blockingSlide = new PlanarRectangle(
                new PlanarPosition(-1f, 6f), new PlanarPosition(1f, 1f), new PlanarPosition(0f, 1f));
            var obstacles = new List<PlanarRectangle> { NarrowTallObstacle, blockingSlide };

            var result = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(-10f, 0f), new PlanarPosition(10f, 4f), 0f, obstacles);

            Assert.That(result.WasBlocked, Is.True);
            foreach (var obstacle in obstacles)
            {
                Assert.That(PlanarGeometry.Contains(obstacle, result.Position), Is.False, "장애물 안에 멈추면 안 된다.");
            }

            var slidPastFirstClip = PlanarPosition.Distance(new PlanarPosition(-1f, 1.8f), result.Position);
            Assert.That(slidPastFirstClip, Is.GreaterThan(0f), "둘째 장애물을 만나기 전까지는 미끄러져야 한다.");
            Assert.That(result.Position.Z, Is.LessThan(13f), "둘째 장애물이 끝까지 미끄러지는 것을 막아야 한다.");
        }

        [Test]
        public void StartingAlreadyInsideAnObstacleDoesNotThrowAndStaysPut()
        {
            // 넛지나 부동소수 오차로 시작이 이미 걸쳐 있는 퇴화 경우다. 법선을 정할 수 없으므로
            // 미끄러지지 않고 그 자리에 머문다.
            var obstacle = new PlanarRectangle(
                PlanarPosition.Zero, new PlanarPosition(1f, 1f), new PlanarPosition(0f, 1f));

            var result = PlanarObstacleAvoidance.Advance(
                PlanarPosition.Zero, new PlanarPosition(5f, 5f), 0f, new List<PlanarRectangle> { obstacle });

            Assert.That(result.WasBlocked, Is.True);
            AssertPosition(result.Position, 0f, 0f);
        }

        [Test]
        public void ALargerRadiusBlocksEarlierThanASmallerOne()
        {
            var smallRadius = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(-10f, 0f), new PlanarPosition(10f, 0f), 0.1f,
                new List<PlanarRectangle> { WideObstacle });
            var largeRadius = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(-10f, 0f), new PlanarPosition(10f, 0f), 1f,
                new List<PlanarRectangle> { WideObstacle });

            Assert.That(largeRadius.Position.X, Is.LessThan(smallRadius.Position.X), "반지름이 클수록 더 일찍 막혀야 한다.");
        }

        private static void AssertPosition(PlanarPosition actual, float expectedX, float expectedZ)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.Z, Is.EqualTo(expectedZ).Within(Tolerance));
        }
    }
}
