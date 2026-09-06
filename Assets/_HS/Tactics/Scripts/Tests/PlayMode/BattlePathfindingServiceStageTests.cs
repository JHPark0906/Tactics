using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Tactics.Cover;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Character.Movement;
using HS.Tactics.Pathfinding;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace HS.Tactics.Tests.PlayMode
{
    /// <summary>
    /// BattlePathfindingService가 살아 있는 엄폐물을 피하고 파괴 후 그래프를 다시 만드는지 검증한다.
    /// TacticalUnit이 실제 서비스를 이용하는 배선도 확인한다.
    /// MarkDestroyed를 직접 호출하므로 체력·사망 어빌리티 경로와 스텝 충돌 계산은 이 파일의 범위 밖이다.
    /// 무대는 전장 경계와 평면 장애물로 구성한다.
    /// </summary>
    public sealed class BattlePathfindingServiceStageTests
    {
        private readonly List<GameObject> _createdObjects = new();

        [TestCase(0.2f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void AUnitWithRadiusActuallyWalksTheDetourAroundCover(float radius)
        {
            var service = CreateService(out _);
            var cover = CreateBareCoverPoint(Vector3.zero);
            var moverObject = new GameObject("WalkingUnit");
            _createdObjects.Add(moverObject);
            moverObject.transform.position = new Vector3(-4f, 0f, 0f);
            var mover = moverObject.AddComponent<PlanarCharacterMover>();
            mover.enabled = false;
            mover.Initialize(null);
            mover.SetObstacles(service.LiveObstacles, radius);
            mover.SetPathPlanner((from, to, corners) => service.Plan(from, to, corners, radius));

            var destination = new Vector3(4f, 0f, 0f);
            Assert.That(mover.MoveTo(destination), Is.True);
            WalkUntilArrival(mover, cover, radius);

            Assert.That(Vector3.Distance(mover.transform.position, destination),
                Is.LessThanOrEqualTo(mover.StoppingDistance + 0.01f));
        }

        [TestCase(0.5f, 0.5f, 0f)]
        [TestCase(2f, 1f, 35f)]
        public void ARealUnitArrivesOutsideCoverAndKeepsItsClaim(float halfWidth, float halfDepth, float yaw)
        {
            var service = CreateService(out _);
            var cover = CreateBareCoverPoint(Vector3.zero);
            typeof(CoverPoint).GetField("halfExtents",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(cover, new Vector2(halfWidth, halfDepth));
            cover.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var unitObject = new GameObject("CoveringUnit");
            _createdObjects.Add(unitObject);
            unitObject.transform.position = new Vector3(-5f, 0f, 0f);
            var mover = unitObject.AddComponent<PlanarCharacterMover>();
            mover.enabled = false;
            var coverState = unitObject.AddComponent<UnitCoverState>();
            LogAssert.Expect(LogType.Warning, new Regex("유닛 정의가 없어"));
            LogAssert.Expect(LogType.Warning, new Regex("행동 트리 에셋이 없어"));
            var unit = unitObject.AddComponent<TacticalUnit>();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(service);
            using var container = builder.Build();
            unit.InjectRuntimeDependencies(null, container);

            Assert.That(coverState.ClaimCover(cover), Is.True);
            Assert.That(coverState.IsInCover, Is.False);
            Assert.That(mover.MoveTo(cover.Position), Is.True);
            WalkUntilArrival(mover, cover, 0.5f);
            mover.Stop();
            coverState.RefreshInCoverTag();

            Assert.That(coverState.IsInCover, Is.True, "충돌하지 않고 멈춘 외부 접근 지점이 엄폐 도착이어야 한다.");
            Assert.That(coverState.HasCoverClaim, Is.True);
            Assert.That(cover.Occupant, Is.SameAs(unitObject));
        }

        [Test]
        public void RadiusClearanceRejectsSlotsAndPathsTooCloseToCoverOrBounds()
        {
            var service = CreateService(out _, new Vector2(5f, 5f));
            CreateBareCoverPoint(Vector3.zero);
            var besideCover = new Vector3(0.75f, 0f, 0f);
            var besideBoundary = new Vector3(4.75f, 0f, 0f);
            Assert.That(service.TryResolveReachable(besideCover, out _), Is.True);
            Assert.That(service.TryResolveReachable(besideCover, out _, 0.5f), Is.False);
            Assert.That(service.TryResolveReachable(besideBoundary, out _, 0.5f), Is.False);
            Assert.That(service.Plan(new Vector3(-3f, 0f, 0f), besideCover, new List<Vector3>(), 0.5f), Is.False);
            Assert.That(service.Plan(new Vector3(-3f, 0f, 0f), besideBoundary, new List<Vector3>(), 0.5f), Is.False);
        }

        [Test]
        public void DestroyingCoverInvalidatesEveryRadiusGraph()
        {
            var service = CreateService(out _);
            var cover = CreateBareCoverPoint(Vector3.zero);
            var corners = new List<Vector3>();
            var from = new Vector3(-4f, 0f, 0f);
            var to = new Vector3(4f, 0f, 0f);
            Assert.That(service.Plan(from, to, corners, 0.5f), Is.True);
            Assert.That(corners.Count, Is.GreaterThan(2));

            cover.MarkDestroyed();

            Assert.That(service.Plan(from, to, corners, 0.5f), Is.True);
            Assert.That(corners.Count, Is.EqualTo(2));
        }

        private static void WalkUntilArrival(PlanarCharacterMover mover, CoverPoint cover, float radius)
        {
            var forbidden = CoverApproachGeometry.Expand(cover.Bounds, radius);
            for (var step = 0; step < 1000 && !mover.HasReachedDestination; step++)
            {
                mover.Tick(0.02f);
                Assert.That(PlanarGeometry.Contains(forbidden, PlanarPosition.FromWorld(mover.transform.position)),
                    Is.False, "경로를 따라가는 모든 스텝에서 유닛 전체가 엄폐물 밖에 있어야 한다.");
            }

            Assert.That(mover.HasReachedDestination, Is.True, "유효하다고 계획한 길을 실제 이동기가 완주해야 한다.");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void ARouteDetoursAroundALiveCoverAndGoesStraightAfterItIsDestroyed()
        {
            var service = CreateService(out _);
            var cover = CreateBareCoverPoint(new Vector3(0f, 0f, 0f));

            var corners = new List<Vector3>();
            var found = service.Plan(new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), corners);

            Assert.That(found, Is.True, "무대 확인: 경계 안 직선상의 목적지인데 경로를 못 찾았다.");
            Assert.That(corners.Count, Is.GreaterThan(2), "가운데를 막는 엄폐물이 있으면 곧은 두 점으로 끝나지 않아야 한다.");

            cover.MarkDestroyed();
            var afterCorners = new List<Vector3>();
            var foundAfter = service.Plan(new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), afterCorners);

            Assert.That(foundAfter, Is.True);
            Assert.That(afterCorners.Count, Is.EqualTo(2), "부서진 뒤에는 막는 것이 없으니 출발·도착 두 점뿐이어야 한다.");
        }

        [Test]
        public void LiveObstaclesShrinksInPlaceWhenACoverIsDestroyed()
        {
            var service = CreateService(out _);
            var cover = CreateBareCoverPoint(new Vector3(0f, 0f, 0f));

            var corners = new List<Vector3>();
            service.Plan(new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), corners);
            var liveList = service.LiveObstacles;
            Assert.That(liveList.Count, Is.EqualTo(1), "무대 확인: 엄폐물 하나가 살아 있는 장애물로 들어가야 한다.");

            cover.MarkDestroyed();

            Assert.That(
                ReferenceEquals(liveList, service.LiveObstacles), Is.True,
                "SetObstacles가 붙든 목록 참조가 그대로 유지되어야 다시 배선하지 않고도 최신 상태를 본다.");
            Assert.That(service.LiveObstacles.Count, Is.EqualTo(0), "부서졌으면 더는 장애물 목록에 없어야 한다.");
        }

        [Test]
        public void WithoutABattleBoundsInTheScenePlanFailsAndWarnsOnce()
        {
            var serviceObject = new GameObject("PathfindingService");
            _createdObjects.Add(serviceObject);
            var service = serviceObject.AddComponent<BattlePathfindingService>();

            var corners = new List<Vector3>();
            LogAssert.Expect(LogType.Warning, new Regex(nameof(BattleBounds)));
            var found = service.Plan(new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), corners);

            Assert.That(found, Is.False);

            // 두 번째 호출은 다시 경고하지 않는다 — 매 스텝 부르는 자리에서 로그가 쌓이지 않게 하기 위해서다.
            var secondCall = service.Plan(new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), corners);
            Assert.That(secondCall, Is.False);
        }

        [Test]
        public void ATacticalUnitWithAnInjectedServiceRejectsADestinationOutsideItsBounds()
        {
            // 경계를 목적지보다 좁게 잡아 둔다. 배선이 빠지면(PlanPath가 실패로만 답하므로) 걸을 수
            // 있는 어떤 목적지도 받아들이지 않는데, 그러면 이 검사 자체가 "무대 확인" 단언에서 걸린다 —
            // 그래서 실제로 경계 판정이 도는지(성공이 아니라 이 특정 이유로 실패하는지)를 이 차이로 가른다.
            var service = CreateService(out var bounds, new Vector2(5f, 5f));
            var unitObject = new GameObject("Unit");
            _createdObjects.Add(unitObject);
            unitObject.transform.position = new Vector3(0f, 0f, -3f);
            var mover = unitObject.AddComponent<PlanarCharacterMover>();
            mover.enabled = false;

            LogAssert.Expect(LogType.Warning, new Regex("유닛 정의가 없어"));
            LogAssert.Expect(LogType.Warning, new Regex("행동 트리 에셋이 없어"));
            var unit = unitObject.AddComponent<TacticalUnit>();

            var builder = new ContainerBuilder();
            builder.RegisterInstance(service);
            using var container = builder.Build();
            unit.InjectRuntimeDependencies(null, container);
            unit.InitializeUnit();

            Assert.That(mover.HasPathPlanner, Is.True, "무대 확인: 유닛 조립이 이동기에 경로 계획을 끼워야 한다.");

            // 경계(반너비 5)를 넘는 z=8은 경계 밖이다.
            var outsideBounds = new Vector3(0f, 0f, 8f);
            Assert.That(
                PlanarGeometry.Contains(bounds.Bounds, PlanarPosition.FromWorld(outsideBounds)), Is.False,
                "무대 확인: 목적지가 실제로 경계 밖이어야 한다.");
            // 경계 안 목적지는 받아들여야 "실패=배선 없음"이 아니라 "실패=경계 판정"임을 보장할 수 있다.
            Assert.That(
                mover.MoveTo(new Vector3(0f, 0f, 3f)), Is.True,
                "무대 확인: 경계 안 목적지는 받아들여야 한다 — 그래야 다음 단언이 배선 부재가 아니라 경계 판정 때문임을 안다.");

            var accepted = mover.MoveTo(outsideBounds);

            Assert.That(accepted, Is.False, "실제 경로 계획 서비스가 배선되어 있다면 경계 밖 목적지를 거절해야 한다.");
        }

        /// <summary>서비스와 경계를 씬에 놓는다. <paramref name="halfExtents"/>를 주지 않으면 경계는 기본 크기(반너비 12) 그대로다.</summary>
        private BattlePathfindingService CreateService(out BattleBounds bounds, Vector2? halfExtents = null)
        {
            var boundsObject = new GameObject("Bounds");
            _createdObjects.Add(boundsObject);
            bounds = boundsObject.AddComponent<BattleBounds>();
            if (halfExtents.HasValue)
            {
                var field = typeof(BattleBounds).GetField(
                    "halfExtents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.That(field, Is.Not.Null, "무대 확인: BattleBounds의 halfExtents 필드 이름이 바뀌었다.");
                field.SetValue(bounds, halfExtents.Value);
            }

            var serviceObject = new GameObject("PathfindingService");
            _createdObjects.Add(serviceObject);
            return serviceObject.AddComponent<BattlePathfindingService>();
        }

        /// <summary>어트리뷰트 묶음 없이 최소한으로만 갖춘 엄폐 지점을 놓는다. 그 결핍을 한 번 경고한다.</summary>
        private CoverPoint CreateBareCoverPoint(Vector3 position)
        {
            var coverObject = new GameObject("Cover");
            _createdObjects.Add(coverObject);
            coverObject.transform.position = position;
            LogAssert.Expect(LogType.Warning, new Regex("어트리뷰트 집합에"));
            return coverObject.AddComponent<CoverPoint>();
        }
    }
}
