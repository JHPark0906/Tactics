using System.Collections.Generic;
using HS.Tactics.Foundation.Geometry;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>다른 유닛의 원도 사각형 장애물과 같은 걸음 자르기에 함께 겨루는지 검증한다.</summary>
    /// <remarks>
    /// <para>
    /// 기대 좌표는 원의 진입 지점과 접선 이동을 기준으로 검증한다. 원-원 부풀리기는 사각형과 달리 반지름을 더하기만 하면 되는 정확한 계산이므로,
    /// 값 자체는 사각형 쪽보다 오히려 단순하다.
    /// </para>
    /// <para>
    /// <b>확인하지 않는 것</b>: 공간 레지스트리에서 원을 얻어 오는 배선(<c>PlanarCharacterMover.SetNearbyUnitsQuery</c>,
    /// <c>TacticalUnit.CollectNearbyUnitCircles</c>)은 여기서 안 본다. 여기서 보는 것은
    /// <c>PlanarObstacleAvoidance</c>에 원 목록을 직접 건넸을 때의 순수 계산뿐이다.
    /// </para>
    /// </remarks>
    public sealed class PlanarObstacleAvoidanceUnitCirclesTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void AHeadOnApproachIsStoppedByAnotherUnitsCircle()
        {
            // 자신의 반지름 0.5 + 상대 반지름 0.5를 더한 합산 반지름 1로 막힌다.
            var others = new List<PlanarCircle> { new(PlanarPosition.Zero, 0.5f) };

            var result = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(-10f, 0f), new PlanarPosition(10f, 0f), 0.5f, null, others);

            Assert.That(result.WasBlocked, Is.True);
            AssertPosition(result.Position, -1.01f, 0f);
        }

        [Test]
        public void ANearerUnitCircleWinsOverAFartherRectangle()
        {
            // 원(중심 2, 반지름 1)이 사각형(중심 6)보다 먼저 막는다 — 두 후보를 독립된 두 패스로 나누지
            // 않고 하나의 nearestFraction으로 겨루게 했는지가 여기서 갈린다.
            var rectangles = new List<PlanarRectangle>
            {
                new(new PlanarPosition(6f, 0f), new PlanarPosition(1f, 1f), new PlanarPosition(0f, 1f)),
            };
            var circles = new List<PlanarCircle> { new(new PlanarPosition(2f, 0f), 1f) };

            var result = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(-10f, 0f), new PlanarPosition(10f, 0f), 0f, rectangles, circles);

            Assert.That(result.WasBlocked, Is.True);
            // 원의 진입 지점(x=1)에서 밀려난 자리다. 사각형만 봤다면 x=5 부근에서 멈췄을 것이다.
            AssertPosition(result.Position, 0.99f, 0f);
        }

        [Test]
        public void AnAngledApproachSlidesAlongAUnitsCircleAndMakesFurtherProgress()
        {
            var others = new List<PlanarCircle> { new(new PlanarPosition(-1f, 2f), 1f) };

            var result = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(-10f, 0f), new PlanarPosition(10f, 4f), 0f, null, others);

            Assert.That(result.WasBlocked, Is.True);
            // 예상 경로는 다음과 같다: 진입 지점(-1.9231, 1.6154)에서 법선 쪽으로
            // 밀려난 뒤, 접선을 따라 남은 거리만큼 미끄러진다. 다시 막히지 않는다.
            AssertPosition(result.Position, 2.7481f, -9.6214f);

            var startToClipOnly = PlanarPosition.Distance(
                new PlanarPosition(-10f, 0f), new PlanarPosition(-1.9231f, 1.6154f));
            var startToActual = PlanarPosition.Distance(new PlanarPosition(-10f, 0f), result.Position);
            Assert.That(startToActual, Is.GreaterThan(startToClipOnly), "미끄러지지 않았다면 자른 자리에 머물러야 한다.");
        }

        [Test]
        public void ThreeUnitsConvergingOnTheSameSpotResolveByTickOrderNotByExplicitArbitration()
        {
            // 세 유닛이 같은 자리 (0,0)을 놓고 다툰다. 순서는 Unity가 정하는 FixedUpdate 호출 순서를
            // 흉내 낸다 — A, B, C 순으로 Tick을 준 것과 같다. 각자 자기 차례에 그때까지 확정된
            // 다른 유닛의 자리(CurrentLogicPosition)만 본다. 별도의 "누가 이긴다" 중재 함수는 없다 —
            // 먼저 도는 쪽이 먼저 차지하고, 나중 쪽은 이미 찬 자리를 원으로 보고 막힐 뿐이다.
            const float ownRadius = 0.5f;

            var a = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(-10f, 0f), PlanarPosition.Zero, ownRadius, null, new List<PlanarCircle>());
            Assert.That(a.WasBlocked, Is.False, "A가 도는 시점에는 아무도 (0,0) 근처에 없다.");
            AssertPosition(a.Position, 0f, 0f);

            var b = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(0f, -10f), PlanarPosition.Zero, ownRadius, null,
                new List<PlanarCircle> { new(a.Position, ownRadius) });
            Assert.That(b.WasBlocked, Is.True, "B는 이미 자리를 차지한 A에 막혀야 한다.");
            AssertPosition(b.Position, 0f, -1.01f);

            var c = PlanarObstacleAvoidance.Advance(
                new PlanarPosition(8f, 8f), PlanarPosition.Zero, ownRadius, null,
                new List<PlanarCircle> { new(a.Position, ownRadius), new(b.Position, ownRadius) });
            Assert.That(c.WasBlocked, Is.True, "C도 A에 막혀야 한다 — B는 C의 대각선 경로에서 멀다.");
            AssertPosition(c.Position, 0.714178f, 0.714178f);

            // 셋 다 서로 겹치지 않아야 한다(합산 반지름 1.0 이상 떨어져 있어야 한다).
            Assert.That(PlanarPosition.Distance(a.Position, b.Position), Is.GreaterThanOrEqualTo(1f - Tolerance));
            Assert.That(PlanarPosition.Distance(a.Position, c.Position), Is.GreaterThanOrEqualTo(1f - Tolerance));
            Assert.That(PlanarPosition.Distance(b.Position, c.Position), Is.GreaterThanOrEqualTo(1f - Tolerance));
        }

        private static void AssertPosition(PlanarPosition actual, float expectedX, float expectedZ)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.Z, Is.EqualTo(expectedZ).Within(Tolerance));
        }
    }
}
