using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Tactics.Cover;
using HS.Tactics.Pathfinding;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.PlayMode
{
    /// <summary>
    /// 엄폐물 중심을 목적지로 요청하면 실제 경로 계획 서비스가 접근 지점까지 경로를 내는지 검증한다.
    /// 엄폐물은 LiveObstacles에도 포함되므로 목적지의 엄폐물과 다른 장애물을 구분해야 한다.
    /// 다른 유닛 충돌, 어빌리티 시스템과 엄폐 선택 규칙은 이 파일의 범위 밖이다.
    /// </summary>
    public sealed class BattlePathfindingServiceCoverReachabilityTests
    {
        private readonly List<GameObject> _createdObjects = new();

        [Test]
        public void AFiniteRadiusCoverPathNeverPassesThroughItsOwnCover()
        {
            var service = CreateService();
            var cover = CreateBareCoverPoint(Vector3.zero);
            var corners = new List<Vector3>();
            Assert.That(service.Plan(new Vector3(-4f, 0f, 0f), cover.Position, corners, 0.5f), Is.True);
            var obstacle = CoverApproachGeometry.Expand(cover.Bounds, 0.5f);
            for (var index = 1; index < corners.Count; index++)
            {
                Assert.That(HS.Tactics.Foundation.Geometry.PlanarGeometry.IntersectsSegment(obstacle,
                    HS.Tactics.Foundation.Geometry.PlanarPosition.FromWorld(corners[index - 1]),
                    HS.Tactics.Foundation.Geometry.PlanarPosition.FromWorld(corners[index])), Is.False);
            }

            Assert.That(corners[corners.Count - 1], Is.Not.EqualTo(cover.Position));
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
        public void APlanToALiveCoverPointItselfSucceeds()
        {
            var service = CreateService();
            var cover = CreateBareCoverPoint(new Vector3(0f, 0f, 0f));

            var corners = new List<Vector3>();
            var found = service.Plan(new Vector3(-10f, 0f, 0f), cover.Position, corners);

            Assert.That(found, Is.True, "엄폐 지점 자신을 목적지로 계획할 수 있어야 한다.");
            Assert.That(corners.Count, Is.GreaterThan(0));
            var last = corners[corners.Count - 1];
            Assert.That(last.x, Is.EqualTo(cover.Position.x).Within(0.01f));
            Assert.That(last.z, Is.EqualTo(cover.Position.z).Within(0.01f));
        }

        [Test]
        public void TravelDistanceToALiveCoverPointItselfSucceeds()
        {
            var service = CreateService();
            var cover = CreateBareCoverPoint(new Vector3(0f, 0f, 0f));

            var found = service.TryMeasureTravelDistance(new Vector3(-10f, 0f, 0f), cover.Position, out var distance);

            Assert.That(found, Is.True, "엄폐 지점까지의 이동 거리를 잴 수 있어야 한다.");
            Assert.That(distance, Is.GreaterThan(0f));
        }

        [Test]
        public void TwoOverlappingLiveCoverPointsStillBlockEachOthersCenter()
        {
            // 겹친 엄폐물끼리는 어느 쪽이 목적지의 주인인지 가릴 수 없으므로 여전히 막힌 것으로 봐야 한다.
            var service = CreateService();
            var first = CreateBareCoverPoint(new Vector3(0f, 0f, 0f));
            CreateBareCoverPoint(new Vector3(0.3f, 0f, 0f));

            var corners = new List<Vector3>();
            var found = service.Plan(new Vector3(-10f, 0f, 0f), first.Position, corners);

            Assert.That(found, Is.False, "겹친 엄폐물이 서로의 자리를 함께 막아 이 목적지는 여전히 갈 수 없어야 한다.");
        }

        /// <summary>서비스와 경계를 씬에 놓는다.</summary>
        private BattlePathfindingService CreateService()
        {
            var boundsObject = new GameObject("Bounds");
            _createdObjects.Add(boundsObject);
            boundsObject.AddComponent<BattleBounds>();

            var serviceObject = new GameObject("PathfindingService");
            _createdObjects.Add(serviceObject);
            return serviceObject.AddComponent<BattlePathfindingService>();
        }

        /// <summary>어트리뷰트 집합 없이 최소한으로만 갖춘 엄폐 지점을 놓는다. 그 결핍을 한 번 경고한다.</summary>
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
